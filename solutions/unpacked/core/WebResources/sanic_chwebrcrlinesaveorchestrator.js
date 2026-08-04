/ cg_/crline-save.js
// Registers an OnSave handler at runtime to avoid CSP inline-handler blocks.

// ---- CONFIG: adjust to your schema ----
const CRLINE_ENTITY_LOGICAL  = "sanic_chtblclientdatachangerequestline";
const CRLINE_ID_ATTR         = "sanic_chtblclientdatachangerequestlineid";
const CRLINE_PARENT_ATTR     = "sanic_changerequestid";   // line → CR lookup
const CRLINE_FIELD_ATTR      = "sanic_fielddefinition";           // line → Field Definition lookup
const CR_ENTITY_LOGICAL      = "sanic_chtblclientdatachangerequest";
const CR_STATUS_ATTR         = "cr140_changestatus";
const CR_STATUS_DRAFT_VALUE  = 796490000;            // <-- replace with your Draft option value

function _id(x){ return (x || "").replace(/[{}]/g,""); }

// 1) ONLOAD: register the save handler programmatically (no inline binding)
function ch_crline_onload_register(executionContext){
  try {
    var ctx = executionContext.getFormContext();

    // Avoid duplicate registration when user re-opens QC
    if (ctx.data && typeof ctx.data.removeOnSave === "function") {
      ctx.data.removeOnSave(ch_crline_onsave);
    }
    if (ctx.data && typeof ctx.data.addOnSave === "function") {
      ctx.data.addOnSave(ch_crline_onsave);
    } else if (ctx.data && ctx.data.entity && typeof ctx.data.entity.addOnSave === "function") {
      // Fallback for older API signatures
      ctx.data.entity.addOnSave(ch_crline_onsave);
    }

    // Optional visual cue to confirm the library loaded
    // ctx.ui.setFormNotification("QC events wired.", "INFO", "cg_wired");
  } catch (e) {
    console.error("OnLoad register failed", e);
  }
}

// 2) ONSAVE: cancel first, validate async, re-save only if OK
function ch_crline_onsave(executionContext){
  var ctx  = executionContext.getFormContext();
  var args = executionContext.getEventArgs();

  // Always cancel the platform save to keep the QC pane open
  if (args && typeof args.preventDefault === "function") args.preventDefault();

  (async function(){
    try {
      // --- Parent CR must be set & Draft ---
      var crRef = ctx.getAttribute(CRLINE_PARENT_ATTR)?.getValue();
      if (!crRef || !crRef[0]?.id) {
        await Xrm.Navigation.openAlertDialog({ text: "Create lines from the Change Request subgrid." });
        return;
      }
      var crId = _id(crRef[0].id);

      var cr = await Xrm.WebApi.retrieveRecord(CR_ENTITY_LOGICAL, crId, `?$select=${CR_STATUS_ATTR}`);
      if (cr[CR_STATUS_ATTR] !== CR_STATUS_DRAFT_VALUE){
        await Xrm.Navigation.openAlertDialog({ text: "You can only add or edit lines while the Change Request is in Draft." });
        return;
      }

      // --- Selected Field required ---
      var fld = ctx.getAttribute(CRLINE_FIELD_ATTR)?.getValue();
      if (!fld || !fld[0]?.id) {
        // Let platform required rules show; we don't re-save.
        return;
      }
      var fieldId = _id(fld[0].id);

      // --- Exclude current id (edit case) ---
      var currentId = null;
      try { currentId = _id(ctx.getAttribute(CRLINE_ID_ATTR)?.getValue?.()); } catch (_){}

      // --- Duplicate check: same CR + same Field exists? ---
      var filters = [
        `_${CRLINE_PARENT_ATTR}_value eq ${crId}`,
        `_${CRLINE_FIELD_ATTR}_value eq ${fieldId}`
      ];
      if (currentId) filters.push(`${CRLINE_ID_ATTR} ne ${currentId}`);

      var dupes = await Xrm.WebApi.retrieveMultipleRecords(
        CRLINE_ENTITY_LOGICAL,
        `?$select=${CRLINE_ID_ATTR}&$top=1&$filter=${encodeURI(filters.join(" and "))}`
      );

      if (dupes.entities && dupes.entities.length > 0){
        await Xrm.Navigation.openAlertDialog({ text: "A line for this field already exists. Edit the existing line instead." });
        return;
      }

      // --- All good → programmatic save (respects Save vs Save&Close) ---
      await ctx.data.save();

    } catch (e) {
      console.error("ch_crline_onsave error", e);
      await Xrm.Navigation.openAlertDialog({ text: "Could not validate/save. Please try again." });
    }
  })();
}

// Expose globals (defensive for CSP)
if (typeof window !== "undefined") {
  window.ch_crline_onload_register = ch_crline_onload_register;
  window.ch_crline_onsave = ch_crline_onsave;
}
