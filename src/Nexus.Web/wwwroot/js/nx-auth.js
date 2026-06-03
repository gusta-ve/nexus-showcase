// UX pequeno do login:
// 1) detecta Caps Lock no campo senha e mostra aviso
// 2) bloqueia duplo-submit e mostra spinner no botão
// 3) executa em DOMContentLoaded e em enhanced-navigation (Blazor)
(function () {
    function init() {
        var form = document.querySelector('.auth4-form');
        if (!form || form._nxInit) return;
        form._nxInit = true;

        var pwd = form.querySelector('input[name="password"]');
        var capsWarn = form.querySelector('.auth4-caps');
        var submit = form.querySelector('.auth4-submit');

        // ---- Caps Lock detection ----
        function updateCaps(e) {
            if (!capsWarn || typeof e.getModifierState !== 'function') return;
            var on = e.getModifierState('CapsLock');
            capsWarn.hidden = !on;
        }
        if (pwd && capsWarn) {
            pwd.addEventListener('keydown', updateCaps);
            pwd.addEventListener('keyup', updateCaps);
            // Quando perde foco, esconde
            pwd.addEventListener('blur', function () { capsWarn.hidden = true; });
        }

        // ---- Submit lock + spinner ----
        form.addEventListener('submit', function () {
            if (submit) {
                submit.disabled = true;
                submit.classList.add('is-loading');
                // Failsafe: se browser cancelar/voltar, restaura em 8s
                setTimeout(function () {
                    submit.disabled = false;
                    submit.classList.remove('is-loading');
                }, 8000);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
    document.addEventListener('enhancedload', init);

    // Eye toggle via event delegation — funciona independente do timing do Blazor
    document.addEventListener('click', function (e) {
        var btn = e.target.closest('.auth4-pwd-toggle');
        if (!btn) return;
        e.preventDefault();
        var input = document.getElementById('login-password');
        if (!input) return;
        var showing = input.type === 'text';
        input.type = showing ? 'password' : 'text';
        var on  = btn.querySelector('.auth4-eye-on');
        var off = btn.querySelector('.auth4-eye-off');
        if (on)  on.style.display  = showing ? 'none'  : 'block';
        if (off) off.style.display = showing ? 'block' : 'none';
    });
})();
