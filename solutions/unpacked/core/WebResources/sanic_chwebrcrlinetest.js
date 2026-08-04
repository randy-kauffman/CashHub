// ch_/crline-test.js
function ch_crline_test_onload(executionContext){
  try {
    var ctx = executionContext.getFormContext();
    ctx.ui.setFormNotification("✅ QC form library loaded", "INFO", "cg_test_loaded");
    console.log("cg_crline_test_onload reached; window has keys:", Object.keys(window));
  } catch (e) {
    console.error("ch_crline_test_onload error:", e);
  }
}
if (typeof window !== "undefined") window.ch_crline_test_onload = ch_crline_test_onload;
