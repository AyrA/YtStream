"use strict";

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
    var updateFileLabel = function () {
        var lbl = this.dataset.fileLabel;
        if (lbl) {
            var e = q(lbl);
            if (e) {
                if (this.files.length > 0) {
                    var text = this.files[0].name;
                    if (this.files.length > 1) {
                        text += " (and " + (this.files.length - 1) + " more)";
                    }
                    e.textContent = text;
                }
                else {
                    e.textContent = this.multiple ? "Select one or more files" : "Select a file";
                }
            }
        }
    };

    Array.from(qa(".copy-on-click")).forEach(function (v) {
        v.addEventListener("click", copyTextEvent);
    });
    Array.from(qa(".backlink")).forEach(function (v) {
        v.addEventListener("click", function (e) {
            e.preventDefault();
            history.back();
        });
    });
    Array.from(qa("[type=file]")).forEach(function (v) {
        if (v.dataset.fileLabel) {
            v.addEventListener("change", updateFileLabel);
            updateFileLabel.call(v);
        }
    });
})(document.querySelector.bind(document), document.querySelectorAll.bind(document));