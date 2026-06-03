// Dropdown do usuário — abre/fecha sem round-trip ao servidor
(function () {
    window.nxUserDd = {
        toggle: function (id) {
            var root = document.getElementById(id);
            if (!root) return;
            var panel = root.querySelector('.nx-user-dd-panel');
            if (!panel) return;
            var opening = !panel.classList.contains('open');
            // Fecha todos
            document.querySelectorAll('.nx-user-dd-panel.open').forEach(function (p) {
                p.classList.remove('open');
            });
            if (opening) panel.classList.add('open');
        }
    };

    // Fecha ao clicar fora
    document.addEventListener('click', function (e) {
        if (!e.target.closest('.nx-user-dd')) {
            document.querySelectorAll('.nx-user-dd-panel.open').forEach(function (p) {
                p.classList.remove('open');
            });
        }
    });
})();
