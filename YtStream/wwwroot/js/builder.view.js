"use strict";

(function (q) {
    var list = [];
    var patterns = {
        videos: [
            /(?:youtu\.be\/|youtube(?:-nocookie)?.com\/(?:v\/|e\/|.*u\/\w+\/|embed\/|.*v=))([\w\-]{10}[AEIMQUYcgkosw048])/i,
            /^\s*([\w\-]{10}[AEIMQUYcgkosw048])\s*$/
        ],
        playlists: [
            // /youtube.com\/playlist?list=(PL[\w\-]+)/i,
            /\bPL(?:[\dA-F]{16}|[\w\-]{32})\b/
        ]
    };

    var playFile = function (id) {
        var p = q("audio");
        if (p) {
            p.pause();
        }
        else {
            p = document.body.appendChild(document.createElement("audio"));
            p.addEventListener("error", function () {
                alert("Unable to play this file. Video is restricted, unavailable, or invalid.");
            });
        }
        if (id) {
            if (p.dataset.id !== id) {
                p.dataset.id = id;
                p.src = location.origin + "/Stream/Order/" + id;
                p.play();
            }
            else {
                delete p.dataset.id;
            }
        }
        else {
            delete p.dataset.id;
        }
    };

    var renderList = function () {
        var tbl = q("#idTable tbody");
        while (tbl.childNodes.length) {
            tbl.childNodes[0].remove();
        }
        if (list.length === 0) {
            var row = tbl.appendChild(document.createElement("tr"));
            var cell = row.appendChild(document.createElement("td"));
            cell.setAttribute("colspan", "2");
            cell.appendChild(document.createElement("i")).textContent = "List is empty";
        }
        list.forEach(function (v, i) {
            var row = document.createElement("tr");

            //Title and link
            var videoCell = row.appendChild(document.createElement("td"));
            var videoLink = videoCell.appendChild(document.createElement("a"));
            videoLink.textContent = v.title;
            videoLink.href = "/Stream/Order/" + v.id;
            videoLink.setAttribute("target", "_blank");

            //Buttons
            var btnCell = row.appendChild(document.createElement("td"));
            var play = btnCell.appendChild(document.createElement("button"));
            btnCell.insertAdjacentHTML("beforeend", "&nbsp;");
            var up = btnCell.appendChild(document.createElement("button"));
            btnCell.insertAdjacentHTML("beforeend", "&nbsp;");
            var down = btnCell.appendChild(document.createElement("button"));
            btnCell.insertAdjacentHTML("beforeend", "&nbsp;");
            var del = btnCell.appendChild(document.createElement("button"));

            play.classList.add("btn", "btn-warning");
            up.classList.add("btn", "btn-success");
            down.classList.add("btn", "btn-success");
            del.classList.add("btn", "btn-danger");

            play.innerHTML = "&#9654;";
            up.innerHTML = "&uarr;";
            down.innerHTML = "&darr;";
            del.textContent = "DEL";

            play.dataset.id = v.id;
            up.dataset.id = down.dataset.id = del.dataset.id = i;
            play.dataset.action = "play";
            up.dataset.action = "up";
            down.dataset.action = "down";
            del.dataset.action = "del";
            up.disabled = i === 0;
            down.disabled = i === list.length - 1;

            //Commit row
            tbl.appendChild(row);
        });
        $(tbl).find("button").on("click", function () {
            var id = +this.dataset.id;
            switch (this.dataset.action) {
                case "play":
                    playFile(this.dataset.id);
                    break;
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
        q("#tbGeneratedUrl").disabled = list.length === 0;
        if (list.length > 0) {
            var type = q("#rbPlayTypeOrder").checked ? "Order" : "Random";
            q("#tbGeneratedUrl").value = location.origin + "/Stream/" + type + "/" + list.map(v => v.id).join(",");
        } else {
            q("#tbGeneratedUrl").value = "Please add at least one video";
        }
    };

    var askIdType = function () {
        var mod = $("#idTypeModal");
        mod.modal("show");
        delete mod[0].dataset.result;
        return new Promise(function (a, r) {
            var hidden = function () {
                mod.off("hidden.bs.modal", hidden);
                a(mod[0].dataset.result);
            };
            mod.on("hidden.bs.modal", hidden);
        });
    };

    var askVideoVsPl = function () {
        var mod = $("#idSelectModal");
        mod.modal("show");
        delete mod[0].dataset.result;
        return new Promise(function (a, r) {
            var hidden = function () {
                mod.off("hidden.bs.modal", hidden);
                a(mod[0].dataset.result);
            };
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

    var getVideo = function (id) {
        for (var i = 0; i < list.length; i++) {
            if (list[i].id === id) {
                return list[i];
            }
        }
        return null;
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
            if (ids.playlist && ids.video) {
                switch (await askVideoVsPl()) {
                    case "video":
                        delete ids.playlist;
                        break;
                    case "playlist":
                        delete ids.video;
                        break;
                    default:
                        return;
                }
            }
            if (ids.playlist) {
                var type = await askIdType();
                if (type === "playlist") {
                    list.push({ title: "playlist " + ids.playlist, id: ids.playlist, pl: true });
                }
                else if (type === "video") {
                    try {
                        list = list.concat(await tools.post("/Api/Info/" + ids.playlist));
                    } catch (e) {
                        alert(e.message);
                    }
                }
                else {
                    //NOOP
                }
            }
            else if (ids.video) {
                var item = getVideo(ids.video);
                if (item) {

                }
                try {
                    list.push(await tools.post("/Api/Info/" + ids.video));
                } catch (e) {
                    alert(e.message);
                }
            }
            renderList();
        }
        console.log(ids);
        field.select();
    };
    //Handle enter key
    q("#tbUrl").addEventListener("keydown", function (e) {
        if (e.keyCode === 13) {
            addId();
        }
    });
    //Add id button
    q("#btnAddId").addEventListener("click", addId);

    //Set up modal events (playlist type)
    var modalTypeBtn = function () {
        q("#idTypeModal").dataset.result = this.dataset.value || "";
    };
    q("#btnIdModalVideoId").addEventListener("click", modalTypeBtn);
    q("#btnIdModalPlId").addEventListener("click", modalTypeBtn);
    q("#idTypeModal .modal-footer button").addEventListener("click", modalTypeBtn);

    //Set up modal events (id type)
    var modalSelectBtn = function () {
        q("#idSelectModal").dataset.result = this.dataset.value || "";
    };
    q("#btnSelectModalVideoId").addEventListener("click", modalSelectBtn);
    q("#btnSelectModalPlId").addEventListener("click", modalSelectBtn);
    q("#idSelectModal .modal-footer button").addEventListener("click", modalSelectBtn);

    //Event for URL type change
    q("#rbPlayTypeOrder").addEventListener("change", renderList);
    //Add label click events because JS is dumb
    q("#rbPlayTypeOrder").parentNode.addEventListener("click", renderList);
    q("#rbPlayTypeRandom").parentNode.addEventListener("click", renderList);

    renderList();
})(tools.q);