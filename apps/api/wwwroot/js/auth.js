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

    const proj = await api('/api/projects', 'POST', { name, description: desc, startDate: start, currency });
    _currentProjectId = proj.id;
    _currentProject = proj;
    _projects = [proj];
    document.getElementById('setup-screen').style.display = 'none';
    await loadProject();
}

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

// ── AUTH ──
let _currentUser = null;

async function checkAuth() {
    try {
        const res = await fetch('/api/auth/me');
        if (res.status === 401) { showLoginScreen(); return false; }
        _currentUser = await res.json();
        return true;
    } catch {
        showLoginScreen();
        return false;
    }
}

function showLoginScreen() {
    _currentUser = null;
    document.getElementById('login-screen').style.display = 'flex';
    document.getElementById('setup-screen').style.display = 'none';
    document.getElementById('project-screen').style.display = 'none';
    document.getElementById('app').style.display = 'none';
    document.getElementById('login-username').focus();
}

async function doLogin() {
    const username = document.getElementById('login-username').value.trim();
    const pw = document.getElementById('login-password').value;
    const errEl = document.getElementById('login-error');
    errEl.style.display = 'none';
    try {
        const res = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password: pw })
        });
        if (res.ok) {
            _currentUser = await res.json();
            document.getElementById('login-screen').style.display = 'none';
            document.getElementById('login-password').value = '';
            const pendingInvite = sessionStorage.getItem('pendingProjectInvite');
            if (pendingInvite) {
                sessionStorage.removeItem('pendingProjectInvite');
                try {
                    await fetch(`/api/invites/${pendingInvite}/accept`, { method: 'POST', headers: { 'Content-Type': 'application/json' } });
                } catch { /* Bei Fehler einfach normal weiter */ }
            }
            await init();
        } else if (res.status === 429) {
            errEl.textContent = 'Zu viele Versuche. Bitte warte eine Minute.';
            errEl.style.display = 'block';
        } else {
            errEl.textContent = 'Falscher Benutzername oder Passwort.';
            errEl.style.display = 'block';
        }
    } catch {
        errEl.textContent = 'Verbindungsfehler.';
        errEl.style.display = 'block';
    }
}

async function doLogout() {
    await fetch('/api/auth/logout', { method: 'POST' });
    showLoginScreen();
}

// ── INIT ──
async function init() {
    try {
        if (!(await checkAuth())) return;

        let raw;
        try {
            raw = await api('/api/projects');
        } catch {
            raw = [];
        }
        _projects = Array.isArray(raw) ? raw : [];

        // Immer Projekt-Selektor zeigen (auch bei einem Projekt)
        // Setup-Screen nur noch intern für Erst-Erstellung via Platform-Admin
        showProjectScreen();
    } catch (e) {
        console.error('init() Fehler:', e);
        showLoginScreen();
    }
}

async function loadProject() {
    try {
    document.getElementById('project-screen').style.display = 'none';
    document.getElementById('setup-screen').style.display = 'none';
    document.getElementById('app').style.display = 'flex';

    const switchBtn = document.getElementById('switch-project-btn');
    if (switchBtn) switchBtn.style.display = '';

    const settings = await api(withProject('/api/settings'));
    applySettings(settings);

    await loadState();
    try {
        const [dataRes, finRes] = await Promise.all([
            fetch(withProject('/api/data')),
            fetch(withProject('/api/finance'))
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
    if (_currentUser) {
        const el = document.getElementById('sidebar-username');
        if (el) el.textContent = _currentUser.username;
        const avatar = document.getElementById('sidebar-user-avatar');
        if (avatar) avatar.textContent = _currentUser.username.charAt(0).toUpperCase();
    }
    document.getElementById('topbar-date').innerHTML =
        'Heute: ' + fmt(today) + '<br>Start: ' + fmt(START);

    renderKpis();
    renderRoadmap();
    renderFinanzen();
    updateAll();
    updateOverdueBanner();
    updateDashboardCards();
    loadDashboardAsync();

    const cwBody = document.getElementById('wb-' + currentWeek);
    const cwChev = document.getElementById('chev-' + currentWeek);
    if (cwBody) cwBody.classList.add('open');
    if (cwChev) cwChev.classList.add('open');
    } catch (e) {
        console.error('loadProject() Fehler:', e);
        document.getElementById('roadmap-content').innerHTML =
            '<p style="color:var(--red);padding:20px">Fehler beim Laden des Projekts. Bitte Seite neu laden.</p>';
    }
}
