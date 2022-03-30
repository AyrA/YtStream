"use strict";

var tools = {
    q: document.querySelector.bind(document),
    qa: document.querySelectorAll.bind(document),
    id: {
        fromVideo: function (id) {
            id = String(id);
            id = id.replace(/_/g, '/').replace(/-/g, '+') + "=";
            if (id.length === 12) {
                try {
                    return atob(id).split('').map(v => ("0" + v.charCodeAt(0).toString(16)).match(/..$/)[0]).join("").toUpperCase();
                }
                catch (e) {
                    return null;
                }
            }
            return null;
        },
        fromCache: function (id) {
            id = String(id);
            if (!id.match(/^[\dA-F]{16}$/i)) {
                return null;
            }
            id = btoa(id.match(/../g).map(v => String.fromCharCode(parseInt(v, 16))).join(""));
            return id.replace(/\+/g, '-').replace(/\//g, '_').substr(0, 11);
        }
    },
    token: function () {
        var e = document.querySelector("[data-af-name]");
        if (e) {
            return {
                name: e.dataset.afName,
                token: e.dataset.afValue,
            };
        }
    },
    post: function (url, data) {
        return new Promise(function (accept, reject) {
            var fd = new FormData();
            var t = tools.token();
            if (t) {
                fd.append(t.name, t.token);
            }
            if (data && typeof (data) == typeof ({})) {
                Object.keys(data).forEach(function (key) {
                    fd.append(key, data[key]);
                });
            }
            var req = new XMLHttpRequest();
            req.responseType = "json";
            req.open("POST", url);
            req.addEventListener("error", reject);
            req.upload.addEventListener("error", reject);
            req.addEventListener("load", function () {
                if (req.status >= 300 || req.status < 200) {
                    reject(new Error("Invalid status code: " + req.status));
                }
                accept(req.response);
            });
            req.send(fd);
        });
    }
};

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