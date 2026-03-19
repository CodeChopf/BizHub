// ── PROJEKT SETTINGS ──
let projectSettings = null;
let START = new Date();

function getStart() { return START; }

function applySettings(settings) {
    projectSettings = settings;
    START = new Date(settings.startDate);

    // Sidebar
    const nameEl = document.getElementById('sidebar-project-name');
    const iconEl = document.getElementById('sidebar-logo-icon');
    if (nameEl) nameEl.textContent = settings.projectName;
    if (iconEl) iconEl.textContent = settings.projectName.substring(0, 2).toUpperCase();

    // Overview
    const titleEl = document.getElementById('overview-title');
    if (titleEl) titleEl.textContent = settings.projectName;

    // Einstellungen
    const sName = document.getElementById('settings-name');
    const sStart = document.getElementById('settings-start');
    const sDesc = document.getElementById('settings-desc');
    const sCurr = document.getElementById('settings-currency');
    if (sName) sName.value = settings.projectName;
    if (sStart) sStart.value = settings.startDate;
    if (sDesc) sDesc.value = settings.description ?? '';
    if (sCurr) sCurr.value = settings.currency ?? 'CHF';

    // Document title
    document.title = settings.projectName + ' — BizHub';
}

function getCurrency() {
    return projectSettings?.currency ?? 'CHF';
}

// ── DATE HELPERS ──
function addDays(d, n) {
    const r = new Date(d); r.setDate(r.getDate() + n); return r;
}
function fmt(d) {
    return d.toLocaleDateString('de-CH', { day: '2-digit', month: '2-digit', year: 'numeric' });
}
function fmtShort(d) {
    return d.toLocaleDateString('de-CH', { day: '2-digit', month: '2-digit' });
}
function formatDateStr(dateStr) {
    if (!dateStr) return '';
    const [y, m, d] = dateStr.split('-');
    return `${d}.${m}.${y}`;
}
function fmtChf(amount) {
    return amount.toLocaleString('de-CH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}
function showToast(msg) {
    const t = document.getElementById('toast');
    t.textContent = msg;
    t.classList.add('show');
    setTimeout(() => t.classList.remove('show'), 2500);
}

function getWeekRanges() {
    const ranges = [];
    if (!appData?.weeks) return ranges;
    for (let i = 0; i < appData.weeks.length; i++) {
        ranges.push({ start: addDays(START, i * 7), end: addDays(START, i * 7 + 6) });
    }
    return ranges;
}

const today = new Date();
let state = {};
let appData = null;
let productData = null;
let financeData = null;

// ── API HELPERS ──
async function api(url, method = 'GET', body = null) {
    const opts = { method, headers: { 'Content-Type': 'application/json' } };
    if (body) opts.body = JSON.stringify(body);
    const res = await fetch(url, opts);
    return res.json();
}

async function loadState() {
    try { state = await api('/api/state'); } catch { state = {}; }
}

async function saveState() {
    try { await api('/api/state', 'POST', state); } catch { console.error('Speichern fehlgeschlagen'); }
}

// ── SETUP SCREEN ──
function switchSetupTab(tab) {
    document.querySelectorAll('.setup-tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('.setup-panel').forEach(p => p.style.display = 'none');
    document.getElementById('setup-tab-' + tab).style.display = 'block';
    event.target.classList.add('active');
}

async function createProject() {
    const name = document.getElementById('setup-name').value.trim();
    const start = document.getElementById('setup-start').value;
    const desc = document.getElementById('setup-desc').value.trim();
    const currency = document.getElementById('setup-currency').value;

    if (!name) { alert('Bitte einen Projektnamen eingeben.'); return; }
    if (!start) { alert('Bitte ein Startdatum wählen.'); return; }

    await api('/api/settings', 'POST', { projectName: name, startDate: start, description: desc, currency });
    location.reload();
}

let _importData = null;

function handleImportPreview() {
    const file = document.getElementById('setup-import-file').files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = e => {
        try {
            const data = JSON.parse(e.target.result);
            _importData = data;
            const preview = document.getElementById('setup-import-preview');
            preview.innerHTML = `
                <div class="import-preview-card">
                    <div class="import-preview-name">${data.settings?.projectName ?? 'Unbekannt'}</div>
                    <div class="import-preview-meta">
                        <div class="import-preview-item">Start: <span>${formatDateStr(data.settings?.startDate ?? '')}</span></div>
                        <div class="import-preview-item">Wochen: <span>${data.weeks?.length ?? 0}</span></div>
                        <div class="import-preview-item">Ausgaben: <span>${data.finance?.expenses?.length ?? 0}</span></div>
                        <div class="import-preview-item">Exportiert: <span>${formatDateStr((data.exportedAt ?? '').split(' ')[0])}</span></div>
                    </div>
                </div>`;
            document.getElementById('setup-import-btn').disabled = false;
        } catch {
            alert('Ungültige JSON-Datei.');
        }
    };
    reader.readAsText(file);
}

async function importProject() {
    if (!_importData) return;
    try {
        await api('/api/import', 'POST', _importData);
        location.reload();
    } catch {
        alert('Fehler beim Importieren.');
    }
}

// ── INIT ──
async function init() {
    // Settings laden
    const settings = await api('/api/settings');

    if (!settings.isSetup) {
        // Setup-Screen anzeigen
        document.getElementById('setup-screen').style.display = 'flex';
        document.getElementById('app').style.display = 'none';
        // Datum auf heute vorbelegen
        document.getElementById('setup-start').value = today.toISOString().split('T')[0];
        return;
    }

    // App anzeigen
    document.getElementById('setup-screen').style.display = 'none';
    document.getElementById('app').style.display = 'flex';

    applySettings(settings);

    await loadState();
    try {
        const [dataRes, finRes] = await Promise.all([
            fetch('/api/data'),
            fetch('/api/finance')
        ]);
        appData = await dataRes.json();
        financeData = await finRes.json();
    } catch {
        document.getElementById('roadmap-content').innerHTML =
            '<p style="color:var(--red);padding:20px">Fehler beim Laden.</p>';
        return;
    }

    const weekRanges = getWeekRanges();
    let currentWeek = 1;
    for (let i = 0; i < weekRanges.length; i++) {
        if (today >= weekRanges[i].start && today <= weekRanges[i].end) { currentWeek = i + 1; break; }
        if (today > weekRanges[i].end) currentWeek = i + 1;
    }
    window._currentWeek = currentWeek;

    const launchDate = weekRanges.length >= 6 ? weekRanges[5].start : addDays(START, 35);
    const daysToLaunch = Math.ceil((launchDate - today) / (1000 * 60 * 60 * 24));
    window._daysToLaunch = daysToLaunch;
    window._launchDate = launchDate;

    document.getElementById('sidebar-date').textContent = 'Heute: ' + fmt(today);
    document.getElementById('sidebar-launch').textContent =
        daysToLaunch > 0 ? '🚀 in ' + daysToLaunch + ' Tagen' : '🚀 Gestartet!';
    document.getElementById('topbar-date').innerHTML =
        'Heute: ' + fmt(today) + '<br>Start: ' + fmt(START);

    renderKpis();
    renderRoadmap();
    renderFinanzen();
    updateAll();

    const cwBody = document.getElementById('wb-' + currentWeek);
    const cwChev = document.getElementById('chev-' + currentWeek);
    if (cwBody) cwBody.classList.add('open');
    if (cwChev) cwChev.classList.add('open');
}

// ── EINSTELLUNGEN ──
async function saveSettings() {
    const name = document.getElementById('settings-name').value.trim();
    const start = document.getElementById('settings-start').value;
    const desc = document.getElementById('settings-desc').value.trim();
    const currency = document.getElementById('settings-currency').value;

    if (!name || !start) { showToast('Name und Startdatum sind Pflicht.'); return; }

    const settings = await api('/api/settings', 'POST', { projectName: name, startDate: start, description: desc, currency });
    applySettings(settings);
    showToast('✓ Einstellungen gespeichert');
}

// ── EXPORT / IMPORT ──
function exportData() {
    const a = document.createElement('a');
    a.href = '/api/export';
    a.download = '';
    a.click();
    showToast('✓ Export wird heruntergeladen');
}

function openImportModal() {
    document.getElementById('import-file').value = '';
    document.getElementById('import-preview').innerHTML = '';
    document.getElementById('import-confirm-btn').disabled = true;
    _importData = null;
    document.getElementById('import-modal').classList.add('open');
}

function closeImportModal() {
    document.getElementById('import-modal').classList.remove('open');
}

function handleImportFileChange() {
    const file = document.getElementById('import-file').files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = e => {
        try {
            const data = JSON.parse(e.target.result);
            _importData = data;
            document.getElementById('import-preview').innerHTML = `
                <div class="import-preview-card">
                    <div class="import-preview-name">${data.settings?.projectName ?? 'Unbekannt'}</div>
                    <div class="import-preview-meta">
                        <div class="import-preview-item">Start: <span>${formatDateStr(data.settings?.startDate ?? '')}</span></div>
                        <div class="import-preview-item">Wochen: <span>${data.weeks?.length ?? 0}</span></div>
                        <div class="import-preview-item">Ausgaben: <span>${data.finance?.expenses?.length ?? 0}</span></div>
                        <div class="import-preview-item">Exportiert: <span>${formatDateStr((data.exportedAt ?? '').split(' ')[0])}</span></div>
                    </div>
                </div>`;
            document.getElementById('import-confirm-btn').disabled = false;
        } catch {
            showToast('Ungültige JSON-Datei.');
        }
    };
    reader.readAsText(file);
}

async function confirmImport() {
    if (!_importData) return;
    try {
        await api('/api/import', 'POST', _importData);
        closeImportModal();
        showToast('✓ Import erfolgreich — App wird neu geladen');
        setTimeout(() => location.reload(), 1500);
    } catch {
        showToast('Fehler beim Importieren.');
    }
}

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
    <div class="kpi-card ${k.color}">
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

// ── FINANZEN ──
function renderFinanzen() {
    if (!financeData) return;

    const strip = document.getElementById('fin-kpi-strip');
    const total = financeData.totalExpenses;
    const count = financeData.expenses.length;
    const cats = financeData.categories.length;
    const currency = getCurrency();

    strip.innerHTML = `
    <div class="kpi-card red">
      <div class="kpi-icon">💸</div>
      <div class="kpi-val">${currency} ${fmtChf(total)}</div>
      <div class="kpi-label">Gesamtausgaben</div>
    </div>
    <div class="kpi-card amber">
      <div class="kpi-icon">🧾</div>
      <div class="kpi-val">${count}</div>
      <div class="kpi-label">Buchungen</div>
    </div>
    <div class="kpi-card blue">
      <div class="kpi-icon">🏷️</div>
      <div class="kpi-val">${cats}</div>
      <div class="kpi-label">Kategorien</div>
    </div>`;

    const catSummary = document.getElementById('category-summary');
    if (financeData.summary.length === 0) {
        catSummary.innerHTML = '<div class="empty-state">Noch keine Ausgaben erfasst.</div>';
    } else {
        const maxTotal = Math.max(...financeData.summary.map(s => s.total));
        catSummary.innerHTML = financeData.summary.map(s => `
      <div class="cat-row">
        <div class="cat-dot" style="background:${getCategoryColor(s.categoryName)}"></div>
        <div class="cat-name">${s.categoryName}</div>
        <div class="cat-bar-wrap">
          <div class="cat-bar-fill" style="width:${Math.round((s.total / maxTotal) * 100)}%;background:${getCategoryColor(s.categoryName)}"></div>
        </div>
        <div class="cat-count">${s.count}×</div>
        <div class="cat-amount">${currency} ${fmtChf(s.total)}</div>
      </div>`).join('');
    }

    const expList = document.getElementById('expenses-list');
    if (financeData.expenses.length === 0) {
        expList.innerHTML = '<div class="empty-state">Noch keine Ausgaben. Klicke auf "+ Ausgabe erfassen".</div>';
    } else {
        expList.innerHTML = financeData.expenses.map(e => {
            const weekLabel = e.weekNumber ? `<span class="exp-week-badge">W0${e.weekNumber}</span>` : '';
            const linkHtml = e.link ? `<a href="${e.link}" target="_blank" class="exp-link">🔗 Link öffnen</a>` : '';
            return `
        <div class="exp-row">
          <span class="exp-cat-badge" style="background:${e.categoryColor}22;color:${e.categoryColor}">${e.categoryName}</span>
          <div>
            <div class="exp-desc">${e.description}</div>
            <div class="exp-meta">${formatDateStr(e.date)} ${weekLabel} ${linkHtml}</div>
            <div id="attachments-${e.id}" class="exp-attachments"></div>
          </div>
          <div class="exp-amount">−${currency} ${fmtChf(e.amount)}</div>
          <button class="btn-icon" onclick="openEditExpenseModal(${e.id})" title="Bearbeiten">✏️</button>
          <button class="exp-delete" onclick="deleteExpense(${e.id})" title="Löschen">✕</button>
        </div>`;
        }).join('');

        financeData.expenses.forEach(async e => {
            const res = await fetch('/api/expenses/' + e.id + '/attachments');
            const attachments = await res.json();
            const container = document.getElementById('attachments-' + e.id);
            if (container) renderAttachments(e.id, attachments, container);
        });
    }
}

function getCategoryColor(name) {
    const cat = financeData?.categories.find(c => c.name === name);
    return cat?.color ?? '#9699a8';
}

// ── ATTACHMENT HELPERS ──
function renderAttachments(expenseId, attachments, container) {
    if (!attachments || attachments.length === 0) { container.innerHTML = ''; return; }
    container.innerHTML = attachments.map(a => `
        <div style="display:inline-flex;align-items:center;gap:6px;margin-top:6px;margin-right:6px">
            <img src="data:${a.mimeType};base64,${a.data}"
                style="width:40px;height:40px;object-fit:cover;border-radius:6px;border:1px solid var(--border2);cursor:pointer"
                onclick="openAttachment('${a.mimeType}','${a.data}')" title="${a.fileName}">
            <button class="exp-delete" onclick="deleteAttachment(${a.id}, ${expenseId})" title="Beleg löschen">✕</button>
        </div>`).join('');
}

function openAttachment(mimeType, base64) {
    const win = window.open();
    win.document.write(`<img src="data:${mimeType};base64,${base64}" style="max-width:100%;max-height:100vh;object-fit:contain">`);
}

async function deleteAttachment(id, expenseId) {
    if (!confirm('Beleg löschen?')) return;
    try {
        await api('/api/attachments/' + id, 'DELETE');
        const res = await fetch('/api/expenses/' + expenseId + '/attachments');
        const attachments = await res.json();
        const container = document.getElementById('attachments-' + expenseId);
        if (container) renderAttachments(expenseId, attachments, container);
        showToast('✓ Beleg gelöscht');
    } catch { showToast('Fehler beim Löschen.'); }
}

function fileToBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result.split(',')[1]);
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

function handleFilePreview() {
    const file = document.getElementById('exp-file').files[0];
    const preview = document.getElementById('exp-attachment-preview');
    const nameEl = document.getElementById('exp-file-name');
    if (!file) { preview.innerHTML = ''; nameEl.textContent = ''; return; }
    nameEl.textContent = file.name;
    const reader = new FileReader();
    reader.onload = e => {
        preview.innerHTML = `<img src="${e.target.result}" style="max-width:100%;max-height:120px;border-radius:8px;margin-top:8px;border:1px solid var(--border2);object-fit:cover;display:block;">`;
    };
    reader.readAsDataURL(file);
}

function handleEditFilePreview() {
    const file = document.getElementById('edit-exp-file').files[0];
    const preview = document.getElementById('edit-exp-attachment-preview');
    const nameEl = document.getElementById('edit-exp-file-name');
    if (!file) { preview.innerHTML = ''; nameEl.textContent = ''; return; }
    nameEl.textContent = file.name;
    const reader = new FileReader();
    reader.onload = e => {
        preview.innerHTML = `<img src="${e.target.result}" style="max-width:100%;max-height:120px;border-radius:8px;margin-top:8px;border:1px solid var(--border2);object-fit:cover;display:block;">`;
    };
    reader.readAsDataURL(file);
}

// ── EXPENSE MODAL ──
function openExpenseModal() {
    const sel = document.getElementById('exp-category');
    sel.innerHTML = financeData.categories.map(c =>
        `<option value="${c.id}">${c.name}</option>`).join('');

    const weekSel = document.getElementById('exp-week');
    weekSel.innerHTML = '<option value="">Keine Zuweisung</option>' +
        appData.weeks.map(w => `<option value="${w.number}">Woche ${w.number}: ${w.title}</option>`).join('');

    document.getElementById('exp-task').innerHTML = '<option value="">Keine Zuweisung</option>';
    document.getElementById('exp-date').value = today.toISOString().split('T')[0];
    document.getElementById('exp-attachment-preview').innerHTML = '';
    document.getElementById('exp-file').value = '';
    document.getElementById('exp-file-name').textContent = '';

    weekSel.onchange = () => {
        const wNum = parseInt(weekSel.value);
        const taskSel = document.getElementById('exp-task');
        if (!wNum) { taskSel.innerHTML = '<option value="">Keine Zuweisung</option>'; return; }
        const week = appData.weeks.find(w => w.number === wNum);
        taskSel.innerHTML = '<option value="">Keine Zuweisung</option>' +
            (week?.tasks.map(t => `<option value="${t.id}">${t.text.substring(0, 60)}...</option>`) ?? []).join('');
    };

    document.getElementById('expense-modal').classList.add('open');
}

function closeExpenseModal() {
    document.getElementById('expense-modal').classList.remove('open');
}

async function saveExpense() {
    const categoryId = parseInt(document.getElementById('exp-category').value);
    const amount = parseFloat(document.getElementById('exp-amount').value);
    const description = document.getElementById('exp-description').value.trim();
    const link = document.getElementById('exp-link').value.trim() || null;
    const date = document.getElementById('exp-date').value;
    const weekNumber = document.getElementById('exp-week').value ? parseInt(document.getElementById('exp-week').value) : null;
    const taskId = document.getElementById('exp-task').value ? parseInt(document.getElementById('exp-task').value) : null;

    if (!description || !amount || amount <= 0) { showToast('Bitte Beschreibung und Betrag eingeben.'); return; }

    try {
        const expense = await api('/api/expenses', 'POST', { categoryId, amount, description, link, date, weekNumber, taskId });

        const fileInput = document.getElementById('exp-file');
        if (fileInput.files.length > 0) {
            const file = fileInput.files[0];
            const base64 = await fileToBase64(file);
            await api(`/api/expenses/${expense.id}/attachments`, 'POST', { fileName: file.name, mimeType: file.type, data: base64 });
        }

        financeData = await api('/api/finance');
        renderFinanzen();
        closeExpenseModal();
        showToast('✓ Ausgabe gespeichert');
        document.getElementById('exp-amount').value = '';
        document.getElementById('exp-description').value = '';
        document.getElementById('exp-link').value = '';
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteExpense(id) {
    if (!confirm('Ausgabe löschen?')) return;
    try {
        await api('/api/expenses/' + id, 'DELETE');
        financeData = await api('/api/finance');
        renderFinanzen();
        showToast('✓ Ausgabe gelöscht');
    } catch { showToast('Fehler beim Löschen.'); }
}

// ── EDIT EXPENSE MODAL ──
function openEditExpenseModal(id) {
    const expense = financeData.expenses.find(e => e.id === id);
    if (!expense) return;

    document.getElementById('edit-exp-id').value = expense.id;
    document.getElementById('edit-exp-amount').value = expense.amount;
    document.getElementById('edit-exp-description').value = expense.description;
    document.getElementById('edit-exp-link').value = expense.link ?? '';
    document.getElementById('edit-exp-date').value = expense.date;
    document.getElementById('edit-exp-file').value = '';
    document.getElementById('edit-exp-file-name').textContent = '';
    document.getElementById('edit-exp-attachment-preview').innerHTML = '';

    const catSel = document.getElementById('edit-exp-category');
    catSel.innerHTML = financeData.categories.map(c =>
        `<option value="${c.id}" ${c.id === expense.categoryId ? 'selected' : ''}>${c.name}</option>`).join('');

    const weekSel = document.getElementById('edit-exp-week');
    weekSel.innerHTML = '<option value="">Keine Zuweisung</option>' +
        appData.weeks.map(w =>
            `<option value="${w.number}" ${w.number === expense.weekNumber ? 'selected' : ''}>Woche ${w.number}: ${w.title}</option>`
        ).join('');

    const taskSel = document.getElementById('edit-exp-task');
    if (expense.weekNumber) {
        const week = appData.weeks.find(w => w.number === expense.weekNumber);
        taskSel.innerHTML = '<option value="">Keine Zuweisung</option>' +
            (week?.tasks.map(t =>
                `<option value="${t.id}" ${t.id === expense.taskId ? 'selected' : ''}>${t.text.substring(0, 60)}...</option>`
            ) ?? []).join('');
    } else {
        taskSel.innerHTML = '<option value="">Keine Zuweisung</option>';
    }

    weekSel.onchange = () => {
        const wNum = parseInt(weekSel.value);
        if (!wNum) { taskSel.innerHTML = '<option value="">Keine Zuweisung</option>'; return; }
        const week = appData.weeks.find(w => w.number === wNum);
        taskSel.innerHTML = '<option value="">Keine Zuweisung</option>' +
            (week?.tasks.map(t => `<option value="${t.id}">${t.text.substring(0, 60)}...</option>`) ?? []).join('');
    };

    document.getElementById('edit-expense-modal').classList.add('open');
}

function closeEditExpenseModal() {
    document.getElementById('edit-expense-modal').classList.remove('open');
}

async function updateExpense() {
    const id = parseInt(document.getElementById('edit-exp-id').value);
    const categoryId = parseInt(document.getElementById('edit-exp-category').value);
    const amount = parseFloat(document.getElementById('edit-exp-amount').value);
    const description = document.getElementById('edit-exp-description').value.trim();
    const link = document.getElementById('edit-exp-link').value.trim() || null;
    const date = document.getElementById('edit-exp-date').value;
    const weekNumber = document.getElementById('edit-exp-week').value ? parseInt(document.getElementById('edit-exp-week').value) : null;
    const taskId = document.getElementById('edit-exp-task').value ? parseInt(document.getElementById('edit-exp-task').value) : null;

    if (!description || !amount || amount <= 0) { showToast('Bitte Beschreibung und Betrag eingeben.'); return; }

    try {
        await api('/api/expenses/' + id, 'PUT', { categoryId, amount, description, link, date, weekNumber, taskId });

        const fileInput = document.getElementById('edit-exp-file');
        if (fileInput.files.length > 0) {
            const file = fileInput.files[0];
            const base64 = await fileToBase64(file);
            await api(`/api/expenses/${id}/attachments`, 'POST', { fileName: file.name, mimeType: file.type, data: base64 });
        }

        financeData = await api('/api/finance');
        renderFinanzen();
        closeEditExpenseModal();
        showToast('✓ Ausgabe aktualisiert');
    } catch { showToast('Fehler beim Speichern.'); }
}

// ── CATEGORY MODAL ──
function openCategoryModal() {
    renderCategoryList();
    document.getElementById('category-modal').classList.add('open');
}

function closeCategoryModal() {
    document.getElementById('category-modal').classList.remove('open');
}

function renderCategoryList() {
    const list = document.getElementById('category-list');
    list.innerHTML = financeData.categories.map(c => `
    <div class="cat-edit-row">
      <input type="color" class="cat-edit-color" value="${c.color}"
        onchange="updateCategoryColor(${c.id}, this.value)"
        style="width:32px;height:32px;border-radius:50%;padding:2px;border:2px solid var(--border2);cursor:pointer">
      <input type="text" class="cat-edit-name" value="${c.name}"
        onblur="updateCategoryName(${c.id}, this.value, '${c.color}')">
      <button class="cat-edit-delete" onclick="deleteCategory(${c.id})">✕</button>
    </div>`).join('');
}

async function updateCategoryName(id, name, color) {
    if (!name.trim()) return;
    await api('/api/categories/' + id, 'PUT', { name: name.trim(), color });
    financeData = await api('/api/finance');
    renderFinanzen();
}

async function updateCategoryColor(id, color) {
    const cat = financeData.categories.find(c => c.id === id);
    if (!cat) return;
    await api('/api/categories/' + id, 'PUT', { name: cat.name, color });
    financeData = await api('/api/finance');
    renderFinanzen();
}

async function addCategory() {
    const name = document.getElementById('new-cat-name').value.trim();
    const color = document.getElementById('new-cat-color').value;
    if (!name) { showToast('Bitte einen Namen eingeben.'); return; }
    await api('/api/categories', 'POST', { name, color });
    financeData = await api('/api/finance');
    renderCategoryList();
    renderFinanzen();
    document.getElementById('new-cat-name').value = '';
    showToast('✓ Kategorie hinzugefügt');
}

async function deleteCategory(id) {
    if (!confirm('Kategorie löschen?')) return;
    await api('/api/categories/' + id, 'DELETE');
    financeData = await api('/api/finance');
    renderCategoryList();
    renderFinanzen();
    showToast('✓ Kategorie gelöscht');
}

function closeModals(event) {
    if (event.target.classList.contains('modal-backdrop')) {
        document.querySelectorAll('.modal-backdrop').forEach(m => m.classList.remove('open'));
    }
}

// ── TOGGLE TASK ──
function toggleTask(row) {
    row.classList.toggle('done');
    state[row.dataset.idx] = row.classList.contains('done');
    saveState();
    updateAll();
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
    document.getElementById('page-' + id).classList.add('active');
    const idx = { overview: 0, roadmap: 1, finanzen: 2, meilensteine: 3, admin: 4, einstellungen: 5 };
    const navItems = document.querySelectorAll('.nav-item');
    if (idx[id] !== undefined && navItems[idx[id]]) navItems[idx[id]].classList.add('active');
    if (id === 'admin') renderAdmin();
    if (id === 'meilensteine') renderMilestones();
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
    document.getElementById('donut-fill').setAttribute('stroke-dasharray', `${filled} ${circumference}`);
    document.getElementById('donut-pct').textContent = pct + '%';
    document.getElementById('stat-done').textContent = done;
    document.getElementById('stat-open').textContent = total - done;
    document.getElementById('stat-weeks').textContent = appData.weeks.length;

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
}

function updateOverview() {
    if (!appData) return;
    const currentWeek = window._currentWeek ?? 1;
    const week = appData.weeks.find(w => w.number === currentWeek);
    const weekRanges = getWeekRanges();
    if (!week) return;

    document.getElementById('cw-badge').textContent = 'Woche ' + currentWeek;
    document.getElementById('cw-title').textContent = week.title;
    const wIdx = currentWeek - 1;
    if (wIdx < weekRanges.length) {
        document.getElementById('cw-date').textContent =
            fmt(weekRanges[wIdx].start) + ' – ' + fmt(weekRanges[wIdx].end);
    }

    const cwBody = document.getElementById('wb-' + currentWeek);
    const cwTasksEl = document.getElementById('cw-tasks');
    cwTasksEl.innerHTML = '';

    if (cwBody) {
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
            div.onclick = () => {
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
                await api('/api/admin/weeks/' + weekNumber + '/reorder', 'PUT', { taskIds: reordered });
                appData = await api('/api/data');
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
            await api('/api/admin/weeks/' + editingWeekNumber, 'PUT', { title, phase, badgePc, badgePhys, note });
            showToast('✓ Woche aktualisiert');
        } else {
            await api('/api/admin/weeks', 'POST', { title, phase, badgePc, badgePhys, note });
            showToast('✓ Woche erstellt');
        }
        appData = await api('/api/data');
        renderRoadmap();
        renderAdmin();
        updateAll();
        closeWeekModal();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteWeek(number) {
    if (!confirm(`Woche ${number} und alle Tasks löschen?`)) return;
    try {
        await api('/api/admin/weeks/' + number, 'DELETE');
        appData = await api('/api/data');
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
            await api('/api/admin/tasks/' + editingTaskId, 'PUT', { type, text, hours });
            showToast('✓ Task aktualisiert');
        } else {
            await api('/api/admin/tasks', 'POST', { weekNumber: editingTaskWeekNumber, type, text, hours });
            showToast('✓ Task erstellt');
        }
        appData = await api('/api/data');
        renderRoadmap();
        renderAdmin();
        updateAll();
        closeTaskModal();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteTask(taskDbId, weekNumber) {
    if (!confirm('Task löschen?')) return;
    try {
        await api('/api/admin/tasks/' + taskDbId, 'DELETE');
        appData = await api('/api/data');
        renderRoadmap();
        renderAdmin();
        updateAll();
        showToast('✓ Task gelöscht');
    } catch { showToast('Fehler beim Löschen.'); }
}

// ── MEILENSTEINE ──
let milestoneData = [];

async function renderMilestones() {
    milestoneData = await api('/api/milestones');
    const container = document.getElementById('milestones-list');

    if (milestoneData.length === 0) {
        container.innerHTML = '<div class="empty-state" style="margin-top:40px">Noch keine Meilensteine gesetzt.<br>Klicke auf "+ Meilenstein setzen" um den aktuellen Stand einzufrieren.</div>';
        return;
    }

    container.innerHTML = '<div class="milestone-list">' +
        milestoneData.map(m => {
            const date = formatDateStr(m.createdAt.split(' ')[0]);
            const time = m.createdAt.split(' ')[1]?.substring(0, 5) ?? '';
            return `
            <div class="milestone-card">
                <div class="milestone-icon">🏁</div>
                <div>
                    <div class="milestone-name">${m.name}</div>
                    ${m.description ? `<div class="milestone-desc">${m.description}</div>` : ''}
                    <div class="milestone-meta">
                        <div class="milestone-meta-item">📅 <span>${date} ${time}</span></div>
                    </div>
                </div>
                <div class="milestone-actions">
                    <button class="btn-icon" onclick="openMilestoneDetail(${m.id})">🔍 Ansehen</button>
                    <button class="btn-icon danger" onclick="deleteMilestone(${m.id})">🗑</button>
                </div>
            </div>`;
        }).join('') + '</div>';
}

function openMilestoneModal() {
    document.getElementById('ms-name').value = '';
    document.getElementById('ms-description').value = '';
    document.getElementById('milestone-modal').classList.add('open');
}

function closeMilestoneModal() {
    document.getElementById('milestone-modal').classList.remove('open');
}

async function saveMilestone() {
    const name = document.getElementById('ms-name').value.trim();
    const description = document.getElementById('ms-description').value.trim() || null;

    if (!name) { showToast('Bitte einen Namen eingeben.'); return; }

    const snapshot = {
        createdAt: new Date().toISOString(),
        fortschritt: Object.values(state).filter(Boolean).length,
        totalTasks: appData.weeks.reduce((s, w) => s + w.tasks.length, 0),
        state: { ...state },
        weeks: appData.weeks,
        expenses: financeData.expenses,
        totalExpenses: financeData.totalExpenses,
        categories: financeData.categories,
        summary: financeData.summary
    };

    try {
        await api('/api/milestones', 'POST', { name, description, snapshot: JSON.stringify(snapshot) });
        closeMilestoneModal();
        renderMilestones();
        showToast('✓ Meilenstein gespeichert');
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteMilestone(id) {
    if (!confirm('Meilenstein löschen?')) return;
    try {
        await api('/api/milestones/' + id, 'DELETE');
        renderMilestones();
        showToast('✓ Meilenstein gelöscht');
    } catch { showToast('Fehler beim Löschen.'); }
}

async function openMilestoneDetail(id) {
    const milestone = await api('/api/milestones/' + id);
    const snap = JSON.parse(milestone.snapshot);
    const currency = getCurrency();

    document.getElementById('ms-detail-title').textContent = milestone.name;

    const pct = snap.totalTasks > 0 ? Math.round((snap.fortschritt / snap.totalTasks) * 100) : 0;
    const date = formatDateStr(milestone.createdAt.split(' ')[0]);
    const time = milestone.createdAt.split(' ')[1]?.substring(0, 5) ?? '';

    let html = `
        <div class="ms-detail-section">
            <div class="ms-detail-label">Übersicht</div>
            <div class="ms-stat-grid">
                <div class="ms-stat"><div class="ms-stat-val">${pct}%</div><div class="ms-stat-label">Fortschritt</div></div>
                <div class="ms-stat"><div class="ms-stat-val">${snap.fortschritt}/${snap.totalTasks}</div><div class="ms-stat-label">Tasks erledigt</div></div>
                <div class="ms-stat"><div class="ms-stat-val">${currency} ${fmtChf(snap.totalExpenses ?? 0)}</div><div class="ms-stat-label">Ausgaben</div></div>
            </div>
            <div style="font-size:12px;color:var(--text3)">Gespeichert am ${date} um ${time} Uhr</div>
            ${milestone.description ? `<div style="font-size:13px;color:var(--text2);margin-top:8px">${milestone.description}</div>` : ''}
        </div>`;

    if (snap.weeks?.length > 0) {
        snap.weeks.forEach(week => {
            const wDone = week.tasks.filter(t => snap.state?.['task-' + t.id]).length;
            const wTotal = week.tasks.length;
            const wPct = wTotal > 0 ? Math.round((wDone / wTotal) * 100) : 0;

            html += `
            <div class="ms-detail-section">
                <div class="ms-detail-label">
                    W0${week.number} — ${week.title}
                    <span style="float:right;color:${wPct === 100 ? 'var(--green)' : 'var(--text2)'}">
                        ${wDone}/${wTotal} (${wPct}%)
                    </span>
                </div>
                <table class="ms-detail-table">
                    <tr><th>Status</th><th>Typ</th><th>Task</th><th>Zeit</th></tr>`;

            week.tasks.forEach(task => {
                const isDone = !!snap.state?.['task-' + task.id];
                html += `<tr>
                    <td style="color:${isDone ? 'var(--green)' : 'var(--text3)'}">${isDone ? '✓ Erledigt' : '○ Offen'}</td>
                    <td><span style="font-size:10px;padding:2px 6px;border-radius:99px;background:${task.type === 'pc' ? 'var(--accent-dim)' : 'var(--green-dim)'};color:${task.type === 'pc' ? 'var(--accent)' : 'var(--green)'}">
                        ${task.type === 'pc' ? 'PC' : 'Physisch'}
                    </span></td>
                    <td style="color:${isDone ? 'var(--text3)' : 'var(--text)'}">
                        ${isDone ? '<s>' : ''}${task.text}${isDone ? '</s>' : ''}
                    </td>
                    <td style="white-space:nowrap">${task.hours}</td>
                </tr>`;
            });
            html += '</table></div>';
        });
    }

    if (snap.expenses?.length > 0) {
        html += `<div class="ms-detail-section">
            <div class="ms-detail-label">Ausgaben (${snap.expenses.length})</div>
            <table class="ms-detail-table">
                <tr><th>Datum</th><th>Kategorie</th><th>Beschreibung</th><th>Betrag</th></tr>`;
        snap.expenses.forEach(e => {
            html += `<tr>
                <td>${formatDateStr(e.date)}</td>
                <td>${e.categoryName}</td>
                <td>${e.description}</td>
                <td style="color:var(--red)">−${currency} ${fmtChf(e.amount)}</td>
            </tr>`;
        });
        html += '</table></div>';
    }

    document.getElementById('ms-detail-body').innerHTML = html;
    window._currentMilestone = { milestone, snap };
    document.getElementById('milestone-detail-modal').classList.add('open');
}

function closeMilestoneDetailModal() {
    document.getElementById('milestone-detail-modal').classList.remove('open');
}

function exportMilestonePdf() {
    if (!window._currentMilestone) return;
    const { milestone } = window._currentMilestone;
    const content = document.getElementById('ms-detail-body').innerHTML;
    const win = window.open('', '_blank');
    win.document.write(`<!DOCTYPE html><html><head>
        <meta charset="UTF-8">
        <title>Meilenstein — ${milestone.name}</title>
        <style>
            body { font-family: Arial, sans-serif; font-size: 13px; color: #1a1a1a; padding: 32px; }
            h1 { font-size: 22px; margin-bottom: 4px; }
            table { width: 100%; border-collapse: collapse; margin-bottom: 16px; }
            th { font-size: 10px; text-transform: uppercase; color: #888; padding: 6px 8px; border-bottom: 2px solid #eee; text-align: left; }
            td { padding: 6px 8px; border-bottom: 1px solid #f0f0f0; }
            .ms-stat-grid { display: grid; grid-template-columns: repeat(3,1fr); gap: 12px; margin-bottom: 16px; }
            .ms-stat { background: #f8f8f8; border-radius: 8px; padding: 12px; }
            .ms-stat-val { font-size: 20px; font-weight: 700; }
            .ms-stat-label { font-size: 11px; color: #888; }
            .ms-detail-label { font-size: 10px; text-transform: uppercase; color: #888; letter-spacing: 0.1em; margin: 16px 0 8px; border-bottom: 1px solid #eee; padding-bottom: 4px; }
            @media print { body { padding: 0; } }
        </style></head><body>
        <h1>Meilenstein: ${milestone.name}</h1>
        ${content}
        <script>window.onload = () => { window.print(); }<\/script>
        </body></html>`);
    win.document.close();
}

function exportMilestoneExcel() {
    if (!window._currentMilestone) return;
    const { milestone, snap } = window._currentMilestone;
    const currency = getCurrency();
    const date = formatDateStr(milestone.createdAt.split(' ')[0]);

    let csv = `Meilenstein;${milestone.name}\n`;
    csv += `Datum;${date}\n`;
    csv += `Fortschritt;${snap.fortschritt}/${snap.totalTasks}\n`;
    csv += `Gesamtausgaben;${currency} ${fmtChf(snap.totalExpenses ?? 0)}\n\n`;

    if (snap.weeks?.length > 0) {
        csv += `Woche;Titel;Erledigt;Total;%\n`;
        snap.weeks.forEach(week => {
            const wDone = week.tasks.filter(t => snap.state?.['task-' + t.id]).length;
            const wTotal = week.tasks.length;
            const wPct = wTotal > 0 ? Math.round((wDone / wTotal) * 100) : 0;
            csv += `W0${week.number};${week.title};${wDone};${wTotal};${wPct}%\n`;

            week.tasks.forEach(task => {
                const isDone = !!snap.state?.['task-' + task.id];
                csv += `;${isDone ? 'Erledigt' : 'Offen'};${task.type === 'pc' ? 'PC' : 'Physisch'};${task.text};${task.hours}\n`;
            });
            csv += '\n';
        });
    }

    if (snap.expenses?.length > 0) {
        csv += `Datum;Kategorie;Beschreibung;Betrag\n`;
        snap.expenses.forEach(e => {
            csv += `${formatDateStr(e.date)};${e.categoryName};${e.description};-${fmtChf(e.amount)}\n`;
        });
    }

    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Meilenstein_${milestone.name.replace(/\s+/g, '_')}_${date}.csv`;
    a.click();
    URL.revokeObjectURL(url);
}

init();