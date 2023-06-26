"use strict";

(async function (q, qa) {
    const streamKey = q("[data-streamkey]").dataset.streamkey || null;
    const wavSample = "UklGRiUAAABXQVZFZm10IBAAAAABAAIARKwAAIhYAQACABAAZGF0YXQAAAAAAAAA";
    const retryCount = 5;
    const player = document.body.appendChild(document.createElement("audio"));
    const initSources = [];
    const sources = [];
    let ptr = -1;
    let doRandom = false;
    let errorCounter = retryCount;
    const setAutoplayMessage = function (visible) {
        console.log("Autoplay hint set to", visible ? "visible" : "hidden");
        q("#autoplayInfo").style.display = visible ? "block" : "none";
    };

    const canAutoplay = async function () {
        //Use new method if supported by the browser
        if (navigator && typeof (navigator.getAutoplayPolicy) === "function") {
            return navigator.getAutoplayPolicy(player) === "allowed";
        }
        //Use traditional method of just trying to play a file
        try {
            //Convert WAV sample into temporary URL
            const emptyData = new Uint8Array(atob(wavSample).split('').map(v => v.charCodeAt(0)));
            const wavdata = URL.createObjectURL(new Blob([emptyData], { type: "audio/wave" }));
            //Try to play file
            player.src = wavdata;
            await player.play();
            return true;
        }
        catch (e) {
            //Autoplay disabled or wav not supported. Likely the first error.
            console.warn("Autoplay test threw exception:", e);
        }
        finally {
            //Cleanup
            console.log("Revoking URL", player.src);
            URL.revokeObjectURL(player.src);
        }
        return false;
    };

    const videoIndex = function (id) {
        let index = -1;
        sources.forEach(function (v, i, a) {
            if (v.id === id) {
                index = i;
            }
        });
        return index;
    };

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

    const getStreamUrl = function (id) {
        return "/Stream/Send/" + id + "?stream=y&buffer=10" + (streamKey ? "&key=" + streamKey : "");
    };

    const setPlayerSrc = function (id) {
        player.src = getStreamUrl(id);
    };

    const play = function (id) {
        console.log("Loading and playing audio", id);
        setPlayerSrc(id);
        const tableRow = q("[data-videoid='" + id + "']").parentNode.parentNode;
        tableRow.scrollIntoView({ behavior: "smooth" });
        highlightElement(tableRow);
        const promise = player.play();
        navigator.mediaSession.playbackState = "playing";
        setMediaInfo(sources[videoIndex(id)]);
        return promise.then(function () {
            errorCounter = retryCount;
            setAutoplayMessage(false);
        });
    };

    const playPrev = function () {
        const promise = play(prevId());
        return promise;
    };

    const playNext = function () {
        const promise = play(nextId());
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
                const end = player.buffered.end(0) | 0;
                const pos = player.currentTime | 0;
                //Only update time if values are valid
                if (pos >= 0 && pos <= end && end > 0) {
                    navigator.mediaSession.setPositionState({
                        duration: end,
                        playbackRate: player.playbackRate,
                        position: pos,
                    });
                }
            }
        }
        return ret;
    };

    const playbackError = function () {
        if (--errorCounter > 0) {
            console.log("Retrying playback...");
            setTimeout(function () {
                play(sources[ptr].id);
            }, 1000);
        }
        else {
            console.log("Giving up. Going to next file");
            errorCounter = retryCount;
            setTimeout(playNext, 1000);
        }
    };

    const highlightElement = function (e) {
        const current = Array.from(qa("#playlist .current-item"));
        current.forEach(v => v.classList.remove("current-item"));
        if (e instanceof Node) {
            e.classList.add("current-item");
        }
    };

    player.onended = playNext;
    player.ontimeupdate = updateTime;
    player.onerror = playbackError;
    q("#tbVol").addEventListener("input", function (e) {
        e.preventDefault();
        player.volume = this.value / +this.max;
    });
    q("#btnPause").addEventListener("click", function (e) {
        e.preventDefault();
        if (!player.src) {
            ptr = 0;
            playNext();
            setAutoplayMessage(false);
        }
        else {
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
    //Hook event for each individual "play" button, and generate initial ordered playback list to undo the shuffle
    getVideos().forEach(function (v) {
        v.node.addEventListener("click", playButtonHandler);
        initSources.push(v);
    });
    //Media button events
    hookMediaEvents();
    //Initialize list with standard position
    initList();
    //Set media information
    setMediaInfo(sources[0]);

    //Check autoplay and deal with the result
    if (await canAutoplay()) {
        ptr = 0;
        playNext();
    }
    else {
        console.log("Autoplay is disabled");
        setAutoplayMessage(true);
        //Setting the initial file allows the play/pause media control to work
        setPlayerSrc(sources[0].id);
        highlightElement(sources[0].node.parentNode.parentNode);
    }
})(tools.q, tools.qa);
