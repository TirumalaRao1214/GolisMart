// GOLIS Mart — site.js
// Mobile navigation drawer toggle

(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var hamburger  = document.getElementById('mobileMenuBtn');
        var mobileNav  = document.getElementById('mobileNav');
        var backdrop   = document.getElementById('mobileNavBackdrop');
        var closeBtn   = document.getElementById('mobileNavClose');

        if (!hamburger || !mobileNav) return;

        function openNav() {
            mobileNav.classList.add('open');
            document.body.style.overflow = 'hidden';
            hamburger.setAttribute('aria-expanded', 'true');
        }

        function closeNav() {
            mobileNav.classList.remove('open');
            document.body.style.overflow = '';
            hamburger.setAttribute('aria-expanded', 'false');
        }

        hamburger.addEventListener('click', openNav);

        if (backdrop) backdrop.addEventListener('click', closeNav);
        if (closeBtn) closeBtn.addEventListener('click', closeNav);

        // Close on Escape key
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && mobileNav.classList.contains('open')) {
                closeNav();
            }
        });
    });
})();

// ─── Cart quantity adjusters ─────────────────────────────────────────────────

/**
 * Adjusts a cart-page quantity input (inline update form).
 * @param {HTMLElement} btn  - the +/- button clicked
 * @param {number}      delta - +1 or -1
 * @param {number}      productId - product ID (for scoping to the right input)
 */
function adjustCartQty(btn, delta, productId) {
    var input = document.getElementById('qty-' + productId);
    if (!input) return;
    var val = parseInt(input.value, 10) || 1;
    val = Math.max(1, Math.min(20, val + delta));
    input.value = val;
}

/**
 * Adjusts the product detail page quantity input.
 * @param {HTMLElement} btn   - the +/- button clicked
 * @param {number}      delta - +1 or -1
 */
function adjustDetailQty(btn, delta) {
    var input = document.getElementById('detailQty');
    if (!input) return;
    var max = parseInt(input.max, 10) || 20;
    var val = parseInt(input.value, 10) || 1;
    val = Math.max(1, Math.min(max, val + delta));
    input.value = val;
}
