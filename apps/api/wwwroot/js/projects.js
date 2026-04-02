// ── PROJEKTVERWALTUNG ──
function showProjectScreen() {
    document.getElementById('app').style.display = 'none';
    document.getElementById('setup-screen').style.display = 'none';
    document.getElementById('project-screen').style.display = 'flex';
    renderProjectScreen();
}

function renderProjectScreen() {
    // Begrüssung
    const welcome = document.getElementById('project-screen-welcome');
    if (welcome) welcome.textContent = _currentUser ? `Hallo ${_currentUser.username} — wähle ein Projekt oder erstelle ein neues.` : '';

    // Platform-Admin Button
    const adminWrap = document.getElementById('platform-invite-btn-wrap');
    if (adminWrap) adminWrap.style.display = _currentUser?.isAdmin ? '' : 'none';

    const container = document.getElementById('project-list-screen');
    if (!container) return;
    if (_projects.length === 0) {
        container.innerHTML = '<p style="color:var(--text3);font-size:13px;margin:0">Du bist noch in keinem Projekt.</p>';
        return;
    }
    container.innerHTML = _projects.map(p => `
        <div class="project-select-card">
            <div style="flex:1;cursor:pointer;min-width:0" onclick="selectProject(${p.id})">
                <div class="project-select-name">${escHtml(p.name)}</div>
                <div class="project-select-meta">${escHtml(p.description ?? '')}${p.role === 'admin' ? '<span style="color:var(--accent);font-size:.72rem;margin-left:6px">Admin</span>' : ''}</div>
            </div>
            <button class="btn-ghost btn-sm" onclick="selectProject(${p.id})">Öffnen</button>
            <button class="btn-ghost btn-sm btn-danger" onclick="leaveProject(${p.id}, '${escHtml(p.name).replace(/'/g, "\\'")}')">Austreten</button>
        </div>`).join('');
}

async function leaveProject(id, name) {
    if (!confirm(`Aus Projekt "${name}" austreten?`)) return;
    const res = await fetch(`/api/projects/${id}/leave`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' }
    });
    if (res.ok) {
        _projects = _projects.filter(p => p.id !== id);
        renderProjectScreen();
        showToast('Aus Projekt ausgetreten.');
    } else {
        try { const err = await res.json(); showToast(err.error ?? 'Fehler.'); } catch { showToast('Fehler.'); }
    }
}

function openPlatformInviteModal() {
    const hoursInput = document.getElementById('platform-invite-hours');
    if (hoursInput) hoursInput.value = '48';
    const linkWrap = document.getElementById('platform-invite-link-wrap');
    if (linkWrap) linkWrap.style.display = 'none';
    const modal = document.getElementById('platform-invite-modal');
    if (modal) modal.style.display = 'flex';
}
function closePlatformInviteModal() {
    const modal = document.getElementById('platform-invite-modal');
    if (modal) modal.style.display = 'none';
}
async function generatePlatformInvite() {
    const hoursInput = document.getElementById('platform-invite-hours');
    const hours = parseInt(hoursInput?.value) || 48;
    try {
        const invite = await api('/api/platform/invites', 'POST', { hoursValid: hours });
        const link = `${location.origin}/?invite=${invite.token}`;
        const linkInput = document.getElementById('platform-invite-link-input');
        if (linkInput) linkInput.value = link;
        const linkWrap = document.getElementById('platform-invite-link-wrap');
        if (linkWrap) linkWrap.style.display = '';
    } catch (e) {
        showToast('Fehler beim Generieren des Links.');
    }
}
function copyPlatformInviteLink() {
    const input = document.getElementById('platform-invite-link-input');
    if (!input) return;
    input.select();
    navigator.clipboard.writeText(input.value).then(() => showToast('Link kopiert!')).catch(() => {
        document.execCommand('copy');
        showToast('Link kopiert!');
    });
}

function checkInviteParam() {
    const params = new URLSearchParams(window.location.search);
    const token = params.get('invite');
    if (token) {
        const regSection = document.getElementById('register-section');
        if (regSection) regSection.style.display = 'block';
        const regToken = document.getElementById('_reg_token');
        if (regToken) regToken.value = token;
    }
}

async function doRegister() {
    const token    = document.getElementById('_reg_token')?.value ?? '';
    const username = document.getElementById('reg-username')?.value.trim() ?? '';
    const password = document.getElementById('reg-password')?.value ?? '';
    const errEl    = document.getElementById('reg-error');
    if (errEl) errEl.style.display = 'none';
    if (!username || !password) {
        if (errEl) { errEl.textContent = 'Benutzername und Passwort sind Pflicht.'; errEl.style.display = 'block'; }
        return;
    }
    const res = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, username, password })
    });
    if (res.ok) {
        const regSection = document.getElementById('register-section');
        if (regSection) regSection.style.display = 'none';
        const loginUser = document.getElementById('login-username');
        if (loginUser) loginUser.value = username;
        showToast('Account erstellt! Bitte einloggen.');
        window.history.replaceState({}, '', window.location.pathname);
    } else {
        try {
            const err = await res.json();
            if (errEl) { errEl.textContent = err.error ?? 'Fehler beim Erstellen.'; errEl.style.display = 'block'; }
        } catch {
            if (errEl) { errEl.textContent = 'Fehler beim Erstellen.'; errEl.style.display = 'block'; }
        }
    }
}

async function selectProject(id) {
    _currentProjectId = id;
    _currentProject = _projects.find(p => p.id === id) ?? null;
    await loadProject();
}

function openCreateProjectModal() {
    document.getElementById('cp-name').value = '';
    document.getElementById('cp-start').value = today.toISOString().split('T')[0];
    document.getElementById('cp-desc').value = '';
    document.getElementById('cp-currency').value = 'CHF';
    document.getElementById('create-project-modal').style.display = 'flex';
    setTimeout(() => document.getElementById('cp-name').focus(), 50);
}
function closeCreateProjectModal() {
    document.getElementById('create-project-modal').style.display = 'none';
}
async function confirmCreateProject() {
    const name = document.getElementById('cp-name').value.trim();
    const start = document.getElementById('cp-start').value;
    const desc = document.getElementById('cp-desc').value.trim();
    const currency = document.getElementById('cp-currency').value;
    if (!name || !start) { showToast('Name und Datum sind Pflicht.'); return; }
    const proj = await api('/api/projects', 'POST', { name, description: desc, startDate: start, currency });
    closeCreateProjectModal();
    _projects = await api('/api/projects');
    _currentProjectId = proj.id;
    _currentProject = _projects.find(p => p.id === proj.id) ?? proj;
    await loadProject();
}

// ── MITGLIEDERVERWALTUNG ──
async function renderMemberList() {
    const isProjectAdmin = _currentProject?.role === 'admin' || _currentUser?.isAdmin;
    const card = document.getElementById('members-card');
    if (!card) return;
    if (!isProjectAdmin) { card.style.display = 'none'; return; }
    card.style.display = '';
    const members = await api(`/api/projects/${_currentProjectId}/members`);
    const list = document.getElementById('members-list');
    list.innerHTML = members.map(m => `
        <div class="user-row">
            <div class="user-info">
                <span class="user-name">${escHtml(m.username)}</span>
                ${m.role === 'admin' ? '<span class="user-badge">Admin</span>' : ''}
            </div>
            <div class="user-actions">
                <button class="btn-ghost btn-sm btn-danger" onclick="removeMember(${m.userId})"
                    ${m.username === _currentUser?.username ? 'disabled title="Eigenen Account nicht entfernbar"' : ''}>Entfernen</button>
            </div>
        </div>`).join('');
}
function openAddMemberModal() {
    document.getElementById('new-member-username').value = '';
    document.getElementById('new-member-role').value = 'member';
    document.getElementById('add-member-modal').style.display = 'flex';
    setTimeout(() => document.getElementById('new-member-username').focus(), 50);
}
function closeAddMemberModal() {
    document.getElementById('add-member-modal').style.display = 'none';
}
async function confirmAddMember() {
    const username = document.getElementById('new-member-username').value.trim();
    const role = document.getElementById('new-member-role').value;
    if (!username) { showToast('Benutzername ist Pflicht.'); return; }
    const res = await fetch(`/api/projects/${_currentProjectId}/members`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, role })
    });
    if (res.ok) { closeAddMemberModal(); renderMemberList(); showToast('Mitglied hinzugefügt.'); }
    else { const err = await res.json(); showToast(err.error ?? 'Fehler.'); }
}
async function removeMember(userId) {
    if (!confirm('Mitglied wirklich entfernen?')) return;
    const res = await fetch(`/api/projects/${_currentProjectId}/members/${userId}`, { method: 'DELETE' });
    if (res.ok) { renderMemberList(); showToast('Mitglied entfernt.'); }
    else { const err = await res.json(); showToast(err.error ?? 'Fehler.'); }
}
