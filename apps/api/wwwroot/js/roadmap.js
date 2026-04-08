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
            const subsDoneCount = subs.filter(s => !!state['subtask-' + s.id]).length;
            const hasSubs = subs.length > 0;

            const subsHtml = hasSubs ? subs.map(sub => {
                const subKey = 'subtask-' + sub.id;
                const subDone = !!state[subKey];
                return `
        <div class="subtask-row${subDone ? ' done' : ''}" onclick="toggleSubtask(this)" data-idx="${subKey}">
          <div class="subtask-check"><span class="check-icon">✓</span></div>
          <span class="subtask-text">${sub.text}</span>
          <span class="task-hrs">${sub.hours}</span>
        </div>`;
            }).join('') : '';

            tasksHtml += `
        <div class="task-row${isDone ? ' done' : ''}" onclick="toggleTask(this)" data-idx="${stateKey}">
          <div class="task-check"><span class="check-icon">✓</span></div>
          <span class="task-type type-${task.type}">${task.type === 'pc' ? 'PC' : 'Physisch'}</span>
          <span class="task-text">${task.text}</span>
          <span class="task-hrs">${task.hours}</span>
          ${hasSubs ? `<button class="subtask-toggle-btn" onclick="event.stopPropagation();toggleSubtaskList(${task.id},this)" title="Unteraufgaben ein-/ausblenden"><span class="subtask-pill">${subsDoneCount}/${subs.length}</span></button>` : ''}
        </div>
        ${hasSubs ? `<div class="subtask-list" id="subtask-list-${task.id}" style="display:none">${subsHtml}</div>` : ''}`;
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

function toggleSubtaskList(taskId, btn) {
    const list = document.getElementById('subtask-list-' + taskId);
    if (!list) return;
    const open = list.style.display === 'none';
    list.style.display = open ? '' : 'none';
    btn.classList.toggle('open', open);
}

function toggleSubtask(row) {
    const key = row.dataset.idx;
    state[key] = !state[key];
    row.classList.toggle('done', !!state[key]);

    const list = row.closest('.subtask-list');
    if (!list) { saveState(); return; }

    const parentTaskId = parseInt(list.id.replace('subtask-list-', ''));

    // Update pill count
    const pill = document.querySelector(`.subtask-toggle-btn[onclick*="toggleSubtaskList(${parentTaskId},"] .subtask-pill`);
    if (pill) {
        const doneCount = list.querySelectorAll('.subtask-row.done').length;
        const totalCount = list.querySelectorAll('.subtask-row').length;
        pill.textContent = `${doneCount}/${totalCount}`;
    }

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
