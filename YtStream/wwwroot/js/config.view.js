"use strict";

(function (q) {
    var boolFields = {
        cache: q("#UseCache"),
        sblock: q("#UseSponsorBlock")
    };
    var updateFields = function () {
        q("#CachePath").disabled =
            q("#CacheMp3Lifetime").disabled =
            q("#CacheSBlockLifetime").disabled =
            !boolFields.cache.checked;
        q("#SponsorBlockServer").disabled = !boolFields.sblock.checked;
    };
    boolFields.cache.addEventListener("change", updateFields);
    boolFields.sblock.addEventListener("change", updateFields);
    updateFields();
    q("#YtApiKey").addEventListener("focus", function () {
        this.type = "text";
    });
    q("#YtApiKey").addEventListener("blur", function () {
        this.type = "password";
    });
})(document.querySelector.bind(document));