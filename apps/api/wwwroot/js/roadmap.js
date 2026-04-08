// ── KPIs ──
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
    <div class="kpi-card ${k.color}" onclick="showPage('roadmap')" style="cursor:pointer">
      <div class="kpi-icon">${k.icon}</div>
      <div class="kpi-val">${k.val}</div>
      <div class="kpi-label">${k.label}</div>
    </div>`).join('');
}

// ── ROADMAP ──
function renderRoadmap() {
    const container = document.getElementById('roadmap-content');
    if (!container || !appData) return;
    container.innerHTML = '';
    let lastPhase = '';
    const weekRanges = getWeekRanges();

    appData.weeks.forEach(week => {
        if (week.phase !== lastPhase) {
            lastPhase = week.phase;
            const el = document.createElement('div');
            el.className = 'phase-hdr';
            el.innerHTML = `<div class="phase-tag">${week.phase}</div><div class="phase-line"></div>`;
            container.appendChild(el);
        }

        const card = document.createElement('div');
        card.className = 'week-card';
        card.id = 'week-' + week.number;

        let tasksHtml = '';
        week.tasks.forEach(task => {
            const stateKey = 'task-' + task.id;
            const isDone = !!state[stateKey];
            tasksHtml += `
        <div class="task-row${isDone ? ' done' : ''}" onclick="toggleTask(this)" data-idx="${stateKey}">
          <div class="task-check"><span class="check-icon">✓</span></div>
          <span class="task-type type-${task.type}">${task.type === 'pc' ? 'PC' : 'Physisch'}</span>
          <span class="task-text">${task.text}</span>
          <span class="task-hrs">${task.hours}</span>
        </div>`;
        });

        const wIdx = week.number - 1;
        const startFmt = wIdx < weekRanges.length ? fmt(weekRanges[wIdx].start) : '';
        const endFmt = wIdx < weekRanges.length ? fmt(weekRanges[wIdx].end) : '';

        card.innerHTML = `
      <div class="wc-header" onclick="toggleWeek(${week.number})">
        <span class="week-num">W0${week.number}</span>
        <div>
          <div class="week-title">${week.title}</div>
          <div class="week-dl">${startFmt} – ${endFmt}</div>
        </div>
        <span class="week-prog" id="wp-${week.number}">0 / ${week.tasks.length}</span>
        <div class="badges">
          <span class="badge badge-pc">${week.badgePc}</span>
          <span class="badge badge-ph">${week.badgePhys}</span>
        </div>
        <span class="chev" id="chev-${week.number}">▶</span>
      </div>
      <div class="week-body" id="wb-${week.number}">
        ${tasksHtml}
        ${week.note ? `<div class="week-note">${week.note}</div>` : ''}
      </div>`;
        container.appendChild(card);
    });
}
