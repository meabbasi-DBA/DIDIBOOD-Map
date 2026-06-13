(function () {
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

    async function loadBoundary() {
        if (boundaryMode !== 'municipality') return null;

        try {
            const res = await fetch(`${apiBase}/api/coverage/boundary`);
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

    async function loadSummary() {
        const el = document.getElementById('coverage-summary');
        if (!el) return;

        try {
            const res = await fetch(`${apiBase}/api/coverage/summary`);
            if (!res.ok) return;

            const s = await res.json();
            let debugLine = '';
            try {
                const dbgRes = await fetch(`${apiBase}/api/coverage/debug`);
                if (dbgRes.ok) {
                    const d = await dbgRes.json();
                    debugLine = `<div class="text-muted">${d.sourceMode} · ${d.baseCells} سلول + ${d.virtualCenters} مرزی · پوشش تخمینی ${d.estimatedCoveragePercent}%</div>`;
                }
            } catch (_) { /* optional */ }
            el.innerHTML = `
                <div><strong>${s.coveragePercent}%</strong> پوشش موفق</div>
                <div>${s.successCells}/${s.totalCells} سلول — کهنه: ${s.staleCells} — ناموفق: ${s.failedCells}</div>
                ${debugLine}
            `;
        } catch (err) {
            console.error('Coverage summary failed', err);
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
            const res = await fetch(`${apiBase}/api/coverage/cells?${params}`);
            if (!res.ok) {
                console.error('Coverage cells request failed', res.status);
                return;
            }

            const geoJson = await res.json();
            if (seq !== loadSeq) return;

            let features = geoJson.features || [];

            const minPoi = document.getElementById('filter-min-poi').value;
            if (minPoi && Number(minPoi) > 0) {
                features = features.filter(f => (f.properties?.poiCount || 0) >= Number(minPoi));
            }

            cellLayer.clearLayers();
            centroidLayer.clearLayers();
            heatmapLayer.clearLayers();

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
        } catch (err) {
            console.error('Coverage cells failed', err);
        }
    }

    async function loadHeatmap() {
        if (!mapReady) return;

        const seq = ++loadSeq;
        const params = buildParams();

        try {
            const res = await fetch(`${apiBase}/api/coverage/heatmap?${params}`);
            if (!res.ok) return;

            const points = await res.json();
            if (seq !== loadSeq) return;

            cellLayer.clearLayers();
            centroidLayer.clearLayers();
            heatmapLayer.clearLayers();

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
        } catch (err) {
            console.error('Coverage heatmap failed', err);
        }
    }

    async function reload() {
        await loadSummary();
        if (!boundaryLayer && boundaryMode === 'municipality') {
            await loadBoundary();
        }
        const mode = document.getElementById('view-mode').value;
        if (mode === 'heatmap') {
            await loadHeatmap();
        } else {
            await loadCells();
        }
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

    document.getElementById('btn-reload').addEventListener('click', reload);
    document.getElementById('view-mode').addEventListener('change', reload);

    initMap();
})();
