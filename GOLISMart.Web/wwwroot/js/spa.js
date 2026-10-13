/**
 * GOLIS Mart — SPA Navigation Engine (Vanilla JS + History API)
 * Provides seamless client-side page transitions across ASP.NET Core MVC Razor views.
 * Progressive enhancement fallback preserved for JavaScript disabled / errors.
 */
(function (window) {
    'use strict';

    var progressBar = null;
    var progressInterval = null;

    function getProgressBar() {
        if (!progressBar) {
            progressBar = document.getElementById('gm-progress-bar');
        }
        return progressBar;
    }

    function startProgress() {
        var bar = getProgressBar();
        if (!bar) return;

        clearInterval(progressInterval);
        bar.style.width = '15%';
        bar.classList.add('gm-loading');

        var progress = 15;
        progressInterval = setInterval(function () {
            if (progress < 85) {
                progress += Math.random() * 15;
                bar.style.width = Math.min(progress, 85) + '%';
            }
        }, 150);
    }

    function completeProgress() {
        var bar = getProgressBar();
        if (!bar) return;

        clearInterval(progressInterval);
        bar.style.width = '100%';
        setTimeout(function () {
            bar.classList.remove('gm-loading');
            setTimeout(function () {
                bar.style.width = '0%';
            }, 250);
        }, 150);
    }

    function isInternalLink(anchor) {
        if (!anchor || !anchor.href) return false;
        if (anchor.target && anchor.target !== '_self' && anchor.target !== '') return false;
        if (anchor.hasAttribute('download')) return false;
        if (anchor.getAttribute('data-no-spa') === 'true') return false;
        if (anchor.getAttribute('href').startsWith('#') || anchor.getAttribute('href').startsWith('javascript:')) return false;

        var origin = window.location.origin;
        var anchorOrigin = anchor.origin || (anchor.protocol + '//' + anchor.host);
        if (origin !== anchorOrigin) return false;

        var path = new URL(anchor.href, window.location.origin).pathname.toLowerCase();

        // Dedicated layouts or auth state mutating routes must never be SPA-intercepted
        if (path.startsWith('/admin')) return false;
        if (path.startsWith('/account/login') || path.startsWith('/account/logout') || path.startsWith('/account/register')) return false;

        return true;
    }

    function updateNavActiveStates(url) {
        var path = new URL(url, window.location.origin).pathname.toLowerCase();

        // Update category bar active states
        document.querySelectorAll('.gm-catbar-link').forEach(function (link) {
            var linkPath = new URL(link.href, window.location.origin).pathname.toLowerCase();
            if (linkPath === path || (linkPath !== '/' && linkPath !== '/catalog' && path.startsWith(linkPath))) {
                link.classList.add('active');
            } else if (linkPath === '/catalog' && (path === '/catalog' || path === '/catalog/index')) {
                link.classList.add('active');
            } else {
                link.classList.remove('active');
            }
        });
    }

    function reinitializePage() {
        // Re-run cart handlers
        if (window.GMCart && typeof window.GMCart.initCartPageEvents === 'function') {
            window.GMCart.initCartPageEvents();
        }
        // Re-run catalog handlers
        if (window.GMCatalog && typeof window.GMCatalog.initCatalogEvents === 'function') {
            window.GMCatalog.initCatalogEvents();
        }
        // Re-run delivery handlers
        if (window.GMDelivery && typeof window.GMDelivery.init === 'function') {
            window.GMDelivery.init();
        }
        // Re-run location picker if present
        if (window.GMLocationPicker && typeof window.GMLocationPicker.init === 'function') {
            window.GMLocationPicker.init();
        }
        // Close mobile nav drawer if open
        var mobileNav = document.getElementById('mobileNav');
        if (mobileNav && mobileNav.classList.contains('open')) {
            mobileNav.classList.remove('open');
            document.body.style.overflow = '';
        }
    }

    var GMSPA = {
        /**
         * Navigate to URL asynchronously
         */
        navigate: function (url, pushHistory) {
            if (pushHistory === undefined) pushHistory = true;

            var targetPath = new URL(url, window.location.origin).pathname.toLowerCase();
            if (targetPath.startsWith('/admin') || targetPath.startsWith('/account/login') || targetPath.startsWith('/account/logout') || targetPath.startsWith('/account/register')) {
                window.location.href = url;
                return;
            }

            startProgress();

            fetch(url, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'X-SPA-Request': 'true',
                    'Accept': 'text/html'
                }
            })
            .then(function (response) {
                if (response.redirected) {
                    var finalPath = new URL(response.url, window.location.origin).pathname.toLowerCase();
                    if (finalPath.startsWith('/admin') || finalPath.startsWith('/account/login') || finalPath.startsWith('/account/logout')) {
                        window.location.href = response.url;
                        return null;
                    }

                    if (pushHistory) {
                        window.history.pushState({ spa: true, url: response.url }, '', response.url);
                    }
                }

                if (!response.ok) {
                    // Fall back to full reload if server returns error or auth redirect
                    window.location.href = url;
                    return null;
                }
                return response.text();
            })
            .then(function (html) {
                if (!html) return;

                var parser = new DOMParser();
                var doc = parser.parseFromString(html, 'text/html');

                // Check for layout mismatch (e.g. redirected to an admin page or non-SPA view)
                var currentIsAdmin = !!document.getElementById('admSidebar');
                var newIsAdmin = !!doc.getElementById('admSidebar');
                if (currentIsAdmin !== newIsAdmin || newIsAdmin) {
                    window.location.href = url;
                    return;
                }

                // Update document title
                if (doc.title) {
                    document.title = doc.title;
                }

                // Update main content
                var newMain = doc.getElementById('main-content') || doc.querySelector('main');
                var currentMain = document.getElementById('main-content') || document.querySelector('main');

                if (newMain && currentMain) {
                    currentMain.innerHTML = newMain.innerHTML;
                    currentMain.classList.add('gm-fade-in');
                    setTimeout(function () {
                        currentMain.classList.remove('gm-fade-in');
                    }, 300);
                } else if (newMain) {
                    document.body.appendChild(newMain);
                }

                // Scroll to top
                window.scrollTo({ top: 0, behavior: 'instant' });

                if (pushHistory) {
                    window.history.pushState({ spa: true, url: url }, '', url);
                }

                updateNavActiveStates(url);
                reinitializePage();
                completeProgress();
            })
            .catch(function (err) {
                console.error('SPA navigation failed, falling back:', err);
                completeProgress();
                window.location.href = url;
            });
        },

        init: function () {
            // Global click interceptor for internal links
            document.body.addEventListener('click', function (e) {
                // Ignore modified clicks (Ctrl/Cmd/Shift/Alt/middle click)
                if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey || e.button !== 0) return;

                var link = e.target.closest('a');
                if (link && isInternalLink(link)) {
                    // Don't intercept dropdown toggles or checkout buttons handled separately
                    if (link.getAttribute('data-bs-toggle') || link.getAttribute('data-no-spa')) return;

                    var url = link.href;
                    // Don't intercept if same exact URL with hash
                    if (url === window.location.href && link.getAttribute('href').startsWith('#')) return;

                    e.preventDefault();
                    GMSPA.navigate(url, true);
                }
            });

            // Handle browser Back / Forward buttons
            window.addEventListener('popstate', function (e) {
                var targetUrl = window.location.href;
                GMSPA.navigate(targetUrl, false);
            });
        }
    };

    window.GMSPA = GMSPA;

    document.addEventListener('DOMContentLoaded', function () {
        GMSPA.init();
    });

})(window);
