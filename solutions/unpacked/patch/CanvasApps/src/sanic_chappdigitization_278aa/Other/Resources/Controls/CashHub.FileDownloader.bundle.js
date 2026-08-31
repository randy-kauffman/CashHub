/*
 * ATTENTION: The "eval" devtool has been used (maybe by default in mode: "development").
 * This devtool is neither made for production nor for readable output files.
 * It uses "eval()" calls to create a separate source file in the browser devtools.
 * If you are trying to read the output file, select a different devtool (https://webpack.js.org/configuration/devtool/)
 * or disable the default devtool with "devtool: false".
 * If you are looking for production-ready output files, see mode: "production" (https://webpack.js.org/configuration/mode/).
 */
var pcf_tools_652ac3f36e1e4bca82eb3c1dc44e6fad;
/******/ (() => { // webpackBootstrap
/******/ 	"use strict";
/******/ 	var __webpack_modules__ = ({

/***/ "./FileDownloader/index.ts"
/*!*********************************!*\
  !*** ./FileDownloader/index.ts ***!
  \*********************************/
(__unused_webpack_module, __webpack_exports__, __webpack_require__) {

eval("{__webpack_require__.r(__webpack_exports__);\n/* harmony export */ __webpack_require__.d(__webpack_exports__, {\n/* harmony export */   FileDownloader: () => (/* binding */ FileDownloader)\n/* harmony export */ });\n/**\n * CashHub.FileDownloader\n *\n * Baja un archivo generado en el servidor SIN navegar a Dataverse.\n *\n * EL PROBLEMA QUE RESUELVE. `Download(\".../sanic_csvcore/$value\")` es una NAVEGACION\n * del navegador. Power Fx no tiene forma de ponerle un header `Authorization`, asi que\n * lo unico que puede autenticarla es la cookie del org — que los usuarios de negocio\n * no tienen, porque viven en apps.powerapps.com. Les daba HTTP 401 y una pestaña en\n * blanco. Y muda: al no ser una operacion de la app, el fallo quedaba fuera del alcance\n * de cualquier `IfError`, asi que nadie se enteraba.\n *\n * Aca el contenido ya viene autenticado por el conector (Custom API\n * `sanic_CHGetBatchCsv`) y el archivo se arma en el navegador con un Blob. No se navega\n * a ningun lado, no viaja ninguna credencial y no se depende del estado de la sesion.\n */\nclass FileDownloader {\n  constructor() {\n    /**\n     * Ultimo token ya atendido. `updateView` corre ante CUALQUIER cambio de propiedad\n     * y ante re-renders que no controlamos; sin esta memoria el usuario terminaria con\n     * el archivo bajado varias veces por una sola pulsacion.\n     */\n    this.lastTrigger = \"\";\n    this.status = \"\";\n    this.bytesDownloaded = 0;\n    /** URL del objeto anterior, para revocarla y no filtrar memoria en la sesion. */\n    this.lastObjectUrl = null;\n  }\n  init(_context, notifyOutputChanged, _state, _container) {\n    this.notifyOutputChanged = notifyOutputChanged;\n    // Sin UI a proposito: es un componente de comportamiento, no de presentacion.\n    // En la pantalla va como un cuadrito de 1x1 fuera de la vista.\n  }\n  updateView(context) {\n    var _a, _b, _c, _d, _e;\n    var trigger = (_a = context.parameters.Trigger.raw) !== null && _a !== void 0 ? _a : \"\";\n    // Nada que hacer: o no hay disparo, o este token ya se atendio.\n    if (trigger === \"\" || trigger === this.lastTrigger) {\n      return;\n    }\n    this.lastTrigger = trigger;\n    var content = (_b = context.parameters.Content.raw) !== null && _b !== void 0 ? _b : \"\";\n    var fileName = ((_c = context.parameters.FileName.raw) !== null && _c !== void 0 ? _c : \"\").trim() || \"archivo.csv\";\n    var mimeType = ((_d = context.parameters.MimeType.raw) !== null && _d !== void 0 ? _d : \"\").trim() || \"text/csv;charset=utf-8\";\n    var expectedBytes = (_e = context.parameters.ExpectedBytes.raw) !== null && _e !== void 0 ? _e : 0;\n    if (content === \"\") {\n      // Bajar un archivo vacio es peor que no bajar nada: el usuario cree que\n      // tiene el lote y lo que tiene es un archivo en blanco.\n      this.report(\"sin-contenido\", 0);\n      return;\n    }\n    try {\n      // UTF-8 explicito. El contenido viaja tal cual quedo archivado —saltos de\n      // linea CRLF incluidos— y aca no se normaliza nada: lo que se baja tiene\n      // que ser identico a lo que se mando al core, o el archivo no prueba nada.\n      var bytes = new TextEncoder().encode(content);\n      // VERIFICACION DE INTEGRIDAD. El servidor dice cuantos bytes archivo; se\n      // comparan con los que de verdad se arman aca. Si no coinciden, el\n      // contenido llego cortado y NO se baja: un CSV incompleto que entra a un\n      // core bancario es mucho peor que una descarga que falla a la vista.\n      if (expectedBytes > 0 && bytes.byteLength !== expectedBytes) {\n        this.report(\"tama\\xF1o-no-coincide:esperados \".concat(expectedBytes, \" bytes, llegaron \").concat(bytes.byteLength), bytes.byteLength);\n        return;\n      }\n      var blob = new Blob([bytes], {\n        type: mimeType\n      });\n      if (this.lastObjectUrl !== null) {\n        URL.revokeObjectURL(this.lastObjectUrl);\n      }\n      this.lastObjectUrl = URL.createObjectURL(blob);\n      var anchor = document.createElement(\"a\");\n      anchor.href = this.lastObjectUrl;\n      anchor.download = fileName;\n      anchor.style.display = \"none\";\n      // El ancla tiene que estar en el documento para que el click cuente como\n      // gesto de usuario en Firefox; en Chromium no hace falta, pero agregarla y\n      // sacarla no cuesta nada y evita una diferencia entre navegadores.\n      document.body.appendChild(anchor);\n      anchor.click();\n      document.body.removeChild(anchor);\n      this.report(\"ok\", bytes.byteLength);\n    } catch (e) {\n      this.report(\"error:\".concat(e instanceof Error ? e.message : String(e)), 0);\n    }\n  }\n  report(status, bytes) {\n    this.status = status;\n    this.bytesDownloaded = bytes;\n    this.notifyOutputChanged();\n  }\n  getOutputs() {\n    return {\n      Status: this.status,\n      BytesDownloaded: this.bytesDownloaded\n    };\n  }\n  destroy() {\n    if (this.lastObjectUrl !== null) {\n      URL.revokeObjectURL(this.lastObjectUrl);\n      this.lastObjectUrl = null;\n    }\n  }\n}\n\n//# sourceURL=webpack://pcf_tools_652ac3f36e1e4bca82eb3c1dc44e6fad/./FileDownloader/index.ts?\n}");

/***/ }

/******/ 	});
/************************************************************************/
/******/ 	// The require scope
/******/ 	const __webpack_require__ = {};
/******/ 	
/************************************************************************/
/******/ 	/* webpack/runtime/define property getters */
/******/ 	// define getter/value functions for harmony exports
/******/ 	__webpack_require__.d = (exports, definition) => {
/******/ 		for(var key in definition) {
/******/ 			if(__webpack_require__.o(definition, key) && !__webpack_require__.o(exports, key)) {
/******/ 				Object.defineProperty(exports, key, { enumerable: true, get: definition[key] });
/******/ 			}
/******/ 		}
/******/ 	};
/******/ 	
/******/ 	/* webpack/runtime/hasOwnProperty shorthand */
/******/ 	__webpack_require__.o = (obj, prop) => (Object.prototype.hasOwnProperty.call(obj, prop));
/******/ 	
/******/ 	/* webpack/runtime/make namespace object */
/******/ 	// define __esModule on exports
/******/ 	__webpack_require__.r = (exports) => {
/******/ 		Object.defineProperty(exports, Symbol.toStringTag, { value: 'Module' });
/******/ 		Object.defineProperty(exports, '__esModule', { value: true });
/******/ 	};
/******/ 	
/************************************************************************/
/******/ 	
/******/ 	// startup
/******/ 	// Load entry module and return exports
/******/ 	// This entry module can't be inlined because the eval devtool is used.
/******/ 	let __webpack_exports__ = {};
/******/ 	__webpack_modules__["./FileDownloader/index.ts"](0,__webpack_exports__,__webpack_require__);
/******/ 	pcf_tools_652ac3f36e1e4bca82eb3c1dc44e6fad = __webpack_exports__;
/******/ 	
/******/ })()
;
if (window.ComponentFramework && window.ComponentFramework.registerControl) {
	ComponentFramework.registerControl('CashHub.FileDownloader', pcf_tools_652ac3f36e1e4bca82eb3c1dc44e6fad.FileDownloader);
} else {
	var CashHub = CashHub || {};
	CashHub.FileDownloader = pcf_tools_652ac3f36e1e4bca82eb3c1dc44e6fad.FileDownloader;
	pcf_tools_652ac3f36e1e4bca82eb3c1dc44e6fad = undefined;
}