"use strict";
(function () {
    var translate = function (v) {
        if (!v) {
            return false;
        }
        v = String(v);
        if (v.length === 11) {
            tools.q("#tbConvert").value = tools.id.fromVideo(v) || v;
            return true;
        }
        else if (v.length === 16) {
            tools.q("#tbConvert").value = tools.id.fromCache(v) || v;
            return true;
        }
        return false;
    };
    tools.q("#tbConvert").addEventListener("keydown", function (e) {
        if (e.keyCode === 13) {
            translate(this.value);
        }
    });
    tools.q("#btnConvert").addEventListener("click", function () {
        translate(tools.q("#tbConvert").value);
    });
})();