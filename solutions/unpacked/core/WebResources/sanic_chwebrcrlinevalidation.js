async function ch_crline_validate_onSave(executionContext) {
  const ctx = executionContext.getFormContext();
  const eventArgs = executionContext.getEventArgs();
  const fld = ctx.getAttribute("sanic_fielddefinition")?.getValue();
  const newValAttr = ctx.getAttribute("sanic_newvalue");
  const newVal = (newValAttr?.getValue() || "").trim();

  if (!fld || !newVal) return; // handled by required rule

  // Read Field Definition metadata
  const fieldId = fld[0].id.replace(/[{}]/g, "");
  const fd = await Xrm.WebApi.retrieveRecord("sanic_chtblclienttablelogicalfieldname", fieldId,
    "?$select=sanic_datatype,sanic_optionsetmap,sanic_lookuptargettable");

  const dt = fd["sanic_datatype"]; // e.g., "Text","Number","Date","Money","Lookup","OptionSet","TwoOptions"
  const map = fd["sanic_optionsetmap"]; // JSON string (optional)
  let ok = true, msg = "";

  switch (dt) {
    case "Number":
    case "Money":
      ok = !isNaN(parseFloat(newVal)); 
      if (!ok) msg = "Enter a valid number (e.g., 1234.56).";
      break;
    case "Date":
      ok = !isNaN(Date.parse(newVal));
      if (!ok) msg = "Enter a valid date (e.g., 2025-10-12).";
      break;
    case "TwoOptions":
      ok = /^(true|false|yes|no)$/i.test(newVal);
      if (!ok) msg = "Enter Yes/No or True/False.";
      break;
    case "OptionSet":
      try {
        const m = map ? JSON.parse(map) : {};
        const labels = Object.keys(m); // {"Active":1,"Inactive":2}
        const values = Object.values(m).map(v => String(v));
        ok = labels.some(l => l.toLowerCase() === newVal.toLowerCase()) || values.includes(newVal);
        if (!ok) msg = "Enter one of: " + labels.join(", ") + " (or its numeric value).";
      } catch(_) { /* if no map, let flow handle */ }
      break;
    case "Lookup":
      // Accept GUID, or "Name (GUID)"; deeper picker comes in Option B
      ok = /^[0-9a-fA-F-]{36}$/.test(newVal) || /\(([0-9a-fA-F-]{36})\)$/.test(newVal);
      if (!ok) msg = "Enter a GUID or paste 'Name (GUID)'.";
      break;
    default: /* Text */ break;
  }

  if (!ok) {
    ctx.ui.setFormNotification(msg, "ERROR", "ch_newvalue");
    eventArgs.preventDefault(); // stop save
  } else {
    ctx.ui.clearFormNotification("ch_newvalue");
  }
}
// expose
if (typeof window!=="undefined") window.ch_crline_validate_onSave = ch_crline_validate_onSave;
