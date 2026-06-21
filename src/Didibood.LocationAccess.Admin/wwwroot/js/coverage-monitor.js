(function () {
    if (window.__coverageMonitorCleanup) {
        window.__coverageMonitorCleanup();
    }

    const cfg = window.coverageConfig || {};
    const apiBase = (cfg.apiBase || window.location.origin || '').replace(/\/$/, '');
    const tehranBounds = cfg.tehranBounds || {
        minLat: 35.50,
        maxLat: 35.88,
        minLng: 51.10,
        maxLng: 51.62
    };
    const gridResolution = cfg.gridResolution || 7;
    const boundaryMode = cfg.boundaryMode || 'municipality';
    const csrfToken = document.querySelector('meta[name="csrf-token"]')?.content || '';
    const traceSessionId = (window.crypto && window.crypto.randomUUID)
        ? window.crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const traceStartedAt = performance.now();

    let boundaryLayer;

    const statusColors = {
        pending: '#6c757d',
        success: '#198754',
        failed: '#dc3545',
        stale: '#fd7e14'
    };

    const statusLabels = {
        pending: 'در انتظار',
        success: 'موفق',
        failed: 'ناموفق',
        stale: 'کهنه'
    };

    let map;
    let cellLayer;
    let centroidLayer;
    let heatmapLayer;
    let mapReady = false;
    let loadSeq = 0;
    let debugSeq = 0;
    let currentAbort;
    const cleanupHandlers = [];

    function trace(eventName, options) {
        const payload = {
            sessionId: traceSessionId,
            eventName,
            timestamp: new Date().toISOString(),
            durationMs: Number((performance.now() - traceStartedAt).toFixed(1)),
            endpoint: options?.endpoint || null,
            gridNumber: options?.gridNumber || null,
            h3Index: options?.h3Index || null,
            status: options?.status || null,
            details: options?.details || null
        };

        const body = JSON.stringify(payload);
        const send = () => fetch('/Coverage?handler=Trace', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': csrfToken
            },
            body,
            keepalive: true
        }).catch(() => { /* trace must never affect rendering */ });

        if ('requestIdleCallback' in window) {
            window.requestIdleCallback(send, { timeout: 1000 });
        } else {
            window.setTimeout(send, 0);
        }

        if (window.console?.debug) {
            console.debug('[coverage-flow]', payload);
        }
    }

    window.coverageTraceEvent = trace;
    trace('page_load_start');

    window.__coverageMonitorCleanup = function () {
        if (currentAbort) currentAbort.abort();
        cleanupHandlers.splice(0).forEach(cleanup => cleanup());
        if (window.coverageTraceEvent === trace) {
            delete window.coverageTraceEvent;
        }
        if (map) {
            map.off();
            map.remove();
            map = null;
        }
    };

    function tehranLatLngBounds() {
        return L.latLngBounds(
            [tehranBounds.minLat, tehranBounds.minLng],
            [tehranBounds.maxLat, tehranBounds.maxLng]
        );
    }

    function setTehranView() {
        if (!map) return;
        map.fitBounds(tehranLatLngBounds(), { padding: [24, 24], maxZoom: 12 });
    }

    function fitCellBounds() {
        if (!map || !cellLayer) return;
        const bounds = cellLayer.getBounds();
        if (bounds.isValid()) {
            map.fitBounds(bounds.pad(0.02), { padding: [24, 24], maxZoom: 12 });
        } else {
            setTehranView();
        }
    }

    function initMap() {
        const center = [
            (tehranBounds.minLat + tehranBounds.maxLat) / 2,
            (tehranBounds.minLng + tehranBounds.maxLng) / 2
        ];

        // Standard Leaflet only — Neshan L.Map rejects L.geoJSON overlays (addLayer is not a function).
        map = L.map('coverage-map', { center, zoom: 11, zoomControl: true });
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(map);

        const legend = L.control({ position: 'bottomleft' });
        legend.onAdd = function () {
            const div = L.DomUtil.create('div', 'coverage-legend');
            div.innerHTML = [
                '<div><span style="background:#198754"></span>موفق</div>',
                '<div><span style="background:#6c757d"></span>در انتظار</div>',
                '<div><span style="background:#dc3545"></span>ناموفق</div>',
                '<div><span style="background:#fd7e14"></span>کهنه</div>'
            ].join('');
            return div;
        };
        legend.addTo(map);

        cellLayer = L.geoJSON(null, {
            style: feature => {
                if (feature.geometry?.type === 'Point') return {};
                const status = feature.properties?.status || 'pending';
                const color = statusColors[status] || '#0d6efd';
                return {
                    color,
                    weight: 2,
                    fillColor: color,
                    fillOpacity: 0.28
                };
            },
            pointToLayer: (feature, latlng) => {
                const props = feature.properties || {};
                const status = props.status || 'pending';
                const color = props.isRefined ? '#6610f2' : (statusColors[status] || '#0d6efd');
                return L.circleMarker(latlng, {
                    radius: props.isRefined ? 5 : 3,
                    fillColor: color,
                    color: '#fff',
                    weight: props.isRefined ? 1.5 : 1,
                    fillOpacity: 0.95
                });
            },
            onEachFeature: (feature, layer) => {
                const props = feature.properties || {};
                const status = props.status || 'pending';
                const label = statusLabels[status] || status;
                const refined = props.isRefined ? ' (مرز)' : '';
                layer.bindTooltip(
                    `<strong>${label}${refined}</strong><br/>POI: ${props.poiCount ?? 0}<br/>H3: ${props.h3Index ?? '—'}`,
                    { sticky: true, direction: 'top', className: 'coverage-cell-tooltip' }
                );
                layer.on('click', () => {
                    if (props.h3Index) showCellDetail(props.h3Index);
                });
            }
        }).addTo(map);

        centroidLayer = L.layerGroup().addTo(map);
        heatmapLayer = L.layerGroup().addTo(map);

        map.whenReady(() => {
            mapReady = true;
            map.invalidateSize();
            loadBoundary().then(() => reload());
        });
    }

    function clearLayerGroup(layerGroup) {
        if (!layerGroup) return;
        layerGroup.eachLayer(layer => {
            layer.off();
            if (layer.unbindTooltip) layer.unbindTooltip();
            if (layer.unbindPopup) layer.unbindPopup();
        });
        layerGroup.clearLayers();
    }

    async function loadBoundary() {
        if (boundaryMode !== 'municipality') return null;

        try {
            const endpoint = `${apiBase}/api/coverage/boundary`;
            const started = performance.now();
            const res = await fetch(endpoint);
            if (!res.ok) return null;
            const geoJson = await res.json();
            if (boundaryLayer) {
                map.removeLayer(boundaryLayer);
            }
            boundaryLayer = L.geoJSON(geoJson, {
                style: {
                    color: '#0d6efd',
                    weight: 2,
                    fillColor: '#0d6efd',
                    fillOpacity: 0.04
                },
                interactive: false
            }).addTo(map);
            trace('boundary_loaded', {
                endpoint,
                status: String(res.status),
                details: `features=${geoJson.features?.length ?? 0};requestMs=${(performance.now() - started).toFixed(1)}`
            });
            return boundaryLayer;
        } catch (err) {
            console.error('Boundary load failed', err);
            return null;
        }
    }

    function fitMapToBoundaryOrCells() {
        if (boundaryLayer) {
            const bounds = boundaryLayer.getBounds();
            if (bounds.isValid()) {
                map.fitBounds(bounds, { padding: [24, 24], maxZoom: 12 });
                return;
            }
        }
        fitCellBounds();
    }

    async function loadSummary(signal) {
        const el = document.getElementById('coverage-summary');
        if (!el) return;

        try {
            const endpoint = `${apiBase}/api/coverage/summary`;
            const started = performance.now();
            const res = await fetch(endpoint, { signal });
            if (!res.ok) return;

            const s = await res.json();
            el.innerHTML = `
                <div><strong>${s.coveragePercent}%</strong> پوشش موفق</div>
                <div>${s.successCells}/${s.totalCells} سلول — کهنه: ${s.staleCells} — ناموفق: ${s.failedCells}</div>
                <div class="text-muted" id="coverage-debug-line">در حال بارگذاری داده تشخیصی…</div>
            `;
            trace('summary_loaded', {
                endpoint,
                status: String(res.status),
                details: `totalCells=${s.totalCells};successCells=${s.successCells};requestMs=${(performance.now() - started).toFixed(1)}`
            });
            return true;
        } catch (err) {
            if (err.name !== 'AbortError') console.error('Coverage summary failed', err);
            return false;
        }
    }

    async function loadDebugLine(seq, signal) {
        const el = document.getElementById('coverage-debug-line');
        if (!el) return;

        try {
            const endpoint = `${apiBase}/api/coverage/debug`;
            trace('debug_requested', { endpoint });
            const started = performance.now();
            const res = await fetch(endpoint, { signal });
            if (!res.ok || seq !== debugSeq) return;

            const d = await res.json();
            if (seq !== debugSeq) return;

            el.textContent = `${d.sourceMode} · ${d.baseCells} سلول + ${d.virtualCenters} مرزی · پوشش تخمینی ${d.estimatedCoveragePercent}%`;
            trace('debug_completed', {
                endpoint,
                status: String(res.status),
                details: `baseCells=${d.baseCells};virtualCenters=${d.virtualCenters};requestMs=${(performance.now() - started).toFixed(1)}`
            });
        } catch (err) {
            if (err.name !== 'AbortError') {
                console.error('Coverage debug failed', err);
                if (seq === debugSeq && el) el.textContent = 'داده تشخیصی در دسترس نیست.';
            }
        }
    }

    function scheduleDebugLoad(signal) {
        const seq = ++debugSeq;
        const start = () => loadDebugLine(seq, signal);
        if ('requestIdleCallback' in window) {
            window.requestIdleCallback(start, { timeout: 2000 });
        } else {
            window.setTimeout(start, 0);
        }
    }

    function buildParams() {
        const status = document.getElementById('filter-status').value;
        const minPoi = document.getElementById('filter-min-poi').value;
        const maxAge = document.getElementById('filter-max-age').value;
        const category = document.getElementById('filter-category').value;
        const params = new URLSearchParams();

        params.set('resolution', String(gridResolution));

        if (boundaryMode !== 'municipality') {
            params.set('minLat', String(tehranBounds.minLat));
            params.set('maxLat', String(tehranBounds.maxLat));
            params.set('minLng', String(tehranBounds.minLng));
            params.set('maxLng', String(tehranBounds.maxLng));
        }

        if (status) params.set('status', status);
        if (maxAge && Number(maxAge) > 0) params.set('maxAgeDays', maxAge);
        if (minPoi && Number(minPoi) > 0) params.set('minPoiCount', minPoi);
        if (category) params.set('categoryId', category);

        return params;
    }

    function hasActiveFilters() {
        return Boolean(
            document.getElementById('filter-status').value ||
            document.getElementById('filter-min-poi').value ||
            document.getElementById('filter-max-age').value
        );
    }

    function addCentroidMarkers(features) {
        centroidLayer.clearLayers();

        features.forEach(feature => {
            if (feature.geometry?.type === 'Point') return;
            const props = feature.properties || {};
            const lat = props.centroidLat;
            const lng = props.centroidLng;
            if (lat == null || lng == null) return;

            const status = props.status || 'pending';
            const marker = L.circleMarker([lat, lng], {
                radius: 3,
                fillColor: statusColors[status] || '#0d6efd',
                color: '#fff',
                weight: 1,
                fillOpacity: 0.95
            });
            centroidLayer.addLayer(marker);
        });
    }

    async function loadCells() {
        if (!mapReady) return;

        const seq = ++loadSeq;
        const params = buildParams();
        params.set('limit', '5000');

        try {
            const endpoint = `${apiBase}/api/coverage/cells?${params}`;
            const started = performance.now();
            const res = await fetch(endpoint);
            if (!res.ok) {
                console.error('Coverage cells request failed', res.status);
                return;
            }

            const geoJson = await res.json();
            if (seq !== loadSeq) return;

            let features = geoJson.features || [];
            trace('cells_loaded', {
                endpoint,
                status: String(res.status),
                details: `features=${features.length};requestMs=${(performance.now() - started).toFixed(1)}`
            });

            const minPoi = document.getElementById('filter-min-poi').value;
            if (minPoi && Number(minPoi) > 0) {
                features = features.filter(f => (f.properties?.poiCount || 0) >= Number(minPoi));
            }

            clearLayerGroup(cellLayer);
            clearLayerGroup(centroidLayer);
            clearLayerGroup(heatmapLayer);

            if (features.length > 0) {
                cellLayer.addData({ type: 'FeatureCollection', features });
                addCentroidMarkers(features);

                if (hasActiveFilters()) {
                    const bounds = cellLayer.getBounds();
                    if (bounds.isValid()) {
                        map.fitBounds(bounds.pad(0.05), { padding: [24, 24], maxZoom: 13 });
                    }
                } else {
                    fitMapToBoundaryOrCells();
                }
            } else {
                fitMapToBoundaryOrCells();
            }
            trace('map_rendered', {
                endpoint,
                details: `mode=cells;features=${features.length};interactiveNodes=${document.querySelectorAll('#coverage-map .leaflet-interactive').length}`
            });
        } catch (err) {
            if (err.name !== 'AbortError') console.error('Coverage cells failed', err);
        }
    }

    async function loadHeatmap() {
        if (!mapReady) return;

        const seq = ++loadSeq;
        const params = buildParams();

        try {
            const endpoint = `${apiBase}/api/coverage/heatmap?${params}`;
            const started = performance.now();
            const res = await fetch(endpoint);
            if (!res.ok) return;

            const points = await res.json();
            if (seq !== loadSeq) return;
            trace('cells_loaded', {
                endpoint,
                status: String(res.status),
                details: `mode=heatmap;points=${points.length};requestMs=${(performance.now() - started).toFixed(1)}`
            });

            clearLayerGroup(cellLayer);
            clearLayerGroup(centroidLayer);
            clearLayerGroup(heatmapLayer);

            const bounds = L.latLngBounds([]);
            points.forEach(p => {
                const radius = Math.min(24, 6 + Math.sqrt(p.weight));
                const color = statusColors[p.status] || '#0d6efd';
                const circle = L.circleMarker([p.lat, p.lng], {
                    radius,
                    fillColor: color,
                    color: '#fff',
                    weight: 1,
                    fillOpacity: 0.6
                });
                circle.bindTooltip(`POI: ${p.weight}`, { direction: 'top' });
                circle.on('click', () => {
                    if (p.h3Index) showCellDetail(p.h3Index);
                });
                heatmapLayer.addLayer(circle);
                bounds.extend([p.lat, p.lng]);
            });

            if (bounds.isValid() && hasActiveFilters()) {
                map.fitBounds(bounds.pad(0.05), { padding: [24, 24], maxZoom: 13 });
            } else if (bounds.isValid()) {
                map.fitBounds(bounds.pad(0.02), { padding: [24, 24], maxZoom: 12 });
            } else {
                setTehranView();
            }
            trace('map_rendered', {
                endpoint,
                details: `mode=heatmap;points=${points.length};interactiveNodes=${document.querySelectorAll('#coverage-map .leaflet-interactive').length}`
            });
        } catch (err) {
            if (err.name !== 'AbortError') console.error('Coverage heatmap failed', err);
        }
    }

    async function reload() {
        if (currentAbort) currentAbort.abort();
        currentAbort = new AbortController();
        const { signal } = currentAbort;
        const summaryPromise = loadSummary(signal);

        if (!boundaryLayer && boundaryMode === 'municipality') {
            await loadBoundary();
        }

        const mode = document.getElementById('view-mode').value;
        if (mode === 'heatmap') {
            await loadHeatmap();
        } else {
            await loadCells();
        }

        summaryPromise.finally(() => {
            if (!signal.aborted) scheduleDebugLoad(signal);
        });
    }

    async function showCellDetail(h3Index) {
        const card = document.getElementById('cell-detail-card');
        const body = document.getElementById('cell-detail-body');
        const mapBox = document.getElementById('cell-static-map');

        const res = await fetch(`${apiBase}/api/coverage/cells/${h3Index}`);
        if (!res.ok) return;

        const cell = await res.json();
        card.style.display = 'block';
        body.innerHTML = `
            <div><strong>H3:</strong> ${cell.h3Index}</div>
            <div><strong>وضعیت:</strong> ${cell.status}</div>
            <div><strong>POI:</strong> ${cell.poiCount}</div>
            <div><strong>آخرین Crawl:</strong> ${cell.lastCrawlAt || '—'}</div>
            <div><strong>خطا:</strong> ${cell.failureReason || '—'}</div>
        `;

        const staticUrl = `${apiBase}/api/static-map?latitude=${cell.centroidLat}&longitude=${cell.centroidLng}&zoom=14&width=280&height=160&style=light`;
        mapBox.innerHTML = `<img src="${staticUrl}" alt="static map" class="img-fluid rounded" onerror="this.style.display='none'" />`;

        map.flyTo([cell.centroidLat, cell.centroidLng], 13, { duration: 0.6 });
    }

    function bind(el, eventName, handler) {
        if (!el) return;
        el.addEventListener(eventName, handler);
        cleanupHandlers.push(() => el.removeEventListener(eventName, handler));
    }

    bind(document.getElementById('btn-reload'), 'click', reload);
    bind(document.getElementById('view-mode'), 'change', reload);

    initMap();
})();
