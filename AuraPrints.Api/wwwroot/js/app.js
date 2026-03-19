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
        const res = await fetch('/api/import', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(_importData)
        });
        const data = await res.json();
        if (!res.ok) {
            showToast('Import fehlgeschlagen: ' + (data.error ?? 'Unbekannter Fehler'));
            return;
        }
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
    const idx = { overview: 0, roadmap: 1, produkte: 2, finanzen: 3, meilensteine: 4, admin: 5, einstellungen: 6 };
    const navItems = document.querySelectorAll('.nav-item');
    if (idx[id] !== undefined && navItems[idx[id]]) navItems[idx[id]].classList.add('active');
    if (id === 'admin') renderAdmin();
    if (id === 'meilensteine') renderMilestones();
    if (id === 'produkte') renderProdukte();
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

// ── PRODUKTE GENERISCH ──
let productData2 = null;
let activeProductTypeId = null;
let editingProductId = null;
let managingFieldsTypeId = null;

async function loadProducts() {
    productData2 = await api('/api/products2');
}

async function renderProdukte2() {
    await loadProducts();
    const tabs = document.getElementById('product-type-tabs');
    const grid = document.getElementById('product-cards-grid');
    const addBtn = document.getElementById('add-product-btn');

    if (productData2.productTypes.length === 0) {
        tabs.innerHTML = '';
        grid.innerHTML = `
            <div style="grid-column:1/-1">
                <div class="empty-state" style="margin-top:40px">
                    Noch keine Produkttypen definiert.<br>
                    Klicke auf "⚙️ Typen verwalten" um deinen ersten Produkttyp zu erstellen.
                </div>
            </div>`;
        addBtn.style.display = 'none';
        return;
    }

    // Aktiven Typ setzen falls noch keiner aktiv
    if (!activeProductTypeId || !productData2.productTypes.find(t => t.id === activeProductTypeId)) {
        activeProductTypeId = productData2.productTypes[0].id;
    }

    // Tabs rendern
    tabs.innerHTML = productData2.productTypes.map(t => `
        <button class="product-type-tab ${t.id === activeProductTypeId ? 'active' : ''}"
            style="${t.id === activeProductTypeId ? `background:${t.color}` : ''}"
            onclick="switchProductType(${t.id})">
            <div class="product-type-dot" style="background:${t.id === activeProductTypeId ? '#fff' : t.color}"></div>
            ${t.name}
            <span style="font-size:11px;opacity:0.7">(${productData2.products.filter(p => p.productTypeId === t.id).length})</span>
        </button>`).join('');

    addBtn.style.display = 'block';
    renderProductCards();
}

function switchProductType(id) {
    activeProductTypeId = id;
    renderProdukte2();
}

function renderProductCards() {
    const grid = document.getElementById('product-cards-grid');
    const type = productData2.productTypes.find(t => t.id === activeProductTypeId);
    const products = productData2.products.filter(p => p.productTypeId === activeProductTypeId);

    if (!type) return;

    if (products.length === 0) {
        grid.innerHTML = `
            <div style="grid-column:1/-1">
                <div class="empty-state" style="margin-top:20px">
                    Noch keine Produkte vom Typ "${type.name}".<br>
                    Klicke auf "+ Produkt hinzufügen".
                </div>
            </div>`;
        return;
    }

    grid.innerHTML = products.map(p => {
        const values = JSON.parse(p.fieldValues);
        const date = formatDateStr(p.createdAt.split(' ')[0]);

        // Ersten Feldwert als Titel nehmen
        const firstField = type.fields[0];
        const title = firstField ? (values[firstField.id] || '—') : 'Produkt #' + p.id;

        let fieldsHtml = type.fields.slice(1).map(f => {
            const val = values[f.id];
            if (!val) return '';
            let displayVal = val;
            if (f.fieldType === 'url') {
                displayVal = `<a href="${val}" target="_blank">🔗 Link öffnen</a>`;
            }
            return `
                <div class="product-field-row">
                    <div class="product-field-label">${f.name}</div>
                    <div class="product-field-value">${displayVal}</div>
                </div>`;
        }).join('');

        return `
            <div class="product-card-generic">
                <div class="product-card-header">
                    <div style="font-size:14px;font-weight:600;color:var(--text);flex:1">${title}</div>
                    <div class="product-card-actions">
                        <button class="btn-icon" onclick="openEditProductModal(${p.id})">✏️</button>
                        <button class="btn-icon danger" onclick="deleteProduct(${p.id})">🗑</button>
                    </div>
                </div>
                <div class="product-card-body">
                    ${fieldsHtml}
                    <div class="product-card-date">Erfasst am ${date}</div>
                </div>
            </div>`;
    }).join('');
}

// ── PRODUKTTYPEN VERWALTEN ──
function openManageTypesModal() {
    renderTypesList();
    document.getElementById('manage-types-modal').classList.add('open');
}

function closeManageTypesModal() {
    document.getElementById('manage-types-modal').classList.remove('open');
    renderProdukte2();
}

function renderTypesList() {
    const list = document.getElementById('types-list');
    if (!productData2 || productData2.productTypes.length === 0) {
        list.innerHTML = '<div class="empty-state">Noch keine Typen erstellt.</div>';
        return;
    }
    list.innerHTML = productData2.productTypes.map(t => `
        <div style="display:flex;align-items:center;gap:10px;padding:10px 12px;background:var(--bg2);border-radius:8px;margin-bottom:8px;border:1px solid var(--border)">
            <div style="width:12px;height:12px;border-radius:50%;background:${t.color};flex-shrink:0"></div>
            <div style="flex:1">
                <div style="font-size:13px;font-weight:500;color:var(--text)">${t.name}</div>
                ${t.description ? `<div style="font-size:11px;color:var(--text3)">${t.description}</div>` : ''}
            </div>
            <div style="font-size:11px;color:var(--text3)">${t.fields.length} Felder</div>
            <button class="btn-icon" onclick="openManageFieldsModal(${t.id})">⚙️ Felder</button>
            <button class="btn-icon danger" onclick="deleteProductType(${t.id})">🗑</button>
        </div>`).join('');
}

async function addProductType() {
    const name = document.getElementById('new-type-name').value.trim();
    const color = document.getElementById('new-type-color').value;
    const desc = document.getElementById('new-type-desc').value.trim() || null;
    if (!name) { showToast('Bitte einen Namen eingeben.'); return; }
    await api('/api/product-types', 'POST', { name, color, description: desc });
    productData2 = await api('/api/products2');
    renderTypesList();
    document.getElementById('new-type-name').value = '';
    document.getElementById('new-type-desc').value = '';
    showToast('✓ Produkttyp erstellt');
}

async function deleteProductType(id) {
    if (!confirm('Produkttyp und alle zugehörigen Produkte löschen?')) return;
    await api('/api/product-types/' + id, 'DELETE');
    productData2 = await api('/api/products2');
    if (activeProductTypeId === id) activeProductTypeId = null;
    renderTypesList();
    showToast('✓ Produkttyp gelöscht');
}

// ── FELDER VERWALTEN ──
function openManageFieldsModal(typeId) {
    managingFieldsTypeId = typeId;
    const type = productData2.productTypes.find(t => t.id === typeId);
    document.getElementById('manage-fields-title').textContent = `Felder — ${type?.name}`;
    renderFieldsList();
    document.getElementById('manage-fields-modal').classList.add('open');
}

function closeManageFieldsModal() {
    document.getElementById('manage-fields-modal').classList.remove('open');
}

function renderFieldsList() {
    const list = document.getElementById('fields-list');
    const type = productData2.productTypes.find(t => t.id === managingFieldsTypeId);
    if (!type || type.fields.length === 0) {
        list.innerHTML = '<div class="empty-state">Noch keine Felder. Füge unten ein Feld hinzu.</div>';
        return;
    }
    list.innerHTML = '<div class="field-list">' + type.fields.map(f => `
        <div class="field-row">
            <div class="field-row-name">${f.name}${f.required ? ' *' : ''}</div>
            <div class="field-row-type">${f.fieldType}</div>
            ${f.options ? `<div style="font-size:11px;color:var(--text3)">${f.options}</div>` : '<div></div>'}
            <button class="btn-icon danger" onclick="deleteField(${f.id})">🗑</button>
        </div>`).join('') + '</div>';
}

function toggleFieldOptions() {
    const type = document.getElementById('new-field-type').value;
    document.getElementById('field-options-wrap').style.display = type === 'select' ? 'flex' : 'none';
}

async function addField() {
    const name = document.getElementById('new-field-name').value.trim();
    const fieldType = document.getElementById('new-field-type').value;
    const options = fieldType === 'select' ? document.getElementById('new-field-options').value.trim() : null;
    const required = document.getElementById('new-field-required').checked;

    if (!name) { showToast('Bitte einen Feldnamen eingeben.'); return; }

    const type = productData2.productTypes.find(t => t.id === managingFieldsTypeId);
    const sortOrder = (type?.fields.length ?? 0) + 1;

    await api(`/api/product-types/${managingFieldsTypeId}/fields`, 'POST', { name, fieldType, options, required, sortOrder });
    productData2 = await api('/api/products2');
    renderFieldsList();
    document.getElementById('new-field-name').value = '';
    document.getElementById('new-field-options').value = '';
    document.getElementById('new-field-required').checked = false;
    showToast('✓ Feld hinzugefügt');
}

async function deleteField(id) {
    if (!confirm('Feld löschen? Bestehende Produktdaten in diesem Feld gehen verloren.')) return;
    await api('/api/product-fields/' + id, 'DELETE');
    productData2 = await api('/api/products2');
    renderFieldsList();
    showToast('✓ Feld gelöscht');
}

// ── PRODUKT MODAL ──
function buildProductForm(typeId, existingValues = {}) {
    const type = productData2.productTypes.find(t => t.id === typeId);
    if (!type || type.fields.length === 0) {
        return '<div class="empty-state">Dieser Typ hat noch keine Felder. Bitte zuerst Felder definieren.</div>';
    }

    return type.fields.map(f => {
        const val = existingValues[f.id] ?? '';
        let input = '';

        if (f.fieldType === 'text') {
            input = `<input type="text" id="pf-${f.id}" value="${val}" placeholder="${f.name}${f.required ? ' (Pflicht)' : ''}">`;
        } else if (f.fieldType === 'number') {
            input = `<input type="number" id="pf-${f.id}" value="${val}" placeholder="0" step="0.01">`;
        } else if (f.fieldType === 'url') {
            input = `<input type="url" id="pf-${f.id}" value="${val}" placeholder="https://...">`;
        } else if (f.fieldType === 'textarea') {
            input = `<textarea id="pf-${f.id}" rows="3">${val}</textarea>`;
        } else if (f.fieldType === 'select') {
            const opts = (f.options ?? '').split(',').map(o => o.trim()).filter(Boolean);
            input = `<select id="pf-${f.id}">
                <option value="">— auswählen —</option>
                ${opts.map(o => `<option value="${o}" ${o === val ? 'selected' : ''}>${o}</option>`).join('')}
            </select>`;
        }

        return `<div class="form-group">
            <label>${f.name}${f.required ? ' *' : ''}</label>
            ${input}
        </div>`;
    }).join('');
}

function openAddProductModal() {
    editingProductId = null;
    const type = productData2.productTypes.find(t => t.id === activeProductTypeId);
    document.getElementById('product-modal-title').textContent = `${type?.name} hinzufügen`;
    document.getElementById('product-modal-body').innerHTML = buildProductForm(activeProductTypeId);
    document.getElementById('product-modal').classList.add('open');
}

function openEditProductModal(id) {
    const product = productData2.products.find(p => p.id === id);
    if (!product) return;
    editingProductId = id;
    const type = productData2.productTypes.find(t => t.id === product.productTypeId);
    const values = JSON.parse(product.fieldValues);
    document.getElementById('product-modal-title').textContent = `${type?.name} bearbeiten`;
    document.getElementById('product-modal-body').innerHTML = buildProductForm(product.productTypeId, values);
    document.getElementById('product-modal').classList.add('open');
}

function closeProductModal() {
    document.getElementById('product-modal').classList.remove('open');
}

async function saveProduct() {
    const typeId = editingProductId
        ? productData2.products.find(p => p.id === editingProductId)?.productTypeId
        : activeProductTypeId;

    const type = productData2.productTypes.find(t => t.id === typeId);
    if (!type) return;

    // Feldwerte sammeln
    const fieldValues = {};
    let valid = true;
    type.fields.forEach(f => {
        const el = document.getElementById('pf-' + f.id);
        const val = el?.value?.trim() ?? '';
        if (f.required && !val) { showToast(`"${f.name}" ist ein Pflichtfeld.`); valid = false; return; }
        fieldValues[f.id] = val;
    });
    if (!valid) return;

    try {
        if (editingProductId) {
            await api('/api/products2/' + editingProductId, 'PUT', { fieldValues });
            showToast('✓ Produkt aktualisiert');
        } else {
            await api('/api/products2', 'POST', { productTypeId: typeId, fieldValues });
            showToast('✓ Produkt hinzugefügt');
        }
        productData2 = await api('/api/products2');
        renderProductCards();
        renderProdukte2();
        closeProductModal();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteProduct(id) {
    if (!confirm('Produkt löschen?')) return;
    await api('/api/products2/' + id, 'DELETE');
    productData2 = await api('/api/products2');
    renderProductCards();
    renderProdukte2();
    showToast('✓ Produkt gelöscht');
}

// ── PRODUKTE KATALOG ──
let catalogData = null;
let activeCategoryId = null;
let editingCatalogProductId = null;
let managingAttributesCategoryId = null;
let editingVariationId = null;

async function loadCatalog() {
    catalogData = await api('/api/catalog');
}

async function renderProdukte() {
    await loadCatalog();

    // Aktive Kategorie setzen bevor wir rendern
    if (!activeCategoryId || !catalogData.categories.find(c => c.id === activeCategoryId)) {
        activeCategoryId = catalogData.categories[0]?.id ?? null;
    }

    renderCatalogSidebar();
    renderCatalogMain();
}

function renderCatalogSidebar() {
    const sidebar = document.getElementById('catalog-sidebar');
    const addBtn = document.getElementById('btn-add-product');
    if (addBtn) addBtn.style.display = activeCategoryId ? 'block' : 'none';

    let html = '';
    if (catalogData.categories.length === 0) {
        html = '<div class="empty-state" style="padding:20px 0;font-size:12px">Noch keine Kategorien.<br>Erstelle zuerst eine Kategorie.</div>';
    } else {
        html = catalogData.categories.map(cat => {
            const count = catalogData.products.filter(p => p.categoryId === cat.id).length;
            const isActive = cat.id === activeCategoryId;
            return `
            <button class="catalog-category-btn ${isActive ? 'active' : ''}"
                style="${isActive ? `background:${cat.color};border-color:${cat.color}` : ''}"
                onclick="switchCategory(${cat.id})">
                <div class="catalog-category-dot" style="background:${isActive ? '#fff' : cat.color}"></div>
                <span style="flex:1">${cat.name}</span>
                <span class="catalog-category-count">${count}</span>
            </button>`;
        }).join('');
    }

    html += `<button class="catalog-manage-btn" onclick="openManageCatalogModal()">⚙️ Kategorien verwalten</button>`;
    sidebar.innerHTML = html;
}

function renderCatalogMain() {
    const main = document.getElementById('catalog-main');

    if (!activeCategoryId) {
        main.innerHTML = '<div class="empty-state" style="margin-top:40px">Erstelle zuerst eine Kategorie in der Seitenleiste.</div>';
        return;
    }

    const category = catalogData.categories.find(c => c.id === activeCategoryId);
    const products = catalogData.products.filter(p => p.categoryId === activeCategoryId);

    if (!category) return;

    let html = `
        <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:16px">
            <div>
                <div style="font-size:16px;font-weight:600;color:var(--text)">${category.name}</div>
                ${category.description ? `<div style="font-size:12px;color:var(--text3)">${category.description}</div>` : ''}
            </div>
            <button class="btn-secondary" onclick="openManageAttributesModal(${category.id})">⚙️ Attribute (${category.attributes?.length ?? 0})</button>
        </div>`;

    if (products.length === 0) {
        html += `<div class="empty-state" style="margin-top:20px">
            Noch keine Produkte in dieser Kategorie.<br>
            Klicke auf "+ Produkt hinzufügen".
        </div>`;
    } else {
        html += products.map(p => renderProductCard(p, category)).join('');
    }

    main.innerHTML = html;
}

function renderProductCard(product, category) {
    const attrValues = typeof product.attributeValues === 'string'
        ? JSON.parse(product.attributeValues || '{}')
        : (product.attributeValues ?? {});

    // Attribute anzeigen
    let attrsHtml = '';
    if (category.attributes && category.attributes.length > 0) {
        const attrItems = category.attributes
            .filter(a => attrValues[a.id])
            .map(a => `
                <div class="catalog-attr-item">
                    <div class="catalog-attr-label">${a.name}</div>
                    <div class="catalog-attr-value">${attrValues[a.id]}</div>
                </div>`).join('');
        if (attrItems) {
            attrsHtml = `<div class="catalog-attr-grid">${attrItems}</div>`;
        }
    }

    // Variationen
    let variationsHtml = '';
    if (product.variations && product.variations.length > 0) {
        const currency = getCurrency();
        const rows = product.variations.map(v => `
            <div class="catalog-variation-row">
                <div class="catalog-variation-name">${v.name}</div>
                <div class="catalog-variation-sku">${v.sku}</div>
                <div class="catalog-variation-price">${currency} ${fmtChf(v.price)}</div>
                <div class="catalog-variation-stock">
                    <span class="stock-badge ${v.stock > 0 ? 'stock-ok' : 'stock-zero'}">
                        ${v.stock > 0 ? v.stock + ' Stk.' : 'Kein Lager'}
                    </span>
                </div>
                <button class="btn-icon" onclick="openEditVariationModal(${v.id}, ${product.id})">✏️</button>
                <button class="btn-icon danger" onclick="deleteVariation(${v.id})">🗑</button>
            </div>`).join('');

        variationsHtml = `
            <div class="catalog-variations-title">
                Variationen (${product.variations.length})
                <button class="btn-secondary" style="font-size:11px;padding:3px 10px" onclick="openAddVariationModal(${product.id})">+ Variation</button>
            </div>
            ${rows}`;
    } else {
        variationsHtml = `
            <div class="catalog-variations-title">Variationen</div>
            <div style="font-size:12px;color:var(--text3);margin-bottom:8px">Noch keine Variationen.</div>
            <button class="btn-secondary" onclick="openAddVariationModal(${product.id})">+ Erste Variation hinzufügen</button>`;
    }

    return `
        <div class="catalog-product-card">
            <div class="catalog-product-header" onclick="toggleProductCard(${product.id})">
                <div>
                    <div class="catalog-product-name">${product.name}</div>
                    ${product.description ? `<div class="catalog-product-desc">${product.description}</div>` : ''}
                </div>
                <div style="display:flex;gap:6px;margin-left:12px">
                    <span style="font-size:11px;color:var(--text3)">${product.variations?.length ?? 0} Variationen</span>
                    <button class="btn-icon" onclick="event.stopPropagation();openEditProductModal(${product.id})">✏️</button>
                    <button class="btn-icon danger" onclick="event.stopPropagation();deleteCatalogProduct(${product.id})">🗑</button>
                </div>
            </div>
            <div class="catalog-product-body" id="product-body-${product.id}">
                ${attrsHtml}
                ${variationsHtml}
            </div>
        </div>`;
}

function toggleProductCard(id) {
    const body = document.getElementById('product-body-' + id);
    if (body) body.classList.toggle('open');
}

function switchCategory(id) {
    activeCategoryId = id;
    renderCatalogSidebar();
    renderCatalogMain();
}

// ── KATEGORIEN MODAL ──
function openManageCatalogModal() {
    renderCatalogCategoriesList();
    document.getElementById('manage-catalog-modal').classList.add('open');
}

function closeManageCatalogModal() {
    document.getElementById('manage-catalog-modal').classList.remove('open');
    renderProdukte();
}

function renderCatalogCategoriesList() {
    const list = document.getElementById('catalog-categories-list');
    if (!catalogData.categories.length) {
        list.innerHTML = '<div class="empty-state">Noch keine Kategorien.</div>';
        return;
    }
    list.innerHTML = catalogData.categories.map(cat => `
        <div style="display:flex;align-items:center;gap:10px;padding:10px 12px;background:var(--bg2);border-radius:8px;margin-bottom:8px;border:1px solid var(--border)">
            <div style="width:12px;height:12px;border-radius:50%;background:${cat.color};flex-shrink:0"></div>
            <div style="flex:1">
                <div style="font-size:13px;font-weight:500;color:var(--text)">${cat.name}</div>
                ${cat.description ? `<div style="font-size:11px;color:var(--text3)">${cat.description}</div>` : ''}
            </div>
            <span style="font-size:11px;color:var(--text3)">${cat.attributes?.length ?? 0} Attribute</span>
            <button class="btn-icon" onclick="openManageAttributesModal(${cat.id});closeManageCatalogModal()">⚙️ Attribute</button>
            <button class="btn-icon danger" onclick="deleteCatalogCategory(${cat.id})">🗑</button>
        </div>`).join('');
}

async function addCatalogCategory() {
    const name = document.getElementById('new-cat-type-name').value.trim();
    const color = document.getElementById('new-cat-type-color').value;
    const desc = document.getElementById('new-cat-type-desc').value.trim() || null;
    if (!name) { showToast('Bitte einen Namen eingeben.'); return; }
    await api('/api/catalog/categories', 'POST', { name, color, description: desc });
    catalogData = await api('/api/catalog');
    renderCatalogCategoriesList();
    document.getElementById('new-cat-type-name').value = '';
    document.getElementById('new-cat-type-desc').value = '';
    showToast('✓ Kategorie erstellt');
}

async function deleteCatalogCategory(id) {
    const cat = catalogData.categories.find(c => c.id === id);
    const count = catalogData.products.filter(p => p.categoryId === id).length;
    if (!confirm(`Kategorie "${cat?.name}" löschen? ${count > 0 ? `${count} Produkte werden ebenfalls gelöscht.` : ''}`)) return;
    await api('/api/catalog/categories/' + id, 'DELETE');
    catalogData = await api('/api/catalog');
    if (activeCategoryId === id) activeCategoryId = null;
    renderCatalogCategoriesList();
    showToast('✓ Kategorie gelöscht');
}

// ── ATTRIBUTE MODAL ──
function openManageAttributesModal(categoryId) {
    managingAttributesCategoryId = categoryId;
    const cat = catalogData.categories.find(c => c.id === categoryId);
    document.getElementById('manage-attr-title').textContent = `Attribute — ${cat?.name}`;
    renderAttributesList();
    document.getElementById('manage-attributes-modal').classList.add('open');
}

function closeManageAttributesModal() {
    document.getElementById('manage-attributes-modal').classList.remove('open');
    renderProdukte();
}

function renderAttributesList() {
    const list = document.getElementById('attributes-list');
    const cat = catalogData.categories.find(c => c.id === managingAttributesCategoryId);
    if (!cat || cat.attributes.length === 0) {
        list.innerHTML = '<div class="empty-state">Noch keine Attribute. Füge unten ein Attribut hinzu.</div>';
        return;
    }
    list.innerHTML = '<div class="attr-list">' + cat.attributes.map(a => `
        <div class="attr-row">
            <div class="attr-row-name">${a.name}${a.required ? ' *' : ''}</div>
            <div class="attr-row-type">${a.fieldType}</div>
            ${a.options ? `<div style="font-size:11px;color:var(--text3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:120px">${a.options}</div>` : '<div></div>'}
            <button class="btn-icon danger" onclick="deleteCatalogAttribute(${a.id})">🗑</button>
        </div>`).join('') + '</div>';
}

function toggleAttrOptions() {
    const type = document.getElementById('new-attr-type').value;
    document.getElementById('attr-options-wrap').style.display = type === 'select' ? 'flex' : 'none';
}

async function addCatalogAttribute() {
    const name = document.getElementById('new-attr-name').value.trim();
    const fieldType = document.getElementById('new-attr-type').value;
    const options = fieldType === 'select' ? document.getElementById('new-attr-options').value.trim() : null;
    const required = document.getElementById('new-attr-required').checked;
    if (!name) { showToast('Bitte einen Namen eingeben.'); return; }
    const cat = catalogData.categories.find(c => c.id === managingAttributesCategoryId);
    const sortOrder = (cat?.attributes?.length ?? 0) + 1;
    await api(`/api/catalog/categories/${managingAttributesCategoryId}/attributes`, 'POST', { name, fieldType, options, required, sortOrder });
    catalogData = await api('/api/catalog');
    renderAttributesList();
    document.getElementById('new-attr-name').value = '';
    document.getElementById('new-attr-options').value = '';
    document.getElementById('new-attr-required').checked = false;
    showToast('✓ Attribut hinzugefügt');
}

async function deleteCatalogAttribute(id) {
    if (!confirm('Attribut löschen?')) return;
    await api('/api/catalog/attributes/' + id, 'DELETE');
    catalogData = await api('/api/catalog');
    renderAttributesList();
    showToast('✓ Attribut gelöscht');
}

// ── PRODUKT MODAL ──
function buildProductForm(categoryId, existingProduct = null) {
    const cat = catalogData.categories.find(c => c.id === categoryId);
    const values = existingProduct ? JSON.parse(existingProduct.attributeValues || '{}') : {};

    let html = `
        <div class="form-group">
            <label>Produktname *</label>
            <input type="text" id="cp-name" value="${existingProduct?.name ?? ''}" placeholder="z.B. Buchstabe A">
        </div>
        <div class="form-group">
            <label>Beschreibung (optional)</label>
            <textarea id="cp-desc" rows="2">${existingProduct?.description ?? ''}</textarea>
        </div>`;

    if (cat?.attributes?.length > 0) {
        html += `<div style="font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.08em;color:var(--text3);margin:16px 0 8px">Attribute</div>`;
        html += cat.attributes.map(a => {
            const val = values[a.id] ?? '';
            let input = '';
            if (a.fieldType === 'select') {
                const opts = (a.options ?? '').split(',').map(o => o.trim()).filter(Boolean);
                input = `<select id="cp-attr-${a.id}">
                    <option value="">— auswählen —</option>
                    ${opts.map(o => `<option value="${o}" ${o === val ? 'selected' : ''}>${o}</option>`).join('')}
                </select>`;
            } else if (a.fieldType === 'number') {
                input = `<input type="number" id="cp-attr-${a.id}" value="${val}" step="0.01">`;
            } else if (a.fieldType === 'url') {
                input = `<input type="url" id="cp-attr-${a.id}" value="${val}" placeholder="https://...">`;
            } else if (a.fieldType === 'textarea') {
                input = `<textarea id="cp-attr-${a.id}" rows="2">${val}</textarea>`;
            } else {
                input = `<input type="text" id="cp-attr-${a.id}" value="${val}" placeholder="${a.name}">`;
            }
            return `<div class="form-group"><label>${a.name}${a.required ? ' *' : ''}</label>${input}</div>`;
        }).join('');
    }

    return html;
}

function openAddProductModal() {
    editingCatalogProductId = null;
    const cat = catalogData.categories.find(c => c.id === activeCategoryId);
    document.getElementById('catalog-product-modal-title').textContent = `Produkt hinzufügen — ${cat?.name}`;
    document.getElementById('catalog-product-modal-body').innerHTML = buildProductForm(activeCategoryId);
    document.getElementById('catalog-product-modal').classList.add('open');
}

function openEditProductModal(id) {
    const product = catalogData.products.find(p => p.id === id);
    if (!product) return;
    editingCatalogProductId = id;
    const cat = catalogData.categories.find(c => c.id === product.categoryId);
    document.getElementById('catalog-product-modal-title').textContent = `Produkt bearbeiten — ${cat?.name}`;
    document.getElementById('catalog-product-modal-body').innerHTML = buildProductForm(product.categoryId, product);
    document.getElementById('catalog-product-modal').classList.add('open');
}

function closeCatalogProductModal() {
    document.getElementById('catalog-product-modal').classList.remove('open');
}

async function saveCatalogProduct() {
    const name = document.getElementById('cp-name').value.trim();
    const desc = document.getElementById('cp-desc').value.trim() || null;
    if (!name) { showToast('Bitte einen Produktnamen eingeben.'); return; }

    const categoryId = editingCatalogProductId
        ? catalogData.products.find(p => p.id === editingCatalogProductId)?.categoryId
        : activeCategoryId;

    const cat = catalogData.categories.find(c => c.id === categoryId);
    const attributeValues = {};
    let valid = true;

    cat?.attributes?.forEach(a => {
        const el = document.getElementById('cp-attr-' + a.id);
        const val = el?.value?.trim() ?? '';
        if (a.required && !val) { showToast(`"${a.name}" ist ein Pflichtfeld.`); valid = false; return; }
        attributeValues[a.id] = val;
    });

    if (!valid) return;

    try {
        if (editingCatalogProductId) {
            await api('/api/catalog/products/' + editingCatalogProductId, 'PUT', { name, description: desc, attributeValues: JSON.stringify(attributeValues) });
            showToast('✓ Produkt aktualisiert');
        } else {
            await api('/api/catalog/products', 'POST', { categoryId, name, description: desc, attributeValues: JSON.stringify(attributeValues) });
            showToast('✓ Produkt hinzugefügt');
        }
        catalogData = await api('/api/catalog');
        closeCatalogProductModal();
        renderCatalogMain();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteCatalogProduct(id) {
    const product = catalogData.products.find(p => p.id === id);
    if (!confirm(`Produkt "${product?.name}" löschen? Alle Variationen werden ebenfalls gelöscht.`)) return;
    await api('/api/catalog/products/' + id, 'DELETE');
    catalogData = await api('/api/catalog');
    renderCatalogMain();
    showToast('✓ Produkt gelöscht');
}

// ── VARIATION MODAL ──
function openAddVariationModal(productId) {
    editingVariationId = null;
    document.getElementById('variation-modal-title').textContent = 'Variation hinzufügen';
    document.getElementById('var-product-id').value = productId;
    document.getElementById('var-edit-id').value = '';
    document.getElementById('var-name').value = '';
    document.getElementById('var-sku').value = '';
    document.getElementById('var-price').value = '';
    document.getElementById('var-stock').value = '0';
    document.getElementById('var-currency').textContent = getCurrency();
    document.getElementById('sku-error').style.display = 'none';
    document.getElementById('variation-modal').classList.add('open');
}

function openEditVariationModal(variationId, productId) {
    const product = catalogData.products.find(p => p.id === productId);
    const variation = product?.variations.find(v => v.id === variationId);
    if (!variation) return;

    editingVariationId = variationId;
    document.getElementById('variation-modal-title').textContent = 'Variation bearbeiten';
    document.getElementById('var-product-id').value = productId;
    document.getElementById('var-edit-id').value = variationId;
    document.getElementById('var-name').value = variation.name;
    document.getElementById('var-sku').value = variation.sku;
    document.getElementById('var-price').value = variation.price;
    document.getElementById('var-stock').value = variation.stock;
    document.getElementById('var-currency').textContent = getCurrency();
    document.getElementById('sku-error').style.display = 'none';
    document.getElementById('variation-modal').classList.add('open');
}

function closeVariationModal() {
    document.getElementById('variation-modal').classList.remove('open');
}

async function generateSku() {
    const productId = parseInt(document.getElementById('var-product-id').value);
    const varName = document.getElementById('var-name').value.trim();
    if (!varName) { showToast('Bitte zuerst den Variationsnamen eingeben.'); return; }
    try {
        const res = await fetch(`/api/catalog/products/${productId}/sku?variationName=${encodeURIComponent(varName)}`);
        const data = await res.json();
        document.getElementById('var-sku').value = data.sku;
    } catch { showToast('Fehler beim Generieren der SKU.'); }
}

async function saveVariation() {
    const productId = parseInt(document.getElementById('var-product-id').value);
    const name = document.getElementById('var-name').value.trim();
    const sku = document.getElementById('var-sku').value.trim();
    const price = parseFloat(document.getElementById('var-price').value) || 0;
    const stock = parseInt(document.getElementById('var-stock').value) || 0;
    const skuError = document.getElementById('sku-error');

    if (!name) { showToast('Bitte einen Namen eingeben.'); return; }
    if (!sku) { showToast('Bitte eine SKU eingeben oder generieren.'); return; }

    skuError.style.display = 'none';

    try {
        if (editingVariationId) {
            const res = await fetch('/api/catalog/variations/' + editingVariationId, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name, sku, price, stock })
            });
            const data = await res.json();
            if (data.error) { skuError.textContent = data.error; skuError.style.display = 'block'; return; }
            showToast('✓ Variation aktualisiert');
        } else {
            const res = await fetch('/api/catalog/variations', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ productId, name, sku, price, stock })
            });
            const data = await res.json();
            if (data.error) { skuError.textContent = data.error; skuError.style.display = 'block'; return; }
            showToast('✓ Variation hinzugefügt');
        }
        catalogData = await api('/api/catalog');
        closeVariationModal();
        renderCatalogMain();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteVariation(id) {
    if (!confirm('Variation löschen?')) return;
    await api('/api/catalog/variations/' + id, 'DELETE');
    catalogData = await api('/api/catalog');
    renderCatalogMain();
    showToast('✓ Variation gelöscht');
}

init();