// Rotator do hero: troca a frase ativa a cada N ms com fade.
// Pure JS, sem framework, executa após DOMContentLoaded + Blazor enhanced nav.
(function () {
    function initRotator() {
        var rotators = document.querySelectorAll('.lp4-rotator');
        rotators.forEach(function (rotator) {
            if (rotator._initialized) return;
            rotator._initialized = true;

            var items = rotator.querySelectorAll('.lp4-rotator-item');
            if (items.length < 2) return;

            var i = 0;
            setInterval(function () {
                items[i].classList.remove('is-active');
                i = (i + 1) % items.length;
                items[i].classList.add('is-active');
            }, 2600);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initRotator);
    } else {
        initRotator();
    }
    document.addEventListener('enhancedload', initRotator);
})();
