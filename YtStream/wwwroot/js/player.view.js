"use strict";

(function (q, qa) {
    const player = document.body.appendChild(document.createElement("audio"));
    const initSources = [];
    const sources = [];
    let ptr = -1;
    let doRandom = false;

    const setAutoplayMessage = function (visible) {
        q("#autoplayInfo").style.display = visible ? "block" : "none";
    };

    const videoIndex = function (id) {
        let index = -1;
        sources.forEach(function (v, i, a) {
            if (v.id === id) {
                index = i;
            }
        });
        return index;
    }
    const getVideos = function () {
        return Array.from(qa("[data-videoid]")).map(function (v) {
            return {
                thumb: v.dataset.videothumb,
                id: v.dataset.videoid,
                title: v.dataset.videotitle,
                channel: v.dataset.videochannel,
                node: v
            };
        });
    };

    const hookMediaEvents = function () {
        if (hookMediaEvents.hooked === true) {
            console.warn("Attempted to hook media event multiple times");
            return;
        }
        hookMediaEvents.hooked = true;
        if ("mediaSession" in navigator) {
            /* No need to hook play and pause. The System does it for us
            navigator.mediaSession.setActionHandler("play", () => {
                console.debug("Media event: play");
                if (player.paused) {
                    player.play();
                }
                setPlayState();
            });
            navigator.mediaSession.setActionHandler("pause", () => {
                console.debug("Media event: pause");
                if (!player.paused) {
                    player.pause();
                }
                setPlayState();
            });
            navigator.mediaSession.setActionHandler("stop", () => {
                console.debug("Media event: stop");
                if (!player.paused) {
                    player.pause();
                }
            });
            //*/
            navigator.mediaSession.setActionHandler("previoustrack", () => {
                console.debug("Media event: prev");
                playPrev();
                setPlayState();
            });
            navigator.mediaSession.setActionHandler("nexttrack", () => {
                console.debug("Media event: next");
                playNext();
                setPlayState();
            });
        }
        else {
            console.info("mediaSession object not available in browser. Cannot hook key events");
        }
    };

    const setMediaInfo = function (video) {
        if ("mediaSession" in navigator) {
            const data = {
                title: video.title,
                artist: video.channel || "",
                album: q("[data-albumtitle]").dataset.albumtitle,
                artwork: [
                    {
                        src: video.thumb,
                        //sizes: "96x96",
                        type: "image/jpeg",
                    }
                ],
            };
            navigator.mediaSession.metadata = new MediaMetadata(data);
        }
    };

    const setPlayState = function () {
        if ("mediaSession" in navigator) {
            navigator.mediaSession.playbackState = player.paused ? "playing" : "paused";
        }
    };

    const sortVideos = function () {
        const videos = {};
        getVideos().forEach(v => videos[v.id] = v.node.parentNode.parentNode);
        sources.forEach(v => videos[v.id].parentNode.appendChild(videos[v.id]));
    };

    const randomize = function () {
        console.log("Shuffling playlist");
        sources.splice(0, sources.length);
        const videos = getVideos();
        while (videos.length) {
            sources.push(videos.splice(Math.random() * videos.length | 0, 1)[0]);
        }
        sortVideos();
    };

    const unRandomize = function () {
        console.log("Sorting playlist");
        sources.splice(0, sources.length);
        initSources.forEach(v => sources.push(v));
        sortVideos();
    };

    const initList = function () {
        if (doRandom) {
            randomize();
        }
        else {
            unRandomize();
        }
        ptr = 0;
    };

    const prevId = function () {
        ptr -= 2;
        if (ptr < 0) {
            ptr = sources.length - 1;
        }
        return sources[ptr++].id;
    };

    const nextId = function () {
        if (ptr < 0 || ptr >= sources.length) {
            console.log("Resetting playlist");
            initList();
        }
        return sources[ptr++].id;
    };

    const play = function (id) {
        console.log("Loading and playing audio", id);
        player.src = "/Stream/Send/" + id + "?stream=y&buffer=10";
        q("[data-videoid='" + id + "']").parentNode.parentNode.scrollIntoView({ behavior: "smooth" });
        const promise = player.play();
        navigator.mediaSession.playbackState = "playing";
        setMediaInfo(sources[videoIndex(id)]);
        return promise.then(() => setAutoplayMessage(false));
    };

    const playPrev = function () {
        console.debug("Before: ptr is", ptr);
        const promise = play(prevId());
        console.debug("After: ptr is", ptr);
        return promise;
    };

    const playNext = function () {
        console.debug("Before: ptr is", ptr);
        const promise = play(nextId());
        console.debug("After: ptr is", ptr);
        return promise;
    };

    const playButtonHandler = function (e) {
        e.preventDefault();
        play(this.dataset.videoid);
        ptr = videoIndex(this.dataset.videoid) + 1;
    };

    const updateTime = function () {
        const n2 = v => v < 10 ? "0" + v : v.toString();
        const t = player.currentTime | 0;
        const h = (t / 3600 | 0);
        const m = (t / 60 | 0) % 60;
        const s = t % 60;
        let ret = "";
        if (h > 0) {
            ret += n2(h) + ":";
        }
        ret += n2(m) + ":" + n2(s);
        q("#lblTime").textContent = ret;

        if ('setPositionState' in navigator.mediaSession) {
            if (player.buffered.length > 0) {
                navigator.mediaSession.setPositionState({
                    duration: player.buffered.end(0),
                    playbackRate: player.playbackRate,
                    position: player.currentTime,
                });
            }
        }
        return ret;
    };

    player.onended = playNext;
    player.ontimeupdate = updateTime;
    q("#tbVol").addEventListener("input", function (e) {
        e.preventDefault();
        player.volume = this.value / 10000;
    });
    q("#btnPause").addEventListener("click", function (e) {
        e.preventDefault();
        if (!player.src) {
            ptr = 0;
            playNext();
        }
        else {
            setAutoplayMessage(false);
            player.paused ? player.play() : player.pause();
        }
        setPlayState();
    });
    q("#btnPrev").addEventListener("click", function (e) {
        e.preventDefault();
        playPrev();
    });
    q("#btnNext").addEventListener("click", function (e) {
        e.preventDefault();
        playNext();
    });

    q("#btnShuffle").addEventListener("click", function (e) {
        e.preventDefault();
        doRandom = !doRandom;
        this.classList.remove("btn-outline-primary", "btn-success");
        this.classList.add(doRandom ? "btn-success" : "btn-outline-primary");
        if (doRandom) {
            randomize();
        }
        else {
            unRandomize();
        }
        ptr = 0;
        playNext();
    });
    //Hook event, and save initial position to undo the shuffle
    getVideos().forEach(function (v) {
        v.node.addEventListener("click", playButtonHandler);
        initSources.push(v);
    });

    hookMediaEvents();
    initList();
    setMediaInfo(sources[0]);
    if (navigator.getAutoplayPolicy) {
        if (navigator.getAutoplayPolicy(player) === "allowed") {
            ptr = 0;
            playNext();
        }
        else {
            console.warn("Autoplay is disabled");
            setAutoplayMessage(true);
        }
    }
    else {
        ptr = 0;
        playNext().catch(function (ex) {
            setAutoplayMessage(true);
            console.warn("Autoplay is disabled", ex);
        });
    }
})(tools.q, tools.qa);
