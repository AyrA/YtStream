"use strict";

(function (links) {
    var player = new Audio();
    var playFile = function (e) {
        var filename = decodeURIComponent(this.href.split('#')[1]);
        var current = player.dataset.currentFile;
        if (filename) {
            e.preventDefault();
            if (filename === current) {
                if (player.paused) {
                    player.currentTime = 0;
                    player.play();
                }
                else {
                    player.pause();
                }
                return;
            }
            player.src = "/Ads/Play/" + encodeURIComponent(filename);
            player.dataset.currentFile = filename;
            player.play();
        }
    };
    links.forEach(function (link) {
        link.addEventListener("click", playFile);
    });
})(Array.from(document.querySelectorAll("a.play-link")));