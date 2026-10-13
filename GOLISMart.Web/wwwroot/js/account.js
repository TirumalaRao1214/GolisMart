/**
 * GOLIS Mart — Account & Address Module (Vanilla JS)
 * Handles AJAX login, registration, profile updates, and address operations.
 */
(function (window) {
    'use strict';

    function getAntiForgeryToken(form) {
        var tokenInput = form ? form.querySelector('input[name="__RequestVerificationToken"]') : document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    var GMAccount = {
        /**
         * Delete address asynchronously
         */
        deleteAddress: function (addressId, cardElement) {
            if (!confirm('Are you sure you want to delete this address?')) return;

            var formData = new FormData();
            var token = getAntiForgeryToken();
            if (token) formData.append('__RequestVerificationToken', token);

            if (cardElement) {
                cardElement.style.opacity = '0.4';
                cardElement.style.pointerEvents = 'none';
            }

            fetch('/account/addresses/' + addressId + '/delete', {
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
                    if (cardElement) {
                        cardElement.style.transition = 'all 0.3s ease';
                        cardElement.style.transform = 'scale(0.9)';
                        cardElement.style.opacity = '0';
                        setTimeout(function () {
                            if (cardElement.parentNode) {
                                cardElement.parentNode.removeChild(cardElement);
                            }
                        }, 300);
                    }
                    if (window.GMToast) {
                        window.GMToast.success(data.message || 'Address removed.');
                    }
                } else {
                    if (cardElement) {
                        cardElement.style.opacity = '1';
                        cardElement.style.pointerEvents = '';
                    }
                    if (window.GMToast) {
                        window.GMToast.error(data.message || 'Could not delete address.');
                    }
                }
            })
            .catch(function (err) {
                console.error('Delete address error:', err);
                if (cardElement) {
                    cardElement.style.opacity = '1';
                    cardElement.style.pointerEvents = '';
                }
                if (window.GMToast) {
                    window.GMToast.error('Network error deleting address.');
                }
            });
        },

        /**
         * Set default address asynchronously
         */
        setDefaultAddress: function (addressId) {
            var formData = new FormData();
            var token = getAntiForgeryToken();
            if (token) formData.append('__RequestVerificationToken', token);

            fetch('/account/addresses/' + addressId + '/set-default', {
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
                    if (window.GMToast) {
                        window.GMToast.success(data.message || 'Default address updated.');
                    }
                    // Refresh address page content if on addresses page
                    if (window.GMSPA) {
                        window.GMSPA.navigate(window.location.href, false);
                    } else {
                        window.location.reload();
                    }
                } else {
                    if (window.GMToast) {
                        window.GMToast.error(data.message || 'Could not update default address.');
                    }
                }
            })
            .catch(function (err) {
                console.error('Set default error:', err);
                if (window.GMToast) {
                    window.GMToast.error('Network error updating default address.');
                }
            });
        },

        /**
         * Handle AJAX auth forms (Login, Register, Profile, Password)
         */
        handleAuthFormSubmit: function (form, submitBtn) {
            var action = form.getAttribute('action') || window.location.pathname;
            var formData = new FormData(form);

            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.classList.add('gm-btn-adding');
            }

            // Remove existing alert error in form if any
            var existingAlert = form.querySelector('.gm-form-alert');
            if (existingAlert) existingAlert.remove();

            fetch(action, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            })
            .then(function (res) {
                return res.json().then(function (data) {
                    return { ok: res.ok, status: res.status, data: data };
                });
            })
            .then(function (result) {
                var data = result.data;
                if (result.ok && data.success) {
                    if (window.GMToast) {
                        window.GMToast.success(data.message || 'Success!');
                    }
                    if (data.redirectUrl) {
                        // Full page navigation ensures the server renders the complete layout and auth header
                        window.location.href = data.redirectUrl;
                    } else {
                        window.location.reload();
                    }
                } else {
                    var errorMsg = data.message || 'An error occurred. Please check your inputs.';
                    if (data.errors) {
                        var errList = [];
                        for (var k in data.errors) {
                            if (data.errors.hasOwnProperty(k)) {
                                errList.push(data.errors[k].join(', '));
                            }
                        }
                        if (errList.length) errorMsg = errList.join('<br/>');
                    }

                    var alertDiv = document.createElement('div');
                    alertDiv.className = 'alert alert-danger py-2 small gm-form-alert mb-3';
                    alertDiv.innerHTML = errorMsg;
                    form.insertBefore(alertDiv, form.firstChild);

                    if (window.GMToast) {
                        window.GMToast.error(data.message || 'Operation failed.');
                    }
                }
            })
            .catch(function (err) {
                console.error('Form submit error:', err);
                var alertDiv = document.createElement('div');
                alertDiv.className = 'alert alert-danger py-2 small gm-form-alert mb-3';
                alertDiv.textContent = 'Network or server error. Please try again.';
                form.insertBefore(alertDiv, form.firstChild);
            })
            .finally(function () {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.classList.remove('gm-btn-adding');
                }
            });
        },

        init: function () {
            // Address page delegated actions
            document.body.addEventListener('click', function (e) {
                var delBtn = e.target.closest('[data-delete-address-id]');
                if (delBtn) {
                    e.preventDefault();
                    var addrId = parseInt(delBtn.dataset.deleteAddressId, 10);
                    var card = delBtn.closest('.col-12, .card, .gm-address-card-wrap');
                    GMAccount.deleteAddress(addrId, card);
                    return;
                }

                var defBtn = e.target.closest('[data-default-address-id]');
                if (defBtn) {
                    e.preventDefault();
                    var defId = parseInt(defBtn.dataset.defaultAddressId, 10);
                    GMAccount.setDefaultAddress(defId);
                    return;
                }
            });

            // Intercept async auth forms marked with data-ajax-form
            document.body.addEventListener('submit', function (e) {
                var form = e.target.closest('form[data-ajax-form]');
                if (form) {
                    e.preventDefault();
                    var btn = form.querySelector('button[type="submit"]');
                    GMAccount.handleAuthFormSubmit(form, btn);
                }
            });
        }
    };

    window.GMAccount = GMAccount;

    document.addEventListener('DOMContentLoaded', function () {
        GMAccount.init();
    });

})(window);
