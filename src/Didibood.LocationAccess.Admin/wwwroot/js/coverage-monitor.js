(function () {
    const cfg = window.coverageConfig || {};
    const apiBase = (cfg.apiBase || '').replace(/\/$/, '');
    const webMapKey = cfg.webMapKey || '';

    const statusColors = {
        pending: '#6c757d',
        success: '#198754',
        failed: '#dc3545',
        stale: '#fd7e14'
    };

    let map;
    let cellLayer;
    let heatmapLayer;

    function initMap() {
        map = new L.Map('coverage-map', {
            key: webMapKey,
            maptype: 'neshan',
            poi: false,
            traffic: false,
            center: [35.6892, 51.3890],
            zoom: 11
        });

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
            style: feature => ({
                color: statusColors[feature.properties.status] || '#0d6efd',
                weight: 1,
                fillColor: statusColors[feature.properties.status] || '#0d6efd',
                fillOpacity: 0.35
            }),
            onEachFeature: (feature, layer) => {
                layer.on('click', () => showCellDetail(feature.properties.h3Index));
            }
        }).addTo(map);

        heatmapLayer = L.layerGroup().addTo(map);
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

        if (status) params.set('status', status);
        if (maxAge && Number(maxAge) > 0) params.set('maxAgeDays', maxAge);
        if (minPoi && Number(minPoi) > 0) params.set('minPoiCount', minPoi);
        if (category) params.set('categoryId', category);

        return params;
    }

    async function loadCells() {
        const params = buildParams();
        params.set('limit', '800');

        const res = await fetch(`${apiBase}/api/coverage/cells?${params}`);
        if (!res.ok) return;

        const geoJson = await res.json();
        let features = geoJson.features || [];

        const minPoi = document.getElementById('filter-min-poi').value;
        if (minPoi && Number(minPoi) > 0) {
            features = features.filter(f => (f.properties?.poiCount || 0) >= Number(minPoi));
        }

        cellLayer.clearLayers();
        cellLayer.addData({ type: 'FeatureCollection', features });
        heatmapLayer.clearLayers();

        if (features.length > 0) {
            const bounds = cellLayer.getBounds();
            if (bounds.isValid()) map.fitBounds(bounds, { padding: [20, 20] });
        }
    }

    async function loadHeatmap() {
        const params = buildParams();
        const res = await fetch(`${apiBase}/api/coverage/heatmap?${params}`);
        if (!res.ok) return;

        const points = await res.json();
        cellLayer.clearLayers();
        heatmapLayer.clearLayers();

        const bounds = [];
        points.forEach(p => {
            const radius = Math.min(24, 6 + Math.sqrt(p.weight));
            const color = statusColors[p.status] || '#0d6efd';
            const circle = L.circleMarker([p.lat, p.lng], {
                radius,
                fillColor: color,
                color: '#fff',
                weight: 1,
                fillOpacity: 0.55
            });
            circle.bindTooltip(`POI: ${p.weight}`, { direction: 'top' });
            circle.on('click', () => {
                if (p.h3Index) showCellDetail(p.h3Index);
            });
            heatmapLayer.addLayer(circle);
            bounds.push([p.lat, p.lng]);
        });

        if (bounds.length > 0) {
            map.fitBounds(bounds, { padding: [20, 20] });
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
    }

    document.getElementById('btn-reload').addEventListener('click', reload);
    document.getElementById('view-mode').addEventListener('change', reload);

    initMap();
    reload();
})();
