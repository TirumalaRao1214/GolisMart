/**
 * GOLIS Mart — Shopping Cart Module (Vanilla JS)
 * Handles AJAX add-to-cart, quantity changes, item removal, badge updates, and cart page refreshes.
 */
(function (window) {
    'use strict';

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function updateCartBadges(count) {
        var numCount = parseInt(count, 10) || 0;
        var badges = document.querySelectorAll('.gm-cart-badge, #gmCartBadge');
        badges.forEach(function (badge) {
            badge.textContent = numCount;
            if (numCount > 0) {
                badge.classList.add('gm-cart-badge--has-items');
            } else {
                badge.classList.remove('gm-cart-badge--has-items');
            }
            badge.classList.remove('gm-cart-badge-bounce');
            void badge.offsetWidth; // Trigger reflow
            badge.classList.add('gm-cart-badge-bounce');
        });
    }

    var GMCart = {
        /**
         * Add product to cart asynchronously
         */
        addToCart: function (productId, quantity, btnElement) {
            quantity = quantity || 1;
            if (btnElement) {
                btnElement.classList.add('gm-btn-adding');
                btnElement.disabled = true;
            }

            var formData = new FormData();
            formData.append('productId', productId);
            formData.append('quantity', quantity);
            var token = getAntiForgeryToken();
            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            return fetch('/cart/add', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            })
            .then(function (response) {
                return response.json();
            })
            .then(function (data) {
                if (data.success) {
                    updateCartBadges(data.totalItems || data.totalQuantity || data.cart?.totalItems || 0);
                    if (window.GMToast) {
                        window.GMToast.success(data.message || 'Item added to cart!');
                    }
                } else {
                    if (window.GMToast) {
                        window.GMToast.error(data.message || 'Could not add item to cart.');
                    }
                }
                return data;
            })
            .catch(function (err) {
                console.error('Cart add error:', err);
                if (window.GMToast) {
                    window.GMToast.error('Network error. Could not add item.');
                }
            })
            .finally(function () {
                if (btnElement) {
                    btnElement.classList.remove('gm-btn-adding');
                    btnElement.disabled = false;
                }
            });
        },

        /**
         * Update cart item quantity
         */
        updateQuantity: function (productId, quantity, containerElement) {
            quantity = parseInt(quantity, 10);
            if (isNaN(quantity) || quantity < 1) quantity = 1;

            var formData = new FormData();
            formData.append('productId', productId);
            formData.append('quantity', quantity);
            var token = getAntiForgeryToken();
            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            return fetch('/cart/update', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (data.success) {
                    updateCartBadges(data.totalItems !== undefined ? data.totalItems : data.cart?.totalItems || 0);
                    // Update totals on page if cart content exists
                    GMCart.refreshCartView();
                    if (window.GMToast && data.message) {
                        window.GMToast.info(data.message);
                    }
                } else {
                    if (window.GMToast) {
                        window.GMToast.error(data.message || 'Could not update quantity.');
                    }
                }
                return data;
            })
            .catch(function (err) {
                console.error('Cart update error:', err);
                if (window.GMToast) {
                    window.GMToast.error('Failed to update quantity.');
                }
            });
        },

        /**
         * Remove item from cart
         */
        removeItem: function (productId, rowElement) {
            var formData = new FormData();
            formData.append('productId', productId);
            var token = getAntiForgeryToken();
            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            if (rowElement) {
                rowElement.style.opacity = '0.5';
                rowElement.style.pointerEvents = 'none';
            }

            return fetch('/cart/remove', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (data.success) {
                    updateCartBadges(data.totalItems !== undefined ? data.totalItems : 0);
                    if (window.GMToast) {
                        window.GMToast.info(data.message || 'Item removed.');
                    }
                    GMCart.refreshCartView();
                } else {
                    if (rowElement) {
                        rowElement.style.opacity = '1';
                        rowElement.style.pointerEvents = '';
                    }
                    if (window.GMToast) {
                        window.GMToast.error(data.message || 'Could not remove item.');
                    }
                }
                return data;
            })
            .catch(function (err) {
                console.error('Cart remove error:', err);
                if (rowElement) {
                    rowElement.style.opacity = '1';
                    rowElement.style.pointerEvents = '';
                }
            });
        },

        /**
         * Refreshes the cart container HTML if the current page is /cart
         */
        refreshCartView: function () {
            var cartContainer = document.getElementById('cart-content-container');
            if (!cartContainer) return;

            fetch('/cart?partial=true', {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html'
                }
            })
            .then(function (res) { return res.text(); })
            .then(function (html) {
                cartContainer.innerHTML = html;
                cartContainer.classList.add('gm-fade-in');
                setTimeout(function () {
                    cartContainer.classList.remove('gm-fade-in');
                }, 300);
                GMCart.initCartPageEvents();
            })
            .catch(function (err) {
                console.error('Error refreshing cart view:', err);
            });
        },

        /**
         * Initialize event delegation for cart buttons
         */
        init: function () {
            // Global delegated click for add-to-cart buttons
            document.body.addEventListener('click', function (e) {
                var btn = e.target.closest('[data-add-to-cart]');
                if (btn) {
                    e.preventDefault();
                    var productId = parseInt(btn.dataset.addToCart, 10);
                    var qtyInput = document.getElementById('detailQty') || document.querySelector('[data-qty-for="' + productId + '"]');
                    var qty = qtyInput ? parseInt(qtyInput.value, 10) || 1 : 1;
                    GMCart.addToCart(productId, qty, btn);
                }
            });

            // Intercept standard add-to-cart forms
            document.body.addEventListener('submit', function (e) {
                var form = e.target.closest('form');
                if (form && form.getAttribute('action') && form.getAttribute('action').toLowerCase().indexOf('/cart/add') !== -1) {
                    e.preventDefault();
                    var pIdInput = form.querySelector('[name="productId"]');
                    var qInput = form.querySelector('[name="quantity"]');
                    var btn = form.querySelector('button[type="submit"]');
                    if (pIdInput) {
                        var pId = parseInt(pIdInput.value, 10);
                        var q = qInput ? parseInt(qInput.value, 10) || 1 : 1;
                        GMCart.addToCart(pId, q, btn);
                    }
                }
            });

            GMCart.initCartPageEvents();
        },

        initCartPageEvents: function () {
            var cartContainer = document.getElementById('cart-content-container');
            if (!cartContainer) return;

            // Handle qty buttons in cart table
            cartContainer.querySelectorAll('.gm-qty-btn-minus, .gm-qty-btn-plus').forEach(function (btn) {
                btn.onclick = function (e) {
                    e.preventDefault();
                    var pId = parseInt(btn.dataset.productId, 10);
                    var delta = btn.classList.contains('gm-qty-btn-plus') ? 1 : -1;
                    var input = document.getElementById('qty-' + pId);
                    if (input) {
                        var current = parseInt(input.value, 10) || 1;
                        var next = Math.max(1, Math.min(20, current + delta));
                        if (next !== current) {
                            input.value = next;
                            GMCart.updateQuantity(pId, next, cartContainer);
                        }
                    }
                };
            });

            // Handle remove buttons
            cartContainer.querySelectorAll('[data-remove-product-id]').forEach(function (btn) {
                btn.onclick = function (e) {
                    e.preventDefault();
                    var pId = parseInt(btn.dataset.removeProductId, 10);
                    var row = btn.closest('.gm-cart-item');
                    GMCart.removeItem(pId, row);
                };
            });
        }
    };

    window.GMCart = GMCart;

    document.addEventListener('DOMContentLoaded', function () {
        GMCart.init();
    });

})(window);
