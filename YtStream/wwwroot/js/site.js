﻿"use strict";

(function (q, qa) {
    var copyText = function (node) {
        if (document.execCommand) {
            if (node instanceof Node) {
                if (document.body.createTextRange) {
                    var r = document.body.createTextRange();
                    r.moveToElementText(node);
                    r.select();
                } else if (window.getSelection && document.createRange) {
                    var s = window.getSelection();
                    var r = document.createRange();
                    r.selectNodeContents(node);
                    s.removeAllRanges();
                    s.addRange(r);
                } else {
                    return false;
                }
            }
            else {
                return false;
            }
            var status = document.execCommand("copy");
            if (document.selection && document.selection.empty) {
                document.selection.empty();
            } else {
                if (window.getSelection) {
                    window.getSelection().removeAllRanges();
                }
                var ele = document.activeElement;
                if (ele) {
                    var name = ele.nodeName.toLowerCase();
                    if (name === "textarea" || (name === "input" && ele.type === "text")) {
                        ele.selectionStart = ele.selectionEnd;
                    }
                }
            }
            return status;
        }
        return false;
    };
    var copyTextEvent = function (e) {
        var ele = this;
        if (copyText(ele)) {
            var count = 4;
            var flash = function () {
                ele.style.visibility = (count-- & 1) ? null : "hidden";
                if (count > 0) {
                    window.setTimeout(flash, 250);
                }
            };
            flash();
        }
        else {
            alert("Copying of text was rejected by your browser");
        }
    };
    Array.from(qa(".copy-on-click")).forEach(function (v) {
        v.addEventListener("click", copyTextEvent);
    });
})(document.querySelector.bind(document), document.querySelectorAll.bind(document));