// GOLIS Mart — location-picker.js
// Automatic address detection using the browser Geolocation API.
//
// Usage:
//   Add a button with id="locationPickerBtn" and data-api-base="<API base URL>".
//   The script populates address form fields when a reverse-geocode succeeds.
//
// Privacy:
//   • GPS permission is only requested when the user clicks the button.
//   • No continuous tracking; one single shot per click.
//   • Coordinates are sent to the GOLIS Mart backend only, not to any external service.

(function () {
    'use strict';

    // ── Field IDs that match the Razor form ──────────────────────────────────
    var FIELD_HOUSE_NUMBER   = 'HouseNumber';
    var FIELD_BUILDING_STREET = 'BuildingStreet';
    var FIELD_LOCALITY       = 'Locality';
    var FIELD_LANDMARK       = 'Landmark';
    var FIELD_CITY           = 'City';
    var FIELD_STATE          = 'State';
    var FIELD_PINCODE        = 'Pincode';

    // ── State ────────────────────────────────────────────────────────────────
    var isRunning = false;

    // ── Entry point ──────────────────────────────────────────────────────────

    document.addEventListener('DOMContentLoaded', function () {
        var btn = document.getElementById('locationPickerBtn');
        if (!btn) return;

        btn.addEventListener('click', function (e) {
            e.preventDefault();
            if (isRunning) return;
            handleLocationRequest(btn);
        });
    });

    // ── Main flow ────────────────────────────────────────────────────────────

    function handleLocationRequest(btn) {
        // Guard: geolocation supported?
        if (!navigator.geolocation) {
            showStatus('error',
                'Your browser does not support location detection. Please enter your address manually.');
            return;
        }

        // Guard: secure context required
        if (window.isSecureContext === false) {
            showStatus('error',
                'Location access requires a secure connection (HTTPS). Please enter your address manually.');
            return;
        }

        setRunning(btn, true);
        showStatus('info', 'Detecting your location\u2026 We need permission to access your device\u2019s GPS. ' +
            'Your coordinates will be sent to our server only to look up the nearest address.');

        var options = {
            enableHighAccuracy: true,
            timeout: 15000,          // 15 s
            maximumAge: 30000        // accept a cached position up to 30 s old
        };

        navigator.geolocation.getCurrentPosition(
            function (position) { onPositionSuccess(btn, position); },
            function (error)    { onPositionError(btn, error); },
            options
        );
    }

    // ── Geolocation callbacks ─────────────────────────────────────────────────

    function onPositionSuccess(btn, position) {
        var lat = position.coords.latitude;
        var lon = position.coords.longitude;

        showStatus('info', 'Location obtained. Looking up address\u2026');

        var url = '/api/location/reverse-geocode'
                + '?latitude='  + encodeURIComponent(lat)
                + '&longitude=' + encodeURIComponent(lon);

        fetch(url, {
            method: 'GET',
            headers: { 'Accept': 'application/json' }
        })
        .then(function (response) {
            if (response.status === 429) {
                throw { type: 'rate-limit' };
            }
            if (response.status === 503) {
                throw { type: 'unavailable' };
            }
            if (!response.ok) {
                throw { type: 'http', status: response.status };
            }
            return response.json();
        })
        .then(function (data) {
            if (!data || !data.success || !data.address) {
                showStatus('warning',
                    'Could not determine a usable address for your location. '
                    + 'Please enter your address manually.');
                return;
            }
            populateFields(data.address);
            showStatus('success',
                '\u2705 Address detected from your location. '
                + 'Please review every field and correct any details before saving.');
        })
        .catch(function (err) {
            var msg;
            if (err && err.type === 'rate-limit') {
                msg = 'Too many location requests. Please wait a moment and try again.';
            } else if (err && err.type === 'unavailable') {
                msg = 'The location lookup service is currently unavailable. Please enter your address manually.';
            } else {
                msg = 'Could not look up the address. Please enter your address manually.';
            }
            showStatus('error', msg);
        })
        .finally(function () {
            setRunning(btn, false);
        });
    }

    function onPositionError(btn, error) {
        setRunning(btn, false);
        var msg;
        switch (error.code) {
            case error.PERMISSION_DENIED:
                msg = 'Location permission was denied. You can still enter your address manually.';
                break;
            case error.POSITION_UNAVAILABLE:
                msg = 'Your location could not be determined. Please check that location services are enabled on your device.';
                break;
            case error.TIMEOUT:
                msg = 'Location request timed out. Please try again or enter your address manually.';
                break;
            default:
                msg = 'Location detection failed. Please enter your address manually.';
        }
        showStatus('error', msg);
    }

    // ── Field population ──────────────────────────────────────────────────────

    function populateFields(address) {
        setField(FIELD_HOUSE_NUMBER,    address.houseNumber);
        setField(FIELD_BUILDING_STREET, address.street);
        setField(FIELD_LOCALITY,        address.locality);
        setField(FIELD_LANDMARK,        address.landmark || '');
        setField(FIELD_CITY,            address.city);
        setField(FIELD_STATE,           address.state);
        setField(FIELD_PINCODE,         address.pinCode);

        // Trigger browser validation events so any existing validator (jQuery Unobtrusive)
        // rechecks the newly populated fields.
        var fieldIds = [FIELD_HOUSE_NUMBER, FIELD_BUILDING_STREET, FIELD_LOCALITY,
                        FIELD_CITY, FIELD_STATE, FIELD_PINCODE];
        fieldIds.forEach(function (id) {
            var el = document.getElementById(id);
            if (el) {
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
    }

    function setField(id, value) {
        var el = document.getElementById(id);
        if (!el) return;
        // Only fill if value is non-empty; leave existing content if the geocoder
        // returned nothing for this field so the customer can fill it themselves.
        if (value && value.trim() !== '') {
            el.value = value.trim();
        }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    function setRunning(btn, running) {
        isRunning = running;
        btn.disabled = running;

        var spinner  = btn.querySelector('.location-spinner');
        var btnText  = btn.querySelector('.location-btn-text');

        if (spinner) spinner.style.display = running ? 'inline-block' : 'none';
        if (btnText) btnText.textContent   = running ? 'Detecting\u2026' : '\uD83D\uDCCD Use My Current Location';
    }

    function showStatus(type, message) {
        var container = document.getElementById('locationStatusMsg');
        if (!container) return;

        // Remove existing alert classes
        container.className = 'alert mt-2';
        container.style.display = 'block';

        switch (type) {
            case 'success': container.className += ' alert-success'; break;
            case 'warning': container.className += ' alert-warning'; break;
            case 'error':   container.className += ' alert-danger';  break;
            default:        container.className += ' alert-info';    break;
        }

        // Use textContent to prevent XSS — never insert raw provider strings as HTML
        container.textContent = message;
    }

})();
