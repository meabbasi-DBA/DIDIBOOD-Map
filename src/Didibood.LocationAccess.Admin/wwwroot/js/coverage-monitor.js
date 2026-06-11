(function () {
    const cfg = window.coverageConfig || {};
    const apiBase = (cfg.apiBase || window.location.origin || '').replace(/\/$/, '');
    const webMapKey = cfg.webMapKey || '';
    const tehranBounds = cfg.tehranBounds || {
        minLat: 35.48,
        maxLat: 35.92,
        minLng: 51.08,
        maxLng: 51.65
    };

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

    function tehranLatLngBounds() {
        return L.latLngBounds(
            [tehranBounds.minLat, tehranBounds.minLng],
            [tehranBounds.maxLat, tehranBounds.maxLng]
        );
    }

    function setTehranView() {
        map.fitBounds(tehranLatLngBounds(), { padding: [24, 24], maxZoom: 12 });
    }

    function initMap() {
        const center = [
            (tehranBounds.minLat + tehranBounds.maxLat) / 2,
            (tehranBounds.minLng + tehranBounds.maxLng) / 2
        ];

        if (webMapKey) {
            map = new L.Map('coverage-map', {
                key: webMapKey,
                maptype: 'dreamy',
                poi: false,
                traffic: false,
                center,
                zoom: 11
            });
        } else {
            map = L.map('coverage-map', { center, zoom: 11, zoomControl: true });
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '&copy; OpenStreetMap'
            }).addTo(map);
        }

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
                const status = feature.properties?.status || 'pending';
                const color = statusColors[status] || '#0d6efd';
                return {
                    color,
                    weight: 2,
                    fillColor: color,
                    fillOpacity: 0.28
                };
            },
            onEachFeature: (feature, layer) => {
                const props = feature.properties || {};
                const status = props.status || 'pending';
                const label = statusLabels[status] || status;
                layer.bindTooltip(
                    `<strong>${label}</strong><br/>POI: ${props.poiCount ?? 0}<br/>H3: ${props.h3Index ?? '—'}`,
                    { sticky: true, direction: 'top', className: 'coverage-cell-tooltip' }
                );
                layer.on('click', () => {
                    if (props.h3Index) showCellDetail(props.h3Index);
                });
            }
        }).addTo(map);

        centroidLayer = L.layerGroup().addTo(map);

        heatmapLayer = L.layerGroup().addTo(map);

        setTimeout(() => map.invalidateSize(), 0);
        setTehranView();
    }

    async function loadSummary() {
        const el = document.getElementById('coverage-summary');
        if (!el) return;

        const res = await fetch(`${apiBase}/api/coverage/summary`);
        if (!res.ok) return;

        const s = await res.json();
        el.innerHTML = `
            <div><strong>${s.coveragePercent}%</strong> پوشش موفق</div>
            <div>${s.successCells}/${s.totalCells} سلول — کهنه: ${s.staleCells} — ناموفق: ${s.failedCells}</div>
        `;
    }

    function buildParams() {
        const status = document.getElementById('filter-status').value;
        const minPoi = document.getElementById('filter-min-poi').value;
        const maxAge = document.getElementById('filter-max-age').value;
        const category = document.getElementById('filter-category').value;
        const params = new URLSearchParams();

        params.set('resolution', '8');
        params.set('minLat', String(tehranBounds.minLat));
        params.set('maxLat', String(tehranBounds.maxLat));
        params.set('minLng', String(tehranBounds.minLng));
        params.set('maxLng', String(tehranBounds.maxLng));

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
        const params = buildParams();
        params.set('limit', '2000');

        const res = await fetch(`${apiBase}/api/coverage/cells?${params}`);
        if (!res.ok) return;

        const geoJson = await res.json();
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
                setTehranView();
            }
        } else {
            setTehranView();
        }
    }

    async function loadHeatmap() {
        const params = buildParams();
        const res = await fetch(`${apiBase}/api/coverage/heatmap?${params}`);
        if (!res.ok) return;

        const points = await res.json();
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
        } else {
            setTehranView();
        }
    }

    async function reload() {
        await loadSummary();
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
    reload();
})();
