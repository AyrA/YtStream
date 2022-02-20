"use strict";

(function (q) {
    var patterns = {
        videos: [
            /(?:youtu\.be\/|youtube(?:-nocookie)?.com\/(?:v\/|e\/|.*u\/\w+\/|embed\/|.*v=))([\w\-]{11})/i
        ],
        playlists: [
            // /youtube.com\/playlist?list=(PL[\w\-]+)/i,
            /\b(PL[\w\-]+)\b/
        ]
    };
    var addId = function () {
    };
    q("#tbUrl").addEventListener("keydown", function (e) {
        if (e.keyCode === 13)
        {
            addId();
        }
    });
    q("#btnAdd").addEventListener("click", addId);
})(document.querySelector.bind(document));