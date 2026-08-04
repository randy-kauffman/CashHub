// ch_propose-change.js
// Opens Quick Create for Change Request, pre-filling target info.
// Hook this from a Command Bar button (Run JavaScript).

async function ch_openProposeChange(primaryControl, entityLogicalName, typeValue, draftStatusValue) {
  try {
    // primaryControl is the form context (when invoked from an open form).
    // If invoked from a grid, primaryControl is the grid context; grab the first selected row.
    let recordId = null;

    if (primaryControl &&  typeof primaryControl.data.entity.getId=== "function") {
      // Form
      recordId = primaryControl.data.entity.getId();

    } else if (primaryControl && primaryControl.getSelectedRows) {
      // Grid (command run from a grid)
      const rows = primaryControl.getSelectedRows();
      if (rows.getLength() > 0) {
        recordId = rows.get(0).getData().getEntity().getId();
        entityLogicalName = rows.get(0).getData().getEntity().getEntityName();
      }
    }

    if (!recordId) {
      Xrm.Navigation.openAlertDialog({ text: "No record selected." });
      return;
    }

    // Normalize GUID (remove braces)
    recordId = recordId.replace(/[{}]/g, "");


    // 1) entityFormOptions
    
    const entityFormOptions = {
        entityName: "sanic_chtblclientdatachangerequest",
        useQuickCreateForm: true
    }


    // 2) Form parameters
    // Option set integer values: pass them from the command (see section C)
    // typeValue: e.g., Update = 100000000
    // draftStatusValue: Draft = 100000000 (replace with your actual values)

    const formParameters = {
        sanic_targettable: entityLogicalName,   // e.g., "account" or your custom table logical name like "cg_client"
        sanic_targetrecordid: recordId,         // store as text (GUID)
        sanic_type: typeValue,                  // option set INT for "Update"
        sanic_changestatus: draftStatusValue          // option set INT for "Draft"
    };

    await Xrm.Navigation.openForm(entityFormOptions,formParameters);
    // After user saves Quick Create, they’ll land back on current page;
    // You can also navigate to the newly created CR if you prefer (needs a save handler).
  } catch (e) {
    console.error("ch_openProposeChange error", e);
    Xrm.Navigation.openAlertDialog({ text: "Unable to open Propose Change form. Details: " + (e.message || e) });
  }
}

// Expose for command designer
if (typeof window !== "undefined") {
  window.ch_openProposeChange = ch_openProposeChange;
}