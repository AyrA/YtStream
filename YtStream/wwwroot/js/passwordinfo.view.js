"use strict";

(function (q, qa) {
    var rules = JSON.parse(q("[data-passwordrules]").dataset.passwordrules);
    var setStatus = function (status, element) {
        if (element) {
            element.textContent = status ? "\u2714\uFE0F" : "\u274C";
        }
    };
    var checkPassword = function (pw1, pw2, btn) {
        //Regex checks
        var checks = {
            upper: /[A-Z]/.test(pw1),
            lower: /[a-z]/.test(pw1),
            digits: /\d/.test(pw1),
            symbols: /[^A-Za-z\d]/.test(pw1)
        };
        //Additional checks
        checks.count = Object.keys(checks).filter(v => checks[v]).length >= rules.RuleCount;
        checks.length = pw1.length >= rules.MinimumLength;
        //Do not consider empty passwords to be matching
        checks.match = pw1.length > 0 && pw1 === pw2;

        setStatus(checks.length, q(".passwd-minlength span"));
        setStatus(checks.upper, q(".passwd-uppercase span"));
        setStatus(checks.lower, q(".passwd-lowercase span"));
        setStatus(checks.digits, q(".passwd-digits span"));
        setStatus(checks.symbols, q(".passwd-symbols span"));
        setStatus(checks.count, q(".passwd-rulecount span"));
        setStatus(checks.match, q(".passwd-match span"));

        var pass = checks.count && checks.length && checks.match;
        pass = pass && (checks.upper || !rules.Uppercase);
        pass = pass && (checks.lower || !rules.Lowercase);
        pass = pass && (checks.digits || !rules.Digits);
        pass = pass && (checks.symbols || !rules.Symbols);

        btn.disabled = !pass;
    };
    q(".passwd-complexity").style.display = "block";
    var e1 = q(".password-check");
    var e2 = q(".password-match");
    var btn = q(".password-validate");
    if (e1) {
        e1.addEventListener("input", function () {
            checkPassword(e1.value, e2 ? e2.value : null, btn);
        });
        e1.addEventListener("change", function () {
            checkPassword(e1.value, e2 ? e2.value : null, btn);
        });
        checkPassword(e1.value, e2 ? e2.value : null, btn);
    }
})(document.querySelector.bind(document), document.querySelectorAll.bind(document));
