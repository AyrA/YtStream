"use strict";

(function (q) {
    var list = [];
    var patterns = {
        videos: [
            /(?:youtu\.be\/|youtube(?:-nocookie)?.com\/(?:v\/|e\/|.*u\/\w+\/|embed\/|.*v=))([\w\-]{10}[AEIMQUYcgkosw048])/i
        ],
        playlists: [
            // /youtube.com\/playlist?list=(PL[\w\-]+)/i,
            /\bPL(?:[\dA-F]{16}|[\w\-]{32})\b/
        ]
    };

    var renderList = function () {
        var tbl = q("#idTable tbody");
        while (tbl.childNodes.length) {
            tbl.childNodes[0].remove();
        }
        list.forEach(function (v, i) {
            var row = document.createElement("tr");
            row.appendChild(document.createElement("td")).textContent = v;
            var btnCell = row.appendChild(document.createElement("td"));
            var up = btnCell.appendChild(document.createElement("button"));
            btnCell.appendChild(document.createTextNode(" "));
            var down = btnCell.appendChild(document.createElement("button"));
            btnCell.appendChild(document.createTextNode(" "));
            var del = btnCell.appendChild(document.createElement("button"));

            up.classList.add("btn", "btn-success");
            down.classList.add("btn", "btn-success");
            del.classList.add("btn", "btn-danger");

            up.innerHTML = "&uarr;";
            down.innerHTML = "&darr;";
            del.textContent = "DEL";

            up.dataset.id = down.dataset.id = del.dataset.id = i;
            up.dataset.action = "up";
            down.dataset.action = "down";
            del.dataset.action = "del";
            up.disabled = i === 0;
            down.disabled = i === list.length - 1;

            tbl.appendChild(row);
        });
        $(tbl).find("button").on("click", function () {
            var id = +this.dataset.id;
            switch (this.dataset.action) {
                case "up":
                    //Remove item and insert at previous location
                    list.splice(id - 1, 0, list.splice(id, 1)[0]);
                    break;
                case "down":
                    //Remove item and insert into the next location
                    list.splice(id + 1, 0, list.splice(id, 1)[0]);
                    break;
                case "del":
                    //Remove item and discard it
                    list.splice(id, 1);
                    break;
            }
            renderList();
        });
    };

    var askIdType = function () {
        var mod = $("#idTypeModal");
        mod.modal("show");
        delete mod[0].dataset.result;
        return new Promise(function (a, r) {
            var cb = function (e) {
                mod[0].dataset.result = this.dataset.value;
                this.removeEventListener("click", cb);
            };
            var hidden = function () {
                mod.off("hidden.bs.modal", hidden);
                a(mod[0].dataset.result);
            };
            q("#btnIdModalVideoId").addEventListener("click", cb);
            q("#btnIdModalPlId").addEventListener("click", cb);
            q("#idTypeModal .modal-footer button").addEventListener("click", cb);
            mod.on("hidden.bs.modal", hidden);
        });
    };

    var matchId = function (str, patterns) {
        for (var i = 0; i < patterns.length; i++) {
            var m = str.match(patterns[i]);
            if (m) {
                return m[1] || m[0];
            }
        }
    };

    var addId = async function () {
        var field = q("#tbUrl");
        var ids = {
            video: matchId(field.value, patterns.videos),
            playlist: matchId(field.value, patterns.playlists)
        }
        if (!ids.video && !ids.playlist) {
            alert("Cannot find video or playlist id in this link");
        }
        else {
            if (ids.video && ids.playlist) {
                var result = await askIdType();
                switch (result) {
                    case "video":
                        ids.playlist = null;
                        break;
                    case "pl":
                        ids.video = null;
                        break;
                    default:
                        return;
                }
            }
            if (ids.video) {
                list.push(ids.video);
            }
            else {
                try {
                    list = list.concat(await tools.post("/Stream/List/" + ids.playlist));
                } catch (e) {
                    alert(e.message);
                }
                //TODO: Add playlist
                //TODO: Ask user if he wants the playlist or the playlist items
            }
            renderList();
        }
        console.log(ids);
        field.select();
    };
    q("#tbUrl").addEventListener("keydown", function (e) {
        if (e.keyCode === 13) {
            addId();
        }
    });
    q("#btnAddId").addEventListener("click", addId);
    //DEBUG
    list = "ABCDEFGHIJKLMN".split("");
    renderList();
})(document.querySelector.bind(document));