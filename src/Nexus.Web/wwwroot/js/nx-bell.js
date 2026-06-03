// Sino de notificações — abre/fecha INSTANT no DOM, C# atualiza state em paralelo.
// O round-trip do Blazor Server (SignalR) costuma levar 100-500ms, e isso atrasa
// uma UI que precisa ser instantânea.
window.nxBell = (function () {
    const outsideHandlers = {};

    // Liga listener no botão pra trocar visibilidade do painel ANTES do Blazor processar.
    function attach(id) {
        const root = document.querySelector(`[data-nx-bell-id="${id}"]`);
        if (!root) return;
        const btn = root.querySelector('.nx-bell-btn');
        const panel = root.querySelector('.nx-bell-panel');
        if (!btn || !panel || btn._nxBound) return;
        btn._nxBound = true;
        btn.addEventListener('click', () => {
            const open = panel.getAttribute('data-open') === 'true';
            panel.setAttribute('data-open', open ? 'false' : 'true');
        }, true);
    }

    // Click fora — dispara OnOutsideClick no C# pra fechar/sincronizar state.
    function register(id, dotNetRef) {
        // setTimeout 0 evita capturar o MESMO click que abriu o painel
        setTimeout(() => {
            const handler = (e) => {
                const el = document.querySelector(`[data-nx-bell-id="${id}"]`);
                if (el && !el.contains(e.target)) {
                    const panel = el.querySelector('.nx-bell-panel');
                    if (panel) panel.setAttribute('data-open', 'false');
                    document.removeEventListener('click', handler, true);
                    delete outsideHandlers[id];
                    dotNetRef.invokeMethodAsync('OnOutsideClick').catch(() => { /* já disposed */ });
                }
            };
            outsideHandlers[id] = handler;
            document.addEventListener('click', handler, true);
        }, 0);
    }

    function unregister(id) {
        const h = outsideHandlers[id];
        if (h) {
            document.removeEventListener('click', h, true);
            delete outsideHandlers[id];
        }
    }

    return { attach, register, unregister };
})();
