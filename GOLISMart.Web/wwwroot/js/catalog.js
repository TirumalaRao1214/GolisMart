/**
 * GOLIS Mart — Catalog & Search Module (Vanilla JS)
 * Handles search suggestions/autocomplete, live filtering, sorting, and pagination.
 */
(function (window) {
    'use strict';

    var debounceTimer = null;
    var activeSuggestionIndex = -1;

    function debounce(func, wait) {
        return function () {
            var context = this, args = arguments;
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(function () {
                func.apply(context, args);
            }, wait);
        };
    }

    var GMCatalog = {
        /**
         * Initialize search autocomplete
         */
        initSearchSuggestions: function () {
            var searchInput = document.getElementById('gmHeaderSearchInput');
            var suggestionsBox = document.getElementById('gmSearchSuggestions');
            if (!searchInput || !suggestionsBox) return;

            var performSearch = debounce(function (query) {
                query = query.trim();
                if (query.length < 2) {
                    suggestionsBox.style.display = 'none';
                    suggestionsBox.innerHTML = '';
                    return;
                }

                fetch('/catalog/suggestions?q=' + encodeURIComponent(query), {
                    headers: { 'Accept': 'application/json' }
                })
                .then(function (res) { return res.json(); })
                .then(function (results) {
                    if (!results || results.length === 0) {
                        suggestionsBox.innerHTML = '<div class="p-3 text-muted small text-center">No products found for "' + query + '"</div>';
                        suggestionsBox.style.display = 'block';
                        return;
                    }

                    var html = '';
                    results.forEach(function (p, index) {
                        var imgTag = p.imagePath ? '<img src="' + p.imagePath + '" alt="' + p.name + '" class="gm-suggestion-img" onerror="this.style.display=\'none\'" />' : '';
                        html += '<a href="/products/' + p.slug + '" class="gm-search-suggestion-item" data-index="' + index + '">' +
                            imgTag +
                            '<div class="gm-suggestion-info">' +
                                '<div class="gm-suggestion-name">' + p.name + '</div>' +
                                '<div class="gm-suggestion-meta">' + (p.categoryName || '') + (p.unitLabel ? ' • ' + p.unitLabel : '') + '</div>' +
                            '</div>' +
                            '<div class="gm-suggestion-price">₹' + (p.price ? p.price.toFixed(2) : '') + '</div>' +
                        '</a>';
                    });

                    // Add "View all results" link
                    html += '<a href="/catalog/search?q=' + encodeURIComponent(query) + '" class="gm-search-suggestion-item text-center fw-semibold text-success justify-content-center border-top bg-light">' +
                        'View all results for "' + query + '" →' +
                    '</a>';

                    suggestionsBox.innerHTML = html;
                    suggestionsBox.style.display = 'block';
                    activeSuggestionIndex = -1;
                })
                .catch(function (err) {
                    console.error('Suggestions error:', err);
                });
            }, 250);

            searchInput.addEventListener('input', function () {
                performSearch(this.value);
            });

            // Keyboard navigation in suggestions
            searchInput.addEventListener('keydown', function (e) {
                var items = suggestionsBox.querySelectorAll('.gm-search-suggestion-item');
                if (!items.length || suggestionsBox.style.display === 'none') return;

                if (e.key === 'ArrowDown') {
                    e.preventDefault();
                    activeSuggestionIndex = (activeSuggestionIndex + 1) % items.length;
                    highlightSuggestion(items, activeSuggestionIndex);
                } else if (e.key === 'ArrowUp') {
                    e.preventDefault();
                    activeSuggestionIndex = (activeSuggestionIndex - 1 + items.length) % items.length;
                    highlightSuggestion(items, activeSuggestionIndex);
                } else if (e.key === 'Enter') {
                    if (activeSuggestionIndex >= 0 && activeSuggestionIndex < items.length) {
                        e.preventDefault();
                        items[activeSuggestionIndex].click();
                    }
                } else if (e.key === 'Escape') {
                    suggestionsBox.style.display = 'none';
                }
            });

            // Close suggestions when clicking outside
            document.addEventListener('click', function (e) {
                if (!searchInput.contains(e.target) && !suggestionsBox.contains(e.target)) {
                    suggestionsBox.style.display = 'none';
                }
            });
        },

        /**
         * Load catalog product grid asynchronously
         */
        loadGrid: function (url, pushHistory) {
            var gridContainer = document.getElementById('product-grid-container');
            if (!gridContainer) {
                // If not on catalog page, perform normal SPA transition
                if (window.GMSPA) {
                    window.GMSPA.navigate(url);
                } else {
                    window.location.href = url;
                }
                return;
            }

            // Append partial=true param
            var fetchUrl = new URL(url, window.location.origin);
            fetchUrl.searchParams.set('partial', 'true');

            gridContainer.style.opacity = '0.5';
            gridContainer.style.pointerEvents = 'none';

            fetch(fetchUrl.toString(), {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html'
                }
            })
            .then(function (res) { return res.text(); })
            .then(function (html) {
                gridContainer.innerHTML = html;
                gridContainer.style.opacity = '1';
                gridContainer.style.pointerEvents = '';
                gridContainer.classList.add('gm-fade-in');
                setTimeout(function () {
                    gridContainer.classList.remove('gm-fade-in');
                }, 300);

                // Update active state in sidebar filter links
                var targetPath = new URL(url, window.location.origin).pathname;
                document.querySelectorAll('.gm-filter-link').forEach(function (link) {
                    var linkPath = new URL(link.href, window.location.origin).pathname;
                    if (linkPath === targetPath) {
                        link.classList.add('active');
                    } else {
                        link.classList.remove('active');
                    }
                });

                if (pushHistory !== false) {
                    window.history.pushState({ spa: true, url: url, isCatalog: true }, '', url);
                }

                // Re-bind grid events
                GMCatalog.initCatalogEvents();
            })
            .catch(function (err) {
                console.error('Error loading grid:', err);
                gridContainer.style.opacity = '1';
                gridContainer.style.pointerEvents = '';
            });
        },

        /**
         * Initialize catalog filtering, sorting, and pagination interception
         */
        initCatalogEvents: function () {
            // Sort dropdown
            var sortSelect = document.getElementById('sortSelect');
            if (sortSelect) {
                sortSelect.onchange = function () {
                    var currentUrl = new URL(window.location.href);
                    currentUrl.searchParams.set('sort', this.value);
                    currentUrl.searchParams.set('page', '1');
                    GMCatalog.loadGrid(currentUrl.toString(), true);
                };
            }

            // Pagination links within product-grid-container
            var gridContainer = document.getElementById('product-grid-container');
            if (gridContainer) {
                gridContainer.querySelectorAll('.gm-pagination a.page-link').forEach(function (link) {
                    link.onclick = function (e) {
                        e.preventDefault();
                        GMCatalog.loadGrid(link.href, true);
                        window.scrollTo({ top: 0, behavior: 'smooth' });
                    };
                });
            }

            // Filter sidebar links
            document.querySelectorAll('.gm-filter-sidebar .gm-filter-link').forEach(function (link) {
                link.onclick = function (e) {
                    e.preventDefault();
                    GMCatalog.loadGrid(link.href, true);
                };
            });
        },

        /**
         * Live product search on the catalog page (debounced keyup/input + fetch).
         * Uses document-level delegation so it is bound exactly once and keeps working
         * after the grid partial or the whole page is swapped (SPA navigation).
         */
        initLiveSearch: function () {
            if (live.bound) return;
            live.bound = true;

            var INPUT_SELECTOR = '#gm-catalog-search-input';

            function onTyping(e) {
                var input = e.target;
                if (!input || !input.matches || !input.matches(INPUT_SELECTOR)) return;
                var delay = parseInt(input.getAttribute('data-debounce-ms'), 10);
                if (isNaN(delay) || delay < 0) delay = DEFAULT_DEBOUNCE_MS;
                clearTimeout(live.timer);
                live.timer = setTimeout(function () { runLiveSearch(input); }, delay);
            }

            document.addEventListener('keyup', onTyping);
            document.addEventListener('input', onTyping);   // paste, clear (x) button, mobile keyboards

            // Pressing Enter / the Search button runs the search immediately (no page reload)
            document.addEventListener('submit', function (e) {
                var form = e.target;
                if (!form || !form.classList || !form.classList.contains('gm-catalog-search-form')) return;
                var input = form.querySelector(INPUT_SELECTOR);
                if (!input) return;
                e.preventDefault();
                clearTimeout(live.timer);
                runLiveSearch(input);
            });
        },

        init: function () {
            GMCatalog.initSearchSuggestions();
            GMCatalog.initCatalogEvents();
            GMCatalog.initLiveSearch();
        }
    };

    // ─── Live search internals ──────────────────────────────────────────────
    var DEFAULT_DEBOUNCE_MS = 300;
    var live = { bound: false, timer: null, controller: null, seq: 0, pendingTerm: null };

    function setLiveLoading(isLoading) {
        var spinner = document.getElementById('gm-search-spinner');
        var results = document.getElementById('gm-catalog-results');
        if (spinner) spinner.classList.toggle('d-none', !isLoading);
        if (results) {
            results.style.opacity = isLoading ? '0.5' : '';
            results.setAttribute('aria-busy', isLoading ? 'true' : 'false');
        }
    }

    function showLiveError(message) {
        var box = document.getElementById('gm-search-error');
        var results = document.getElementById('gm-catalog-results');
        if (box) {
            box.textContent = message || '';
            box.classList.toggle('d-none', !message);
        }
        // Hide stale results while an error is shown so they are not mistaken for the new search
        if (results) results.classList.toggle('d-none', !!message);
    }

    function cancelInflight() {
        live.seq++;
        live.pendingTerm = null;
        if (live.controller) {
            live.controller.abort();
            live.controller = null;
        }
    }

    function syncFiltersAfterSearch(term) {
        // Keep other forms/links in sync so category / availability / sort navigation keeps the term
        document.querySelectorAll('.gm-catalog-filter-form [name="search"]').forEach(function (f) { f.value = term; });
        document.querySelectorAll('.gm-filter-sidebar a.gm-filter-link').forEach(function (link) {
            var u = new URL(link.href, window.location.origin);
            if (term) u.searchParams.set('search', term); else u.searchParams.delete('search');
            link.href = u.pathname + u.search;
        });

        // Reflect the term in the address bar (reload / share) without adding history entries
        if (window.location.pathname.toLowerCase().indexOf('/catalog/category/') !== 0) {
            var cur = new URL(window.location.href);
            if (term) cur.searchParams.set('search', term); else cur.searchParams.delete('search');
            cur.searchParams.delete('page');
            cur.searchParams.delete('partial');
            window.history.replaceState(window.history.state, '', cur.pathname + cur.search);
        }
    }

    function applyLiveResults(html, term) {
        var doc = new DOMParser().parseFromString(html, 'text/html');
        var newResults = doc.getElementById('gm-catalog-results');
        var newHeading = doc.getElementById('gm-catalog-heading');
        var curResults = document.getElementById('gm-catalog-results');
        var curHeading = document.getElementById('gm-catalog-heading');
        if (!newResults || !curResults) throw new Error('Unexpected search response');

        curResults.innerHTML = newResults.innerHTML;

        if (newHeading && curHeading) {
            if (window.location.pathname.toLowerCase().indexOf('/catalog/category/') === 0) {
                // Keep the category title, refresh only the product count
                var newCount = newHeading.querySelector('small');
                var curCount = curHeading.querySelector('small');
                if (newCount && curCount) curCount.textContent = newCount.textContent;
            } else {
                curHeading.innerHTML = newHeading.innerHTML;
            }
        }

        curResults.classList.remove('gm-fade-in');
        void curResults.offsetWidth;
        curResults.classList.add('gm-fade-in');
        curResults.addEventListener('animationend', function () {
            curResults.classList.remove('gm-fade-in');
        }, { once: true });

        syncFiltersAfterSearch(term);
    }

    function runLiveSearch(input) {
        var term = input.value.trim();

        if (input.dataset.appliedTerm === undefined) {
            input.dataset.appliedTerm = (input.defaultValue || '').trim();
        }

        // Already requesting this exact term
        if (term === live.pendingTerm) return;

        // Back to the term that is already displayed: drop any outdated in-flight request
        if (term === input.dataset.appliedTerm) {
            if (live.controller) {
                cancelInflight();
                setLiveLoading(false);
            }
            showLiveError('');
            return;
        }

        // Cancel the previous request; its late response must never touch the UI
        cancelInflight();
        var mySeq = live.seq;
        var controller = new AbortController();
        live.controller = controller;
        live.pendingTerm = term;

        // Build the request from the current filter state (category / sort / availability)
        var filterForm = document.querySelector('.gm-catalog-filter-form');
        var searchForm = input.form;
        var url = new URL((searchForm && searchForm.getAttribute('action')) || '/catalog', window.location.origin);
        ['category', 'sortBy', 'sortOrder', 'inStock'].forEach(function (name) {
            var field = (filterForm && filterForm.elements[name]) || (searchForm && searchForm.elements[name]);
            if (field && field.value) url.searchParams.set(name, field.value);
        });
        if (term) url.searchParams.set('search', term);
        url.searchParams.set('page', '1');
        url.searchParams.set('partial', 'true');

        showLiveError('');
        setLiveLoading(true);

        fetch(url.toString(), {
            signal: controller.signal,
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'text/html' }
        })
        .then(function (res) {
            if (!res.ok) throw new Error('Search request failed (' + res.status + ')');
            return res.text();
        })
        .then(function (html) {
            if (mySeq !== live.seq || !input.isConnected) return;   // outdated response / page swapped
            applyLiveResults(html, term);
            input.dataset.appliedTerm = term;
            live.controller = null;
            live.pendingTerm = null;
            setLiveLoading(false);
        })
        .catch(function (err) {
            if (err && err.name === 'AbortError') return; // superseded by a newer request
            if (mySeq !== live.seq || !input.isConnected) return;
            console.error('Live search error:', err);
            live.controller = null;
            live.pendingTerm = null;
            setLiveLoading(false);
            showLiveError('We couldn\u2019t load search results. Please check your connection and try again.');
        });
    }

    function highlightSuggestion(items, index) {
        items.forEach(function (item, i) {
            if (i === index) {
                item.classList.add('active');
                item.scrollIntoView({ block: 'nearest' });
            } else {
                item.classList.remove('active');
            }
        });
    }

    window.GMCatalog = GMCatalog;

    document.addEventListener('DOMContentLoaded', function () {
        GMCatalog.init();
    });

})(window);
