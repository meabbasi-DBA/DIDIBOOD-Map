(function () {
    if (window.__dashboardCrawlCleanup) {
        window.__dashboardCrawlCleanup();
    }

    const dashboardPollMs = 3000;
    const badgeOnlyPollMs = 15000;
    const csrfToken = document.querySelector('meta[name="csrf-token"]')?.content || '';
    let pollTimer;
    let isRefreshing = false;

    const els = {
        badge: document.getElementById('crawl-status-badge'),
        topBadge: document.getElementById('topnav-crawl-badge'),
        idle: document.getElementById('crawl-idle-state'),
        active: document.getElementById('crawl-active-state'),
        jobName: document.getElementById('crawl-job-name'),
        startedAt: document.getElementById('crawl-started-at'),
        statusLabel: document.getElementById('crawl-status-label'),
        requests: document.getElementById('crawl-requests'),
        cells: document.getElementById('crawl-cells'),
        tasksPlanned: document.getElementById('crawl-tasks-planned'),
        progressBar: document.getElementById('crawl-progress-bar'),
        reqSpeed: document.getElementById('crawl-req-speed'),
        cellSpeed: document.getElementById('crawl-cell-speed'),
        newPoi: document.getElementById('crawl-new-poi'),
        updatedPoi: document.getElementById('crawl-updated-poi'),
        liveError: document.getElementById('crawl-live-error'),
        liveErrorText: document.getElementById('crawl-live-error-text'),
        message: document.getElementById('crawl-message'),
        runningJobs: document.getElementById('stat-running-jobs'),
        h3Success: document.getElementById('stat-h3-success'),
        h3Crawled: document.getElementById('stat-h3-crawled'),
        h3Remaining: document.getElementById('stat-h3-remaining'),
        h3SuccessRate: document.getElementById('stat-h3-success-rate'),
        schedulerState: document.getElementById('crawl-scheduler-state'),
        apiUsed: document.getElementById('crawl-api-used'),
        apiRemaining: document.getElementById('crawl-api-remaining'),
        currentGrid: document.getElementById('crawl-current-grid'),
        currentGridDetail: document.getElementById('crawl-current-grid-detail'),
        nextGrids: document.getElementById('crawl-next-grids'),
        lastTimeline: document.getElementById('crawl-last-timeline'),
        gridRows: document.getElementById('dashboard-grid-rows'),
        workerBadge: document.getElementById('worker-status-badge'),
        btnStart: document.getElementById('btn-crawl-start'),
        btnPause: document.getElementById('btn-crawl-pause'),
        btnResume: document.getElementById('btn-crawl-resume'),
        btnStop: document.getElementById('btn-crawl-stop')
    };

    const hasDashboard = Boolean(els.btnStart);
    const hasTopBadge = Boolean(els.topBadge);
    if (!hasDashboard && !hasTopBadge) return;

    const pollMs = hasDashboard ? dashboardPollMs : badgeOnlyPollMs;

    window.__dashboardCrawlCleanup = function () {
        if (pollTimer) {
            clearInterval(pollTimer);
            pollTimer = null;
        }
        document.removeEventListener('visibilitychange', handleVisibilityChange);
    };

    const statusLabels = {
        queued: 'در صف Worker',
        running: 'در حال اجرا',
        paused: 'مکث'
    };

    async function postAction(handler) {
        const res = await fetch(`/Index?handler=${handler}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': csrfToken,
                'Content-Type': 'application/json'
            }
        });
        return res.json();
    }

    async function refresh() {
        if (document.hidden || isRefreshing) return;
        isRefreshing = true;
        const started = performance.now();
        try {
            const endpoint = '/Index?handler=CrawlStatus';
            const res = await fetch(endpoint, { headers: { 'Accept': 'application/json' } });
            if (!res.ok) return;
            const data = await res.json();
            render(data);
            window.coverageTraceEvent?.('crawl_status_polled', {
                endpoint,
                status: String(res.status),
                details: `requestMs=${(performance.now() - started).toFixed(1)};isActive=${Boolean(data.isActive)};currentGrid=${data.currentGrid ?? ''};queued=${data.queuedGrids?.length ?? 0};remaining=${data.apiCallsRemainingThisHour ?? ''}`
            });
        } catch (err) {
            console.error('Crawl status poll failed', err);
        } finally {
            isRefreshing = false;
        }
    }

    function render(data) {
        const status = data.status || '';
        const isRunning = status === 'running';
        const isPaused = status === 'paused';
        const isQueued = status === 'queued';
        const isActive = Boolean(data.isActive);

        if (els.runningJobs) els.runningJobs.textContent = data.runningCount ?? 0;
        if (els.h3Success && data.h3Success != null) els.h3Success.textContent = Number(data.h3Success).toLocaleString();
        if (els.h3Crawled && data.h3Crawled != null) els.h3Crawled.textContent = Number(data.h3Crawled).toLocaleString();
        if (els.h3Remaining && data.h3Remaining != null) els.h3Remaining.textContent = Number(data.h3Remaining).toLocaleString();
        if (els.h3SuccessRate && data.h3SuccessRate != null) els.h3SuccessRate.textContent = `${Number(data.h3SuccessRate).toLocaleString()}%`;
        if (els.schedulerState) els.schedulerState.textContent = data.schedulerState || 'idle';
        if (els.apiUsed && data.apiCallsUsedThisHour != null) els.apiUsed.textContent = Number(data.apiCallsUsedThisHour).toLocaleString();
        if (els.apiRemaining && data.apiCallsRemainingThisHour != null) els.apiRemaining.textContent = Number(data.apiCallsRemainingThisHour).toLocaleString();
        renderCurrentGrid(data);
        renderNextGrids(data.nextQueuedGrids || []);
        renderTimeline(data.lastCrawl);
        renderGridRows(data.dashboardGrids || []);
        if (els.workerBadge) {
            els.workerBadge.textContent = isRunning ? 'فعال' : (data.pausedCount > 0 ? 'مکث' : (isQueued ? 'در صف' : 'آماده'));
            els.workerBadge.className = 'badge ' + (isRunning ? 'bg-success' : (data.pausedCount > 0 ? 'bg-warning' : (isQueued ? 'bg-info' : 'bg-secondary')));
        }

        updateBadge(els.badge, data);
        updateBadge(els.topBadge, data);

        if (!hasDashboard) return;

        if (!isActive) {
            els.idle.style.display = '';
            els.active.style.display = 'none';
            if (els.liveError) els.liveError.style.display = 'none';
            els.btnStart.disabled = (data.runningCount ?? 0) > 0;
            els.btnPause.disabled = true;
            els.btnStop.disabled = true;
            if (els.btnResume) {
                els.btnResume.style.display = (data.pausedCount ?? 0) > 0 ? '' : 'none';
                els.btnResume.disabled = (data.pausedCount ?? 0) === 0;
            }
            return;
        }

        els.idle.style.display = 'none';
        els.active.style.display = 'block';
        els.jobName.textContent = data.jobName || '—';
        els.startedAt.textContent = data.startedAt
            ? `شروع: ${new Date(data.startedAt).toLocaleString('fa-IR')}`
            : '—';

        if (els.statusLabel) {
            els.statusLabel.textContent = statusLabels[status] || status;
            els.statusLabel.className = 'badge ' + (
                isRunning ? 'bg-danger' : (isPaused ? 'bg-warning text-dark' : (isQueued ? 'bg-info' : 'bg-secondary'))
            );
        }

        els.requests.textContent = Number(data.requestCount || 0).toLocaleString();
        els.cells.textContent = Number(data.cellsProcessed || 0).toLocaleString();
        els.newPoi.textContent = Number(data.newRecords || 0).toLocaleString();
        if (els.updatedPoi) els.updatedPoi.textContent = Number(data.updatedRecords || 0).toLocaleString();

        if (els.liveError && els.liveErrorText) {
            if (data.liveError) {
                els.liveError.style.display = '';
                els.liveErrorText.textContent = data.liveError;
            } else {
                els.liveError.style.display = 'none';
                els.liveErrorText.textContent = '';
            }
        }

        if (els.tasksPlanned) {
            const done = Number(data.tasksDone ?? (data.cellsProcessed || 0) + (data.cellsFailed || 0));
            const total = Number(data.totalTasksPlanned || 0);
            els.tasksPlanned.textContent = total > 0
                ? `${done.toLocaleString()} / ${total.toLocaleString()} وظیفه`
                : (isQueued ? 'در انتظار Worker…' : '');
        }

        const pct = data.progressPercent || 0;
        els.progressBar.style.width = `${Math.max(isActive && pct === 0 && isRunning ? 2 : pct, 0)}%`;
        els.progressBar.textContent = data.totalTasksPlanned
            ? `${pct}%`
            : (isQueued ? '…' : '');

        els.reqSpeed.textContent = `${data.requestsPerMinute || 0} /min`;
        els.cellSpeed.textContent = `${data.cellsPerMinute || 0} /min`;

        els.btnStart.disabled = isRunning || isPaused || isQueued;
        els.btnPause.disabled = !isRunning;
        els.btnStop.disabled = !isRunning && !isPaused && !isQueued;
        if (els.btnResume) {
            els.btnResume.style.display = isPaused ? '' : 'none';
            els.btnResume.disabled = !isPaused;
        }
    }

    function renderCurrentGrid(data) {
        if (!els.currentGrid) return;

        if (!data.currentGrid) {
            els.currentGrid.textContent = '—';
            if (els.currentGridDetail) els.currentGridDetail.textContent = '';
            return;
        }

        const gridNumber = data.currentGridNumber ? `#${data.currentGridNumber}` : '—';
        els.currentGrid.textContent = gridNumber;
        if (els.currentGridDetail) {
            const parts = [
                `H3 ${data.currentGrid}`,
                data.currentGridCategoryId ? `category ${data.currentGridCategoryId}` : '',
                data.currentGridSearchTerm || ''
            ].filter(Boolean);
            els.currentGridDetail.textContent = parts.join(' — ');
        }
    }

    function renderNextGrids(rows) {
        if (!els.nextGrids) return;
        if (!rows.length) {
            els.nextGrids.innerHTML = '<span class="text-muted">هیچ Grid واجدی یافت نشد.</span>';
            return;
        }

        els.nextGrids.innerHTML = rows.slice(0, 5).map(row => {
            const grid = row.gridNumber == null ? '—' : `#${row.gridNumber}`;
            return `<div>Grid ${grid} — H3 ${row.h3Index} — ${row.priority}</div>`;
        }).join('');
    }

    function renderTimeline(row) {
        if (!els.lastTimeline) return;
        if (!row) {
            els.lastTimeline.innerHTML = '<span class="text-muted">هنوز Crawl ثبت نشده</span>';
            return;
        }

        const grid = row.gridNumber == null ? '—' : `#${row.gridNumber}`;
        const timestamp = row.timestamp ? new Date(row.timestamp).toLocaleString('fa-IR') : '—';
        const duration = row.durationMs == null ? '—' : `${Number(row.durationMs).toLocaleString()} ms`;
        els.lastTimeline.innerHTML = [
            `<div>Last Crawled Grid Number: ${grid} — H3 ${row.h3Index}</div>`,
            `<div>Last Crawled Datetime: ${timestamp}</div>`,
            `<div>Duration: ${duration}</div>`,
            `<div>Status: ${row.status || '—'}</div>`
        ].join('');
    }

    function renderGridRows(rows) {
        if (!els.gridRows) return;
        if (!rows.length) {
            els.gridRows.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-3">No grid rows available.</td></tr>';
            return;
        }

        els.gridRows.innerHTML = rows.map(row => {
            const grid = row.gridNumber == null ? '—' : row.gridNumber;
            const last = row.lastCrawlTime ? new Date(row.lastCrawlTime).toLocaleString('fa-IR') : '—';
            const score = row.coverageScore == null ? '0.000' : Number(row.coverageScore).toFixed(3);
            return [
                '<tr>',
                `<td>${grid}</td>`,
                `<td>${row.status || 'Never Crawled'}</td>`,
                `<td>${last}</td>`,
                `<td>${Number(row.poiCount || 0).toLocaleString()}</td>`,
                `<td>${score}</td>`,
                '</tr>'
            ].join('');
        }).join('');
    }

    function updateBadge(el, data) {
        if (!el) return;
        const status = data.status || '';
        if (status === 'running') {
            el.className = 'badge bg-danger d-flex align-items-center gap-1';
            el.innerHTML = `<i class="bi bi-circle-fill" style="font-size:.5rem"></i>Crawl ${data.progressPercent || 0}%`;
        } else if (status === 'queued') {
            el.className = 'badge bg-info';
            el.textContent = 'Crawl در صف';
        } else if (status === 'paused') {
            el.className = 'badge bg-warning';
            el.textContent = 'Crawl متوقف';
        } else if ((data.runningCount ?? 0) > 0) {
            el.className = 'badge bg-danger';
            el.textContent = `${data.runningCount} در حال اجرا`;
        } else {
            el.className = 'badge bg-secondary';
            el.textContent = 'بدون Crawl';
        }
    }

    els.btnStart?.addEventListener('click', async () => {
        const r = await postAction('StartCrawl');
        if (els.message) els.message.textContent = r.message || '';
        await refresh();
    });

    els.btnStop?.addEventListener('click', async () => {
        const r = await postAction('StopCrawl');
        if (els.message) els.message.textContent = r.message || '';
        await refresh();
    });

    els.btnPause?.addEventListener('click', async () => {
        const r = await postAction('PauseCrawl');
        if (els.message) els.message.textContent = r.message || '';
        await refresh();
    });

    els.btnResume?.addEventListener('click', async () => {
        const r = await postAction('ResumeCrawl');
        if (els.message) els.message.textContent = r.message || '';
        await refresh();
    });

    function handleVisibilityChange() {
        if (!document.hidden) void refresh();
    }

    refresh();
    pollTimer = setInterval(refresh, pollMs);
    document.addEventListener('visibilitychange', handleVisibilityChange);
})();
