// ch-crlinefilter.js
// Filters the ChangeRequestLine.Field lookup to Field Definitions
// where ch_tablelogicalname == parent CR's cg_targettable AND cg_isallowed == true.

async function ch_crline_onload(executionContext) {
  var formCtx = executionContext.getFormContext();

  var PARENT_CR_LOOKUP_ATTR = "sanic_changerequestid";   // lookup on Line -> ChangeRequest
  var FIELD_LOOKUP_ATTR     = "sanic_fielddefinition";           // attribute name for Field Definition lookup on the Line
  var FIELD_ENTITY_LOGICAL  = "sanic_chtblclienttablelogicalfieldname"; // entity logical name of Field Definition
  var FIELD_TABLE_NAME_ATTR = "sanic_table";// text on Field Definition storing target table logical name
  var FIELD_ISALLOWED_ATTR  = "sanic_isallowed";       // two-options on Field Definition (true=1)
  var CR_ENTITY_LOGICAL     = "sanic_chtblclientdatachangerequest";   // entity logical name of Change Request
  var CR_TARGETTABLE_ATTR   = "sanic_targettable";     // text on Change Request


  try {
    // 1) Ensure the Field lookup CONTROL exists on this QC form
    var fieldCtrl = formCtx.getControl(FIELD_LOOKUP_ATTR);
    if (!fieldCtrl) {
      formCtx.ui.setFormNotification("Field lookup control '"+FIELD_LOOKUP_ATTR+"' not found on Quick Create form.", "ERROR", "ch_no_field_ctrl");
      console.warn("[ch] Missing control:", FIELD_LOOKUP_ATTR);
      return;
    }

    // 2) Get Parent CR id from the line's parent lookup (must be on the form, hidden is fine)
    var parentCrRef = formCtx.getAttribute(PARENT_CR_LOOKUP_ATTR)?.getValue();
    if (!parentCrRef || !parentCrRef[0] || !parentCrRef[0].id) {
      formCtx.ui.setFormNotification("Parent Change Request is empty on Quick Create. Make sure you open from the subgrid and include the parent lookup on the QC form.", "WARNING", "ch_no_parent");
      console.warn("[ch] Parent CR lookup empty. Ensure subgrid is 'Only related' and parent lookup attribute is on QC form.");
      return;
    }
    var parentCrId = parentCrRef[0].id.replace(/[{}]/g, "");

    // 3) Retrieve the parent CR to read its target table
    var select = "?$select=" + encodeURIComponent(CR_TARGETTABLE_ATTR);
    var cr = await Xrm.WebApi.retrieveRecord(CR_ENTITY_LOGICAL, parentCrId, select);
    var targetTable = cr[CR_TARGETTABLE_ATTR];

    if (!targetTable) {
      formCtx.ui.setFormNotification("Parent Change Request has no target table set.", "ERROR", "ch_no_target");
      console.warn("[cg] CR has no target table:", CR_TARGETTABLE_ATTR);
      return;
    }

    // 4) Build the filter XML: table match + allowed flag
    var filterXml =
      "<filter type='and'>" +
        "<condition attribute='" + FIELD_TABLE_NAME_ATTR + "' operator='eq' value='" + targetTable + "' />" +
        "<condition attribute='" + FIELD_ISALLOWED_ATTR + "' operator='eq' value='1' />" +
      "</filter>";

    // 5) Register the filter right before the lookup searches
    fieldCtrl.addPreSearch(function () {
      try {
        fieldCtrl.addCustomFilter(filterXml, FIELD_ENTITY_LOGICAL);
      } catch (e) {
        console.warn("[cg] addCustomFilter warn (often safe to ignore):", e);
      }
    });

    // Optional: toast to confirm it ran (remove later)
    console.log("[cg] Filter registered for target table:", targetTable);
    formCtx.ui.setFormNotification("Field list filtered for '"+targetTable+"'.", "INFO", "ch_filter_ok");
    setTimeout(function(){ formCtx.ui.clearFormNotification("ch_filter_ok"); }, 3000);

  } catch (e) {
    console.error("[cg] crline_onload error:", e);
    formCtx.ui.setFormNotification("Error applying filter: " + (e.message || e), "ERROR", "ch_err");
  }
}

if (typeof window !== "undefined") window.ch_crline_onload = ch_crline_onload;