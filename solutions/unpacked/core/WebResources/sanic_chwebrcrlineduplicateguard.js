// ch_crline-duplicate-guard.js
// Wire ch_crline_prevent_duplicate_onSave on the ChangeRequestLine Quick Create form "On Save" (pass execution context)

const CRLINE_ENTITY_LOGICAL     = "sanic_chtblclientdatachangerequestline";
const CRLINE_ID_ATTR            = "sanic_chtblclientdatachangerequestlineid";

const CRLINE_PARENT_ATTR        = "sanic_changerequestid";    // line → CR lookup
const CRLINE_FIELD_ATTR         = "sanic_fielddefinition";            // line → Field Definition lookup

const CR_ENTITY_LOGICAL         = "sanic_chtblclientdatachangerequest";
const CR_STATUS_ATTR            = "cr140_changestatus";           // CR status (Choice)
const CR_STATUS_DRAFT_VALUE     = 796490000;             // <-- replace with your Draft option value

// (helper) strip braces from GUID
function _id(x){ return (x || "").replace(/[{}]/g, ""); }

// Main OnSave handler (non-async; cancels save immediately)
function ch_crline_prevent_duplicate_onSave(executionContext){
  const ctx  = executionContext.getFormContext();
  const args = executionContext.getEventArgs();

  // 1) Cancel the platform save right away so the pane stays open.
  //    We'll re-save manually after our async checks pass.
  args.preventDefault();

  // 2) Start async validation flow
  (async () => {
    try {
      // -- Read parent CR
      const crRef = ctx.getAttribute(CRLINE_PARENT_ATTR)?.getValue();
      if (!crRef || !crRef[0] || !crRef[0].id) {
        await Xrm.Navigation.openAlertDialog({ text: "Create lines from a Change Request subgrid so the parent is set." });
        return; // keep pane open
      }
      const crId = _id(crRef[0].id);

      // -- Parent must be Draft
      const cr = await Xrm.WebApi.retrieveRecord(CR_ENTITY_LOGICAL, crId, `?$select=${CR_STATUS_ATTR}`);
      const status = cr[CR_STATUS_ATTR];
      if (status !== CR_STATUS_DRAFT_VALUE) {
        await Xrm.Navigation.openAlertDialog({ text: "You can only add or edit lines while the Change Request is in Draft." });
        return;
      }

      // -- Selected field (required anyway)
      const fld = ctx.getAttribute(CRLINE_FIELD_ATTR)?.getValue();
      if (!fld || !fld[0] || !fld[0].id) {
        // Let the platform’s required rule show; we won’t re-save.
        return;
      }
      const fieldId = _id(fld[0].id);

      // -- Exclude current line if editing
      let currentId = null;
      try {
        currentId = ctx.getAttribute(CRLINE_ID_ATTR)?.getValue?.();
        currentId = _id(currentId);
      } catch(e){ /* ignore */ }

      // -- Duplicate check: any other line with same CR + same Field?
      //    We query the line entity with a filter on the two lookups.
      //    Lookup filters use the internal "_<schema>_value" OData field.
      const filterParts = [
        `_${CRLINE_PARENT_ATTR}_value eq ${crId}`,
        `_${CRLINE_FIELD_ATTR}_value eq ${fieldId}`
      ];
      if (currentId) filterParts.push(`${CRLINE_ID_ATTR} ne ${currentId}`);
      const filter = filterParts.join(" and ");

      const dupes = await Xrm.WebApi.retrieveMultipleRecords(
        CRLINE_ENTITY_LOGICAL,
        `?$select=${CRLINE_ID_ATTR}&$top=1&$filter=${encodeURI(filter)}`
      );

      if (dupes.entities && dupes.entities.length > 0) {
        await Xrm.Navigation.openAlertDialog({
          text: "There is already a change line for this field on this Change Request. Edit the existing line instead."
        });
        return; // keep pane open
      }

      // -- All checks passed → programmatically save (pane remains open unless user chose Save&Close)
      //    Use data.save() to continue the save we canceled.
      await ctx.data.save(); // no options = honors the user’s intent

    } catch (err) {
      console.error("ch_crline_prevent_duplicate_onSave error:", err);
      await Xrm.Navigation.openAlertDialog({
        text: "We couldn't validate this line. Please try again."
      });
      // keep pane open
    }
  })();
}

// expose
if (typeof window !== "undefined") {
  window.ch_crline_prevent_duplicate_onSave = ch_crline_prevent_duplicate_onSave;
}


