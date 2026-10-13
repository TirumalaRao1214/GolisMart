/**
 * GOLIS Mart — Delivery & Location Module (Vanilla JS)
 * Handles PIN code serviceability verification via MVC endpoints (isolated from external CDNs/direct DB).
 */
(function (window) {
    'use strict';

    var STORAGE_KEY = 'gm_selected_pincode';

    function setSavedPincode(pincode, locality) {
        try {
            var data = { pincode: pincode, locality: locality || '', timestamp: Date.now() };
            localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
        } catch (e) {
            console.warn('LocalStorage write failed:', e);
        }
        updateHeaderLocationText(pincode, locality);
    }

    function getSavedPincode() {
        try {
            var raw = localStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    }

    function updateHeaderLocationText(pincode, locality) {
        var el = document.getElementById('headerDeliveryPincodeText');
        if (el) {
            if (pincode) {
                el.textContent = (locality ? locality + ' — ' : '') + pincode;
                el.classList.add('fw-bold');
            } else {
                el.textContent = 'Enter Pincode';
                el.classList.remove('fw-bold');
            }
        }
    }

    var GMDelivery = {
        /**
         * Check PIN code serviceability
         */
        checkPincode: function (pincode, resultContainerId) {
            var resultContainer = document.getElementById(resultContainerId);
            if (!pincode || pincode.trim().length !== 6) {
                if (resultContainer) {
                    resultContainer.innerHTML = '<div class="alert alert-danger py-2 small mb-0">Please enter a valid 6-digit PIN code.</div>';
                    resultContainer.style.display = 'block';
                }
                return;
            }

            pincode = pincode.trim();

            if (resultContainer) {
                resultContainer.innerHTML = '<div class="text-center py-2 text-muted small"><span class="spinner-border spinner-border-sm me-2"></span>Checking delivery availability…</div>';
                resultContainer.style.display = 'block';
            }

            fetch('/api/delivery/check?pincode=' + encodeURIComponent(pincode), {
                headers: { 'Accept': 'application/json' }
            })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (!resultContainer) return;

                if (data.isServiceable) {
                    var eta = data.estimatedMinutes ? (data.estimatedMinutes + ' mins') : '45 mins';
                    var freeAbove = data.freeDeliveryThreshold ? '₹' + data.freeDeliveryThreshold.toFixed(0) : '₹499';

                    resultContainer.innerHTML =
                        '<div class="alert alert-success py-2 px-3 small mb-0">' +
                            '<div class="d-flex align-items-center justify-content-between mb-1">' +
                                '<strong class="text-success"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" style="width:16px;height:16px;vertical-align:-2px;margin-right:4px;"><polyline points="20 6 9 17 4 12"/></svg>Serviceable in ' + (data.areaName || pincode) + '!</strong>' +
                                '<span class="badge bg-success">⚡ ' + eta + '</span>' +
                            '</div>' +
                            '<div class="text-muted">Min order: ₹' + (data.minimumOrderAmount || 0) + ' • Free delivery above ' + freeAbove + '</div>' +
                            '<div class="mt-2 text-end">' +
                                '<button type="button" class="btn btn-sm btn-success gm-apply-pincode" data-pincode="' + pincode + '" data-area="' + (data.areaName || '') + '">Select this location</button>' +
                            '</div>' +
                        '</div>';

                    // Attach click handler for apply button
                    var applyBtn = resultContainer.querySelector('.gm-apply-pincode');
                    if (applyBtn) {
                        applyBtn.onclick = function () {
                            setSavedPincode(pincode, data.areaName || '');
                            if (window.GMToast) {
                                window.GMToast.success('Delivery location set to ' + (data.areaName ? data.areaName + ' (' + pincode + ')' : pincode));
                            }
                            // Close modal if open
                            var modalEl = document.getElementById('gmLocationModal');
                            if (modalEl && window.bootstrap && window.bootstrap.Modal) {
                                var modal = window.bootstrap.Modal.getInstance(modalEl);
                                if (modal) modal.hide();
                            }
                        };
                    }
                } else {
                    resultContainer.innerHTML =
                        '<div class="alert alert-warning py-2 px-3 small mb-0">' +
                            '<strong>Currently not serviceable</strong><br/>' +
                            (data.message || 'We do not deliver to PIN code ' + pincode + ' yet. We are expanding rapidly!') +
                        '</div>';
                }
            })
            .catch(function (err) {
                console.error('Delivery check error:', err);
                if (resultContainer) {
                    resultContainer.innerHTML = '<div class="alert alert-danger py-2 small mb-0">Failed to check delivery serviceability. Please try again.</div>';
                }
            });
        },

        init: function () {
            // Restore saved location in header
            var saved = getSavedPincode();
            if (saved && saved.pincode) {
                updateHeaderLocationText(saved.pincode, saved.locality);
            }

            // Header modal elements
            var submitBtn = document.getElementById('gmHeaderPincodeSubmit');
            var input = document.getElementById('gmHeaderPincodeInput');
            if (submitBtn && input) {
                submitBtn.onclick = function () {
                    GMDelivery.checkPincode(input.value, 'gmHeaderPincodeResult');
                };
                input.onkeydown = function (e) {
                    if (e.key === 'Enter') {
                        e.preventDefault();
                        submitBtn.click();
                    }
                };
            }

            // Quick PIN pills
            document.querySelectorAll('.gm-quick-pin').forEach(function (btn) {
                btn.onclick = function () {
                    var pin = btn.dataset.pin;
                    if (input) input.value = pin;
                    GMDelivery.checkPincode(pin, 'gmHeaderPincodeResult');
                };
            });
        }
    };

    window.GMDelivery = GMDelivery;

    document.addEventListener('DOMContentLoaded', function () {
        GMDelivery.init();
    });

})(window);
