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

        init: function () {
            GMCatalog.initSearchSuggestions();
            GMCatalog.initCatalogEvents();
        }
    };

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
