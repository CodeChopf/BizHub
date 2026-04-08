// ── TOGGLE TASK ──
function toggleTask(row) {
    row.classList.toggle('done');
    state[row.dataset.idx] = row.classList.contains('done');
    saveState();
    updateAll();
    updateOverdueBanner();
}

// ── TOGGLE WEEK ──
function toggleWeek(n) {
    const body = document.getElementById('wb-' + n);
    const chev = document.getElementById('chev-' + n);
    if (body) body.classList.toggle('open');
    if (chev) chev.classList.toggle('open');
}

// ── NAV ──
function showPage(id) {
    document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
    document.querySelectorAll('.nav-item').forEach(t => t.classList.remove('active'));
    const page = document.getElementById('page-' + id);
    if (!page) return;
    page.classList.add('active');
    // Highlight nav item by data-page or matching onclick
    const navBtn = document.getElementById('nav-' + id);
    if (navBtn) navBtn.classList.add('active');
    else {
        // fallback for overview / einstellungen which have no nav-id
        document.querySelectorAll('.nav-item').forEach(btn => {
            if (btn.getAttribute('onclick') === `showPage('${id}')`) btn.classList.add('active');
        });
    }
    if (id === 'admin') renderAdmin();
    if (id === 'meilensteine') renderMilestones();
    if (id === 'produkte') renderProdukte();
    if (id === 'produktion') renderProduktion();
    if (id === 'kalender') renderKalender();
    if (id === 'einstellungen') { renderMemberList(); loadAppVersion(); }
}

// ── UPDATE ALL ──
function updateAll() {
    if (!appData) return;
    const allTasks = document.querySelectorAll('.task-row');
    const total = allTasks.length;
    const done = document.querySelectorAll('.task-row.done').length;
    const pct = total > 0 ? Math.round((done / total) * 100) : 0;

    const circumference = 2 * Math.PI * 50;
    const filled = (pct / 100) * circumference;
    document.getElementById('donut-fill')?.setAttribute('stroke-dasharray', `${filled} ${circumference}`);
    const donutPct = document.getElementById('donut-pct');
    if (donutPct) donutPct.textContent = pct + '%';
    const statDone = document.getElementById('stat-done');
    if (statDone) statDone.textContent = done;
    const statOpen = document.getElementById('stat-open');
    if (statOpen) statOpen.textContent = total - done;
    const statWeeks = document.getElementById('stat-weeks');
    if (statWeeks) statWeeks.textContent = appData.weeks.length;

    renderKpis();

    appData.weeks.forEach(week => {
        const body = document.getElementById('wb-' + week.number);
        if (!body) return;
        const wTasks = body.querySelectorAll('.task-row');
        const wDone = body.querySelectorAll('.task-row.done').length;
        const el = document.getElementById('wp-' + week.number);
        if (el) el.textContent = wDone + ' / ' + wTasks.length;
        const card = document.getElementById('week-' + week.number);
        if (!card) return;
        card.classList.remove('complete', 'wc-active');
        if (wDone === wTasks.length && wTasks.length > 0) card.classList.add('complete');
        else if (wDone > 0) card.classList.add('wc-active');
    });

    updateOverview();
    updateDashboardCards();
}

function updateOverview() {
    if (!appData) return;
    const currentWeek = window._currentWeek ?? 1;
    const week = appData.weeks.find(w => w.number === currentWeek);
    const weekRanges = getWeekRanges();
    if (!week) {
        const cwBadge = document.getElementById('cw-badge');
        if (cwBadge) cwBadge.textContent = '';
        const cwTitle = document.getElementById('cw-title');
        if (cwTitle) cwTitle.textContent = '';
        const cwDate = document.getElementById('cw-date');
        if (cwDate) cwDate.textContent = '';
        const cwTasks = document.getElementById('cw-tasks');
        if (cwTasks) cwTasks.innerHTML = '';
        const weeksGrid = document.getElementById('weeks-grid');
        if (weeksGrid) weeksGrid.innerHTML = '';
        return;
    }

    const cwBadgeEl = document.getElementById('cw-badge');
    if (cwBadgeEl) cwBadgeEl.textContent = 'Woche ' + currentWeek;
    const cwTitleEl = document.getElementById('cw-title');
    if (cwTitleEl) cwTitleEl.textContent = week.title;
    const wIdx = currentWeek - 1;
    if (wIdx < weekRanges.length) {
        const cwDateEl = document.getElementById('cw-date');
        if (cwDateEl) cwDateEl.textContent = fmt(weekRanges[wIdx].start) + ' – ' + fmt(weekRanges[wIdx].end);
    }

    const cwBody = document.getElementById('wb-' + currentWeek);
    const cwTasksEl = document.getElementById('cw-tasks');
    if (cwTasksEl) cwTasksEl.innerHTML = '';

    if (cwBody && cwTasksEl) {
        cwBody.querySelectorAll('.task-row').forEach(row => {
            const isDone = row.classList.contains('done');
            const type = row.querySelector('.task-type').textContent.trim();
            const text = row.querySelector('.task-text').textContent.trim();
            const hrs = row.querySelector('.task-hrs').textContent.trim();
            const div = document.createElement('div');
            div.className = 'cw-task' + (isDone ? ' done' : '');
            div.innerHTML = `
        <div class="task-dot ${type === 'PC' ? 'dot-pc' : 'dot-phys'}"></div>
        <span style="flex:1">${text}</span>
        <span style="font-size:11px;color:var(--text3);flex-shrink:0;padding-left:10px">${hrs}</span>`;
            div.onclick = (e) => {
                e.stopPropagation();
                showPage('roadmap');
                setTimeout(() => {
                    const wb = document.getElementById('wb-' + currentWeek);
                    const ch = document.getElementById('chev-' + currentWeek);
                    if (wb && !wb.classList.contains('open')) { wb.classList.add('open'); if (ch) ch.classList.add('open'); }
                }, 100);
            };
            cwTasksEl.appendChild(div);
        });
    }

    const grid = document.getElementById('weeks-grid');
    if (!grid) return;
    grid.innerHTML = '';
    appData.weeks.forEach(week => {
        const w = week.number;
        const body = document.getElementById('wb-' + w);
        const wTasks = body ? body.querySelectorAll('.task-row') : [];
        const wDone = body ? body.querySelectorAll('.task-row.done').length : 0;
        const pct = wTasks.length > 0 ? Math.round((wDone / wTasks.length) * 100) : 0;
        const isCurrent = w === currentWeek;
        const isComplete = pct === 100 && wTasks.length > 0;
        const isActive = wDone > 0 && !isComplete;

        let cls = 'week-tile';
        if (isCurrent) cls += ' wt-current';
        else if (isComplete) cls += ' wt-complete';
        else if (isActive) cls += ' wt-active';

        const wIdx2 = w - 1;
        const startStr = wIdx2 < weekRanges.length ? fmtShort(weekRanges[wIdx2].start) : '';
        const endStr = wIdx2 < weekRanges.length ? fmtShort(weekRanges[wIdx2].end) : '';

        const tile = document.createElement('div');
        tile.className = cls;
        tile.innerHTML = `
      <div class="wt-num">W0${w}${isCurrent ? ' · Jetzt' : ''}</div>
      <div class="wt-title">${week.title}</div>
      <div class="wt-bar-track">
        <div class="wt-bar-fill ${isActive ? 'partial' : ''}" style="width:${pct}%"></div>
      </div>
      <div class="wt-meta"><span>${wDone}/${wTasks.length} Tasks</span><span>${pct}%</span></div>
      <div class="wt-date">${startStr} – ${endStr}</div>`;
        tile.onclick = () => { showPage('roadmap'); setTimeout(() => toggleWeek(w), 100); };
        grid.appendChild(tile);
    });
}

// ── DASHBOARD CARDS (sync) ──
function updateDashboardCards() {
    // Roadmap
    if (appData) {
        const total = appData.weeks.reduce((s, w) => s + w.tasks.length, 0);
        const done  = Object.values(state).filter(Boolean).length;
        const pct   = total > 0 ? Math.round((done / total) * 100) : 0;
        const week  = appData.weeks.find(w => w.number === (window._currentWeek ?? 1));
        const progEl  = document.getElementById('dash-roadmap-progress');
        const weekEl  = document.getElementById('dash-roadmap-week');
        const tasksEl = document.getElementById('dash-roadmap-tasks');
        if (progEl)  progEl.textContent  = pct + '%';
        if (weekEl)  weekEl.textContent  = week ? week.title : 'Woche ' + (window._currentWeek ?? 1);
        if (tasksEl) tasksEl.textContent = done + ' / ' + total + ' Tasks';
    }
    // Finanzen
    if (financeData) {
        const currency  = getCurrency();
        const income    = financeData.totalIncome   ?? 0;
        const expenses  = financeData.totalExpenses ?? 0;
        const balance   = financeData.netBalance    ?? (income - expenses);
        const balanceEl = document.getElementById('dash-fin-balance');
        const labelEl   = document.getElementById('dash-fin-balance-label');
        const incomeEl  = document.getElementById('dash-fin-income');
        const expEl     = document.getElementById('dash-fin-expenses');
        const sign      = balance >= 0 ? '+' : '−';
        if (balanceEl) {
            balanceEl.textContent = sign + currency + ' ' + fmtChf(Math.abs(balance));
            balanceEl.style.color = balance >= 0 ? 'var(--green)' : 'var(--red)';
        }
        if (labelEl)  labelEl.textContent = 'Bilanz';
        if (incomeEl) incomeEl.textContent = '↑ ' + currency + ' ' + fmtChf(income);
        if (expEl)    expEl.textContent    = '↓ ' + currency + ' ' + fmtChf(expenses);
    }
}

// ── DASHBOARD CARDS (async) ──
async function loadDashboardAsync() {
    // Kalender
    try {
        const events  = await api(withProject('/api/calendar'));
        const calBody = document.getElementById('dash-cal-body');
        if (calBody) {
            const todayStr = new Date().toISOString().split('T')[0];
            const upcoming = (events ?? [])
                .filter(e => e.date >= todayStr)
                .sort((a, b) => a.date.localeCompare(b.date))
                .slice(0, 3);
            if (upcoming.length === 0) {
                calBody.innerHTML = '<div class="dash-empty">Keine bevorstehenden Termine</div>';
            } else {
                calBody.innerHTML = upcoming.map(e => `
                    <div class="dash-event-row">
                        <div class="dash-event-date">${fmt(new Date(e.date))}${e.time ? ' · ' + e.time : ''}</div>
                        <div class="dash-event-title">${e.title.replace(/</g, '&lt;')}</div>
                    </div>`).join('');
            }
        }
    } catch { /* silently ignore */ }

    // Produktion
    try {
        const items    = await api(withProject('/api/production'));
        const prodBody = document.getElementById('dash-prod-body');
        if (prodBody) {
            const openCount  = (items ?? []).filter(i => !i.done).length;
            const totalCount = (items ?? []).length;
            prodBody.innerHTML = `
                <div class="dash-stat">${openCount}</div>
                <div class="dash-stat-label">Offene Artikel</div>
                <div class="dash-row" style="margin-top:12px">
                    <span class="dash-chip">${totalCount} gesamt</span>
                    <span class="dash-chip dash-chip--muted">${totalCount - openCount} erledigt</span>
                </div>`;
        }
    } catch { /* silently ignore */ }

    // Meilensteine
    try {
        const milestones = await api(withProject('/api/milestones'));
        const msBody     = document.getElementById('dash-ms-body');
        if (msBody) {
            if (!milestones || milestones.length === 0) {
                msBody.innerHTML = '<div class="dash-empty">Noch keine Meilensteine</div>';
            } else {
                const latest  = milestones[milestones.length - 1];
                const dateStr = fmt(new Date(latest.createdAt));
                msBody.innerHTML = `
                    <div class="dash-stat">${milestones.length}</div>
                    <div class="dash-stat-label">Meilensteine gesetzt</div>
                    <div class="dash-row" style="margin-top:12px">
                        <span class="dash-chip">${latest.name.replace(/</g, '&lt;')}</span>
                        <span class="dash-chip dash-chip--muted">${dateStr}</span>
                    </div>`;
            }
        }
    } catch { /* silently ignore */ }
}

// ── ADMIN ──
let editingWeekNumber = null;
let editingTaskId = null;
let editingTaskWeekNumber = null;

function renderAdmin() {
    if (!appData) return;
    const container = document.getElementById('admin-content');
    container.innerHTML = '';

    appData.weeks.forEach(week => {
        const div = document.createElement('div');
        div.className = 'admin-week';
        div.innerHTML = `
      <div class="admin-week-header">
        <span class="admin-week-num">W0${week.number}</span>
        <div style="flex:1">
          <div class="admin-week-title">${week.title}</div>
          <div class="admin-week-phase">${week.phase}</div>
        </div>
        <div class="admin-week-actions">
          <button class="btn-icon" onclick="openEditWeekModal(${week.number})">✏️ Bearbeiten</button>
          <button class="btn-icon danger" onclick="deleteWeek(${week.number})">🗑 Löschen</button>
        </div>
      </div>
      <div class="admin-tasks" id="admin-tasks-${week.number}">
        ${renderAdminTasks(week)}
      </div>
      <button class="admin-add-task" onclick="openNewTaskModal(${week.number})">+ Task hinzufügen</button>`;
        container.appendChild(div);
    });

    initDragDrop();
}

function renderAdminTasks(week) {
    if (!week.tasks || week.tasks.length === 0) {
        return '<div style="padding:12px 18px;font-size:13px;color:var(--text3)">Noch keine Tasks.</div>';
    }
    return week.tasks.map(task => `
      <div class="admin-task-row" data-task-db-id="${task.id}">
        <span class="drag-handle">⠿</span>
        <span class="task-type type-${task.type}">${task.type === 'pc' ? 'PC' : 'Physisch'}</span>
        <span class="admin-task-text">${task.text}</span>
        <span class="admin-task-hrs">${task.hours}</span>
        <button class="btn-icon" onclick="openEditTaskModal(${task.id}, ${week.number})">✏️</button>
        <button class="btn-icon danger" onclick="deleteTask(${task.id}, ${week.number})">🗑</button>
      </div>`).join('');
}

function initDragDrop() {
    document.querySelectorAll('.admin-tasks').forEach(container => {
        const weekNumber = parseInt(container.id.replace('admin-tasks-', ''));
        container.querySelectorAll('.admin-task-row').forEach(row => {
            row.setAttribute('draggable', true);
            row.addEventListener('dragstart', e => {
                e.dataTransfer.setData('text/plain', row.dataset.taskDbId);
                setTimeout(() => row.style.opacity = '0.4', 0);
            });
            row.addEventListener('dragend', () => {
                row.style.opacity = '1';
                container.querySelectorAll('.admin-task-row').forEach(r => r.classList.remove('drag-over'));
            });
            row.addEventListener('dragover', e => {
                e.preventDefault();
                container.querySelectorAll('.admin-task-row').forEach(r => r.classList.remove('drag-over'));
                row.classList.add('drag-over');
            });
            row.addEventListener('drop', async e => {
                e.preventDefault();
                container.querySelectorAll('.admin-task-row').forEach(r => r.classList.remove('drag-over'));
                const draggedDbId = parseInt(e.dataTransfer.getData('text/plain'));
                const targetDbId = parseInt(row.dataset.taskDbId);
                if (draggedDbId === targetDbId) return;
                const allRows = [...container.querySelectorAll('.admin-task-row')];
                const ids = allRows.map(r => parseInt(r.dataset.taskDbId));
                const fromIdx = ids.indexOf(draggedDbId);
                const toIdx = ids.indexOf(targetDbId);
                if (fromIdx === -1 || toIdx === -1) return;
                const reordered = [...ids];
                const [moved] = reordered.splice(fromIdx, 1);
                reordered.splice(toIdx, 0, moved);
                await api(withProject('/api/admin/weeks/' + weekNumber + '/reorder'), 'PUT', { taskIds: reordered });
                appData = await api(withProject('/api/data'));
                renderRoadmap();
                renderAdmin();
                updateAll();
                showToast('✓ Reihenfolge gespeichert');
            });
        });
    });
}

function openNewWeekModal() {
    editingWeekNumber = null;
    document.getElementById('week-modal-title').textContent = 'Woche erstellen';
    document.getElementById('week-modal-save').textContent = 'Erstellen';
    document.getElementById('week-title').value = '';
    document.getElementById('week-phase').value = '';
    document.getElementById('week-badge-pc').value = '';
    document.getElementById('week-badge-phys').value = '';
    document.getElementById('week-note').value = '';
    document.getElementById('week-modal').classList.add('open');
}

function openEditWeekModal(number) {
    const week = appData.weeks.find(w => w.number === number);
    if (!week) return;
    editingWeekNumber = number;
    document.getElementById('week-modal-title').textContent = 'Woche bearbeiten';
    document.getElementById('week-modal-save').textContent = 'Speichern';
    document.getElementById('week-title').value = week.title;
    document.getElementById('week-phase').value = week.phase;
    document.getElementById('week-badge-pc').value = week.badgePc;
    document.getElementById('week-badge-phys').value = week.badgePhys;
    document.getElementById('week-note').value = week.note ?? '';
    document.getElementById('week-modal').classList.add('open');
}

function closeWeekModal() {
    document.getElementById('week-modal').classList.remove('open');
}

async function saveWeek() {
    const title = document.getElementById('week-title').value.trim();
    const phase = document.getElementById('week-phase').value.trim();
    const badgePc = document.getElementById('week-badge-pc').value.trim();
    const badgePhys = document.getElementById('week-badge-phys').value.trim();
    const note = document.getElementById('week-note').value.trim() || null;

    if (!title || !phase) { showToast('Titel und Phase sind Pflichtfelder.'); return; }

    try {
        if (editingWeekNumber) {
            await api(withProject('/api/admin/weeks/' + editingWeekNumber), 'PUT', { title, phase, badgePc, badgePhys, note });
            showToast('✓ Woche aktualisiert');
        } else {
            await api(withProject('/api/admin/weeks'), 'POST', { title, phase, badgePc, badgePhys, note });
            showToast('✓ Woche erstellt');
        }
        appData = await api(withProject('/api/data'));
        renderRoadmap();
        renderAdmin();
        updateAll();
        closeWeekModal();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteWeek(number) {
    if (!confirm(`Woche ${number} und alle Tasks löschen?`)) return;
    try {
        await api(withProject('/api/admin/weeks/' + number), 'DELETE');
        appData = await api(withProject('/api/data'));
        renderRoadmap();
        renderAdmin();
        updateAll();
        showToast('✓ Woche gelöscht');
    } catch { showToast('Fehler beim Löschen.'); }
}

function openNewTaskModal(weekNumber) {
    editingTaskId = null;
    editingTaskWeekNumber = weekNumber;
    document.getElementById('task-modal-title').textContent = 'Task erstellen';
    document.getElementById('task-modal-save').textContent = 'Erstellen';
    document.getElementById('task-type').value = 'pc';
    document.getElementById('task-text').value = '';
    document.getElementById('task-hours').value = '';
    document.getElementById('task-modal').classList.add('open');
}

function openEditTaskModal(taskDbId, weekNumber) {
    const week = appData.weeks.find(w => w.number === weekNumber);
    if (!week) return;
    const task = week.tasks.find(t => t.id === taskDbId);
    if (!task) return;
    editingTaskId = taskDbId;
    editingTaskWeekNumber = weekNumber;
    document.getElementById('task-modal-title').textContent = 'Task bearbeiten';
    document.getElementById('task-modal-save').textContent = 'Speichern';
    document.getElementById('task-type').value = task.type;
    document.getElementById('task-text').value = task.text;
    document.getElementById('task-hours').value = task.hours;
    document.getElementById('task-modal').classList.add('open');
}

function closeTaskModal() {
    document.getElementById('task-modal').classList.remove('open');
}

async function saveTask() {
    const type = document.getElementById('task-type').value;
    const text = document.getElementById('task-text').value.trim();
    const hours = document.getElementById('task-hours').value.trim();

    if (!text || !hours) { showToast('Beschreibung und Zeitaufwand sind Pflichtfelder.'); return; }

    try {
        if (editingTaskId !== null) {
            await api(withProject('/api/admin/tasks/' + editingTaskId), 'PUT', { type, text, hours });
            showToast('✓ Task aktualisiert');
        } else {
            await api(withProject('/api/admin/tasks'), 'POST', { weekNumber: editingTaskWeekNumber, type, text, hours });
            showToast('✓ Task erstellt');
        }
        appData = await api(withProject('/api/data'));
        renderRoadmap();
        renderAdmin();
        updateAll();
        closeTaskModal();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteTask(taskDbId, weekNumber) {
    if (!confirm('Task löschen?')) return;
    try {
        await api(withProject('/api/admin/tasks/' + taskDbId), 'DELETE');
        appData = await api(withProject('/api/data'));
        renderRoadmap();
        renderAdmin();
        updateAll();
        showToast('✓ Task gelöscht');
    } catch { showToast('Fehler beim Löschen.'); }
}
