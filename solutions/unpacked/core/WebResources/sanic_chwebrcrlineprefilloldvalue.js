
// ch_crline-prefill-oldvalue.js
// Prefills cg_oldvalue on ChangeRequestLine Quick Create BEFORE save.
// Wire this function on the QC form's On Load: ch_crline_prefill_oldvalue_onload

const CRLINE_ENTITY_LOGICAL       = "sanic_chtblclientdatachangerequestline";
const CRLINE_PARENT_LOOKUP_ATTR   = "sanic_changerequestid";     // Line → ChangeRequest (lookup attribute on the line)
const CRLINE_FIELDLOOKUP_ATTR     = "sanic_fielddefinition";             // Line → Field Definition (lookup attribute on the line)
const CRLINE_OLDVALUE_ATTR        = "sanic_oldvalue";          // Line → OldValue (text attribute)
const FIELDDEF_ENTITY_LOGICAL     = "sanic_chtblclienttablelogicalfieldname";   // Field Definition entity logical name
const FIELDDEF_FIELDNAME_ATTR     = "sanic_logicalname";  // Field Definition → column schema name (text)
const FIELDDEF_DATATYPE_ATTR      = "sanic_datatype";          // Field Definition → datatype (optional)
const CR_ENTITY_LOGICAL           = "sanic_chtblclientdatachangerequest";     // Change Request entity logical name
const CR_TARGETTABLE_TEXT_ATTR    = "sanic_targettable";       // CR → target table (text, e.g., "cg_client")
const CR_TARGETRECORDID_ATTR      = "sanic_targetrecordid";    // CR → target record id (text GUID without braces)

let ch_cachedTarget = { table: null, id: null };

async function ch_crline_prefill_oldvalue_onload(executionContext) {
  const formCtx = executionContext.getFormContext();

  // Resolve parent CR target (table + id), cache it for this QC session
  if (!ch_cachedTarget.table || !ch_cachedTarget.id) {
    const ok = await ch_resolveParentTarget(formCtx);
    if (!ok) {
      formCtx.ui.setFormNotification(
        "Parent Change Request target not set yet. Create this line from the CR subgrid.",
        "WARNING",
        "ch_no_parent"
      );
      return;
    } else {
      formCtx.ui.clearFormNotification("ch_no_parent");
    }
  }

  // If a field is already selected (user navigated back), prefill immediately
  await ch_prefillOldValue(formCtx);

  // Recalculate OldValue whenever the Field changes
  const fldAttr = formCtx.getAttribute(CRLINE_FIELDLOOKUP_ATTR);
  if (fldAttr && !fldAttr._ch_boundOldHandler) {
    fldAttr.addOnChange(async function(){ await ch_prefillOldValue(formCtx); });
    fldAttr._ch_boundOldHandler = true;
  }
}

// ------- helpers -------

async function ch_resolveParentTarget(formCtx){
  // try a couple times in case parent lookup hydrates slightly after load
  for (let i=0; i<3; i++) {
    const parentRef = formCtx.getAttribute(CRLINE_PARENT_LOOKUP_ATTR)?.getValue();
    if (parentRef && parentRef[0] && parentRef[0].id) {
      const crId = parentRef[0].id.replace(/[{}]/g, "");
      const qs = `?$select=${CR_TARGETTABLE_TEXT_ATTR},${CR_TARGETRECORDID_ATTR}`;
      const cr = await Xrm.WebApi.retrieveRecord(CR_ENTITY_LOGICAL, crId, qs);
      const table = cr[CR_TARGETTABLE_TEXT_ATTR];
      const id    = cr[CR_TARGETRECORDID_ATTR];
      if (table && id) {
        ch_cachedTarget = { table, id };
        return true;
      }
    }
    await new Promise(r => setTimeout(r, 150));
  }
  return false;
}

async function ch_prefillOldValue(formCtx){
  const fld = formCtx.getAttribute(CRLINE_FIELDLOOKUP_ATTR)?.getValue();
  if (!fld || !fld[0] || !fld[0].id) {
    formCtx.getAttribute(CRLINE_OLDVALUE_ATTR)?.setValue(null);
    return;
  }
  const fdId = fld[0].id.replace(/[{}]/g,"");
  const fd = await Xrm.WebApi.retrieveRecord(
    FIELDDEF_ENTITY_LOGICAL,
    fdId,
    `?$select=${FIELDDEF_FIELDNAME_ATTR},${FIELDDEF_DATATYPE_ATTR}`
  );
  const logical = fd[FIELDDEF_FIELDNAME_ATTR];
  const dtype   = fd[FIELDDEF_DATATYPE_ATTR];

  if (!logical) return;

  // Pull ONLY that column from the target row
  const rec = await Xrm.WebApi.retrieveRecord(
    ch_cachedTarget.table,
    ch_cachedTarget.id,
    `?$select=${logical}`
  );

  const oldText = ch_formatFromRecord(rec, logical, dtype);
  const attr = formCtx.getAttribute(CRLINE_OLDVALUE_ATTR);
  attr && attr.setValue(oldText);

  // Keep OldValue read-only
  const ctrl = formCtx.getControl(CRLINE_OLDVALUE_ATTR);
  if (ctrl) ctrl.setDisabled(true);
}

function ch_formatFromRecord(rec, logicalName, dataType){
  // Prefer formatted label when present
  const fmtKey = `${logicalName}@OData.Community.Display.V1.FormattedValue`;
  let val = rec[fmtKey];

  // Lookups are stored as _<name>_value
  if (!val && dataType && String(dataType).toLowerCase() === "lookup") {
    const lkKey = `_${logicalName}_value`;
    val = rec[lkKey] || "";
    const lkFmt = `${lkKey}@OData.Community.Display.V1.FormattedValue`;
    val = rec[lkFmt] || val;
  }

  if (val === undefined || val === null) val = rec[logicalName];
  if (val === undefined || val === null || val === "") return "— (blank) —";
  return String(val);
}
