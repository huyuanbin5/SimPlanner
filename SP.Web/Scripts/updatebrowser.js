﻿(function () {
    window.$buoop = { vs: { i: 11, f: -4, o: -4, s: 8, c: -4 }, api: 4 };
    function $buo_f() {
        var e = document.createElement("script");
        e.src = "//browser-update.org/update.min.js";
        document.body.appendChild(e);
    };
    try { document.addEventListener("DOMContentLoaded", $buo_f, false) }
    catch (e) { window.attachEvent("onload", $buo_f) }
})()
	