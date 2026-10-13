/**
 * GOLIS Mart — Toast Notification Module (Vanilla JS)
 * Accessible, lightweight toast manager.
 */
(function (window) {
    'use strict';

    var container = null;

    function getContainer() {
        if (!container) {
            container = document.getElementById('gm-toast-container');
            if (!container) {
                container = document.createElement('div');
                container.id = 'gm-toast-container';
                container.className = 'gm-toast-container';
                container.setAttribute('aria-live', 'polite');
                container.setAttribute('aria-atomic', 'true');
                document.body.appendChild(container);
            }
        }
        return container;
    }

    function getIconSvg(type) {
        switch (type) {
            case 'success':
                return '<svg class="gm-toast-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>';
            case 'error':
                return '<svg class="gm-toast-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>';
            case 'warning':
                return '<svg class="gm-toast-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>';
            case 'info':
            default:
                return '<svg class="gm-toast-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>';
        }
    }

    var GMToast = {
        show: function (message, type, duration) {
            if (!message) return;
            type = type || 'info';
            duration = duration !== undefined ? duration : 4000;

            var cont = getContainer();
            var toast = document.createElement('div');
            toast.className = 'gm-toast gm-toast-' + type;
            toast.setAttribute('role', 'alert');

            toast.innerHTML = getIconSvg(type) +
                '<div class="gm-toast-content">' + message + '</div>' +
                '<button type="button" class="gm-toast-close" aria-label="Close notification">&times;</button>';

            var closeBtn = toast.querySelector('.gm-toast-close');
            function removeToast() {
                toast.classList.remove('show');
                toast.classList.add('hide');
                setTimeout(function () {
                    if (toast.parentNode) {
                        toast.parentNode.removeChild(toast);
                    }
                }, 300);
            }

            if (closeBtn) {
                closeBtn.addEventListener('click', removeToast);
            }

            cont.appendChild(toast);

            // Trigger animation
            requestAnimationFrame(function () {
                toast.classList.add('show');
            });

            if (duration > 0) {
                setTimeout(removeToast, duration);
            }

            return toast;
        },

        success: function (msg, duration) {
            return this.show(msg, 'success', duration);
        },

        error: function (msg, duration) {
            return this.show(msg, 'error', duration);
        },

        warning: function (msg, duration) {
            return this.show(msg, 'warning', duration);
        },

        info: function (msg, duration) {
            return this.show(msg, 'info', duration);
        }
    };

    window.GMToast = GMToast;

})(window);
