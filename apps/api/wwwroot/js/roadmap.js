// ── KPIs (legacy – element may not exist) ──
function renderKpis() {
    const strip = document.getElementById('kpi-strip');
    if (!strip || !appData) return;
    const total = appData.weeks.reduce((s, w) => s + w.tasks.length, 0);
    const done = Object.values(state).filter(Boolean).length;
    const pct = total > 0 ? Math.round((done / total) * 100) : 0;
    const kpis = [
        { icon: '📊', val: pct + '%', label: 'Fortschritt', color: 'blue' },
        { icon: '✅', val: done, label: 'Erledigt', color: 'green' },
        { icon: '⏳', val: total - done, label: 'Offen', color: 'amber' },
        { icon: '📅', val: 'W' + window._currentWeek, label: 'Aktuelle Woche', color: 'purple' },
        { icon: '🚀', val: window._daysToLaunch > 0 ? window._daysToLaunch : '🎉', label: 'Tage bis Launch', color: 'red' }
    ];
    strip.innerHTML = kpis.map(k => `
    <div class="kpi-card ${k.color}">
      <div class="kpi-icon">${k.icon}</div>
      <div class="kpi-val">${k.val}</div>
      <div class="kpi-label">${k.label}</div>
    </div>`).join('');
}

// ── ROADMAP PROGRESS BAR ──
function renderRoadmapProgress() {
    const el = document.getElementById('rm-progress');
    if (!el || !appData) return;
    let total = 0, done = 0;
    appData.weeks.forEach(w => w.tasks.forEach(t => {
        total++;
        if (state['task-' + t.id]) done++;
    }));
    const pct = total > 0 ? Math.round((done / total) * 100) : 0;
    const color = pct >= 100 ? 'var(--green)' : pct >= 50 ? 'var(--accent)' : 'var(--amber)';
    el.innerHTML = `
        <div class="rm-prog-row">
            <span class="rm-prog-label">${done} / ${total} Tasks erledigt</span>
            <span class="rm-prog-pct" style="color:${color}">${pct}%</span>
            ${window._daysToLaunch > 0 ? `<span class="rm-prog-launch">🚀 ${window._daysToLaunch} Tage bis Launch</span>` : '<span class="rm-prog-launch">🎉 Launch!</span>'}
        </div>
        <div class="rm-bar-track"><div class="rm-bar-fill" style="width:${pct}%;background:${color}"></div></div>`;
}

// ── PHASE TABS ──
let _roadmapPhase = null;

function renderPhaseTabs() {
    const el = document.getElementById('rm-phase-tabs');
    if (!el || !appData) return;
    const phases = [...new Set(appData.weeks.map(w => w.phase))];
    if (phases.length <= 1) { el.innerHTML = ''; return; }
    el.innerHTML = `
        <button class="rm-phase-tab${_roadmapPhase === null ? ' active' : ''}" data-phase="__all__" onclick="setRoadmapPhase(null)">Alle</button>
        ${phases.map(p => `<button class="rm-phase-tab${_roadmapPhase === p ? ' active' : ''}" data-phase="${p}" onclick="setRoadmapPhase('${p.replace(/'/g, "\\'")}')">${p}</button>`).join('')}`;
}

function setRoadmapPhase(phase) {
    _roadmapPhase = phase;
    // Update tab active states
    document.querySelectorAll('.rm-phase-tab').forEach(btn => {
        const isAll = btn.dataset.phase === '__all__';
        btn.classList.toggle('active', phase === null ? isAll : btn.dataset.phase === phase);
    });
    // Show/hide week cards and phase headers
    document.querySelectorAll('.week-card').forEach(card => {
        card.style.display = (!phase || card.dataset.phase === phase) ? '' : 'none';
    });
    document.querySelectorAll('.phase-hdr').forEach(hdr => {
        hdr.style.display = phase ? 'none' : '';
    });
}

// ── ROADMAP ──
function renderRoadmap() {
    const container = document.getElementById('roadmap-content');
    if (!container || !appData) return;
    container.innerHTML = '';
    let lastPhase = '';
    const weekRanges = getWeekRanges();
    const currentWeek = window._currentWeek ?? 1;

    appData.weeks.forEach(week => {
        if (week.phase !== lastPhase) {
            lastPhase = week.phase;
            const ph = document.createElement('div');
            ph.className = 'phase-hdr';
            ph.innerHTML = `<div class="phase-tag">${week.phase}</div><div class="phase-line"></div>`;
            container.appendChild(ph);
        }

        const isCurrentWeek = week.number === currentWeek;
        const card = document.createElement('div');
        card.className = 'week-card';
        card.id = 'week-' + week.number;
        card.dataset.phase = week.phase;
        if (_roadmapPhase && week.phase !== _roadmapPhase) card.style.display = 'none';

        let tasksHtml = '';
        week.tasks.forEach(task => {
            const stateKey = 'task-' + task.id;
            const isDone = !!state[stateKey];
            const subs = task.subtasks ?? [];
            const hasSubs = subs.length > 0;

            const subsHtml = hasSubs ? `<div class="subtask-list" id="subtask-list-${task.id}">` +
                subs.map(sub => {
                    const subKey = 'subtask-' + sub.id;
                    const subDone = !!state[subKey];
                    return `
        <div class="subtask-row${subDone ? ' done' : ''}" onclick="toggleSubtask(this)" data-idx="${subKey}">
          <div class="subtask-check"><span class="check-icon">✓</span></div>
          <span class="subtask-text">${sub.text}</span>
          <span class="task-hrs">${sub.hours}</span>
        </div>`;
                }).join('') + `</div>` : '';

            const tags = task.tags ?? [];
            const tagsHtml = tags.length > 0
                ? tags.map(t => `<span class="task-tag-chip" style="background:${t.color}22;color:${t.color}">${t.name}</span>`).join('')
                : (task.type ? `<span class="task-tag-chip" style="background:var(--border2);color:var(--text3)">${task.type === 'pc' ? 'PC' : task.type === 'phys' ? 'Physisch' : task.type}</span>` : '');

            tasksHtml += `
        <div class="task-row${isDone ? ' done' : ''}" onclick="toggleTask(this)" data-idx="${stateKey}">
          <div class="task-check"><span class="check-icon">✓</span></div>
          <div class="task-tags-wrap">${tagsHtml}</div>
          <span class="task-text">${task.text}</span>
          <span class="task-hrs">${task.hours}</span>
        </div>
        ${subsHtml}`;
        });

        const wIdx = week.number - 1;
        const startFmt = wIdx < weekRanges.length ? fmt(weekRanges[wIdx].start) : '';
        const endFmt   = wIdx < weekRanges.length ? fmt(weekRanges[wIdx].end) : '';

        card.innerHTML = `
      <div class="wc-header" onclick="toggleWeek(${week.number})">
        <span class="week-num">W${String(week.number).padStart(2, '0')}</span>
        <div class="wc-title-wrap">
          <div class="week-title">${week.title}${isCurrentWeek ? ' <span class="wc-current-badge">Aktuell</span>' : ''}</div>
          <div class="week-dl">${startFmt} – ${endFmt}</div>
        </div>
        <span class="week-prog" id="wp-${week.number}">0 / ${week.tasks.length}</span>
        <div class="badges">
          <span class="badge badge-pc">${week.badgePc}</span>
          <span class="badge badge-ph">${week.badgePhys}</span>
        </div>
        <span class="chev${isCurrentWeek ? ' open' : ''}" id="chev-${week.number}">▶</span>
      </div>
      <div class="week-body${isCurrentWeek ? ' open' : ''}" id="wb-${week.number}">
        ${tasksHtml || '<div style="padding:14px 18px;font-size:13px;color:var(--text3)">Keine Tasks.</div>'}
        ${week.note ? `<div class="week-note">${week.note}</div>` : ''}
      </div>`;
        container.appendChild(card);
    });

    renderPhaseTabs();
    renderRoadmapProgress();
}

function toggleSubtask(row) {
    const key = row.dataset.idx;
    state[key] = !state[key];
    row.classList.toggle('done', !!state[key]);

    const list = row.closest('.subtask-list');
    if (!list) { saveState(); return; }

    const parentTaskId = parseInt(list.id.replace('subtask-list-', ''));

    // Auto-check / auto-uncheck parent task
    const task = findTask(parentTaskId);
    const subs = task?.subtasks ?? [];
    if (subs.length > 0) {
        const allDone = subs.every(s => !!state['subtask-' + s.id]);
        const taskKey = 'task-' + parentTaskId;
        const taskRow = document.querySelector(`.task-row[data-idx="${taskKey}"]`);
        if (taskRow) {
            const taskCurrentlyDone = taskRow.classList.contains('done');
            if (allDone && !taskCurrentlyDone) {
                taskRow.classList.add('done');
                state[taskKey] = true;
            } else if (!allDone && taskCurrentlyDone) {
                taskRow.classList.remove('done');
                state[taskKey] = false;
            }
        }
    }

    saveState();
    updateAll();
    updateOverdueBanner();
}

function getDashboardRoleVariant() {
    const projectRole = String(_currentProject?.role ?? '').toLowerCase();
    const isAdmin = !!_currentUser?.isPlatformAdmin || projectRole === 'admin';
    return isAdmin ? 'admin' : 'member';
}

function applyDashboardRoleLayout() {
    const page = document.getElementById('page-overview');
    if (!page) return;
    const variant = getDashboardRoleVariant();
    page.classList.toggle('dashboard-admin', variant === 'admin');
    page.classList.toggle('dashboard-member', variant !== 'admin');
}

function renderOverviewAlerts() {
    const list = document.getElementById('overview-alerts-list');
    const count = document.getElementById('overview-alerts-count');
    if (!list || !count) return;

    const overdue = (typeof getOverdueTasks === 'function') ? getOverdueTasks() : [];
    const openTasks = appData?.weeks?.reduce((acc, w) =>
        acc + w.tasks.filter(t => !state[`task-${t.id}`]).length, 0) ?? 0;
    const days = window._daysToLaunch ?? 0;

    const items = [];
    if (overdue.length > 0) {
        items.push({
            level: 'critical',
            title: `${overdue.length} überfällige Aufgaben`,
            sub: 'Aus vergangenen Wochen sind Aufgaben offen.',
            meta: 'Roadmap'
        });
    }
    if (days > 0 && days <= 14) {
        items.push({
            level: 'warn',
            title: `Launch in ${days} Tagen`,
            sub: 'Fokus auf kritische Restaufgaben.',
            meta: 'Timeline'
        });
    }
    if (openTasks > 0) {
        items.push({
            level: overdue.length > 0 ? 'warn' : 'ok',
            title: `${openTasks} offene Aufgaben gesamt`,
            sub: 'Diese Zahl sinkt mit jedem abgeschlossenen Task.',
            meta: 'Fortschritt'
        });
    }

    count.textContent = String(items.filter(i => i.level !== 'ok').length);
    if (items.length === 0) {
        list.innerHTML = `
            <div class="overview-list-item">
                <div class="overview-item-main">
                    <div class="overview-item-title">Keine kritischen Risiken</div>
                    <div class="overview-item-sub">Alles im grünen Bereich.</div>
                </div>
                <span class="status-chip ok">OK</span>
            </div>`;
        return;
    }

    list.innerHTML = items.map(item => `
        <div class="overview-list-item ${item.level === 'critical' ? 'critical' : (item.level === 'warn' ? 'warn' : '')}">
            <div class="overview-item-main">
                <div class="overview-item-title">${escHtml(item.title)}</div>
                <div class="overview-item-sub">${escHtml(item.sub)}</div>
            </div>
            <div class="overview-item-meta">
                <span class="status-chip ${item.level === 'critical' ? 'critical' : (item.level === 'warn' ? 'warning' : 'ok')}">${item.level === 'critical' ? 'kritisch' : (item.level === 'warn' ? 'warnung' : 'ok')}</span>
                <div>${escHtml(item.meta)}</div>
            </div>
        </div>`).join('');
}

function renderOverviewTimeline(currentWeek, weekRanges) {
    const list = document.getElementById('overview-timeline-list');
    if (!list || !appData?.weeks?.length) return;

    const upcomingWeeks = appData.weeks
        .filter(w => w.number >= currentWeek)
        .slice(0, 4)
        .map(w => {
            const idx = w.number - 1;
            const dateStr = idx < weekRanges.length
                ? `${fmtShort(weekRanges[idx].start)} – ${fmtShort(weekRanges[idx].end)}`
                : '';
            const done = w.tasks.filter(t => !!state[`task-${t.id}`]).length;
            const total = w.tasks.length;
            const pct = total > 0 ? Math.round((done / total) * 100) : 0;
            return { title: `W${String(w.number).padStart(2, '0')} · ${w.title}`, dateStr, pct, done, total };
        });

    list.innerHTML = upcomingWeeks.map(w => `
        <div class="overview-list-item">
            <div class="overview-item-main">
                <div class="overview-item-title">${escHtml(w.title)}</div>
                <div class="overview-item-sub">${escHtml(w.dateStr)}</div>
            </div>
            <div class="overview-item-meta">${w.done}/${w.total} · ${w.pct}%</div>
        </div>`).join('');
}

function renderOverviewFinanceSnapshot() {
    const el = document.getElementById('overview-finance-summary');
    if (!el || !financeData) return;
    const currency = getCurrency();
    const income = financeData.totalIncome ?? 0;
    const expenses = financeData.totalExpenses ?? 0;
    const balance = financeData.netBalance ?? (income - expenses);
    const monthKey = today.toISOString().slice(0, 7);
    const monthEntries = (financeData.expenses ?? []).filter(e => (e.date ?? '').startsWith(monthKey));
    const monthExpenses = monthEntries.filter(e => (e.type ?? 'expense') === 'expense').length;
    const monthIncome = monthEntries.filter(e => (e.type ?? 'expense') === 'income').length;

    el.innerHTML = `
        <div class="overview-fin-row">
            <div class="overview-fin-label">Bilanz</div>
            <div class="overview-fin-value ${balance >= 0 ? 'pos' : 'neg'}">${balance >= 0 ? '+' : '−'}${currency} ${fmtChf(Math.abs(balance))}</div>
        </div>
        <div class="overview-fin-row">
            <div class="overview-fin-label">Einnahmen</div>
            <div class="overview-fin-value pos">${currency} ${fmtChf(income)}</div>
        </div>
        <div class="overview-fin-row">
            <div class="overview-fin-label">Ausgaben</div>
            <div class="overview-fin-value neg">${currency} ${fmtChf(expenses)}</div>
        </div>
        <div class="overview-fin-row">
            <div class="overview-fin-label">Buchungen im Monat</div>
            <div class="overview-fin-value">${monthIncome + monthExpenses} (${monthIncome} Ein / ${monthExpenses} Aus)</div>
        </div>`;
}

async function renderOverviewActivity() {
    const el = document.getElementById('overview-activity-list');
    if (!el) return;
    try {
        const entries = await api(withProject('/api/activity?limit=8'));
        if (!entries || entries.length === 0) {
            el.innerHTML = `
                <div class="overview-list-item">
                    <div class="overview-item-main">
                        <div class="overview-item-title">Noch keine Aktivität</div>
                        <div class="overview-item-sub">Neue Änderungen erscheinen hier automatisch.</div>
                    </div>
                </div>`;
            return;
        }
        el.innerHTML = entries.map(e => `
            <div class="overview-list-item">
                <div class="overview-item-main">
                    <div class="overview-item-title">${escHtml(e.title ?? 'Aktivität')}</div>
                    <div class="overview-item-sub">${escHtml(e.description ?? '')}</div>
                </div>
                <div class="overview-item-meta">${escHtml((e.createdAt ?? '').slice(0, 16).replace('T', ' '))}</div>
            </div>`).join('');
    } catch {
        el.innerHTML = `
            <div class="overview-list-item">
                <div class="overview-item-main">
                    <div class="overview-item-title">Aktivität konnte nicht geladen werden</div>
                    <div class="overview-item-sub">Bitte später erneut versuchen.</div>
                </div>
            </div>`;
    }
}
