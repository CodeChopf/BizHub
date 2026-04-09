// ── PROJEKTVERWALTUNG ──
async function showProjectScreen() {
    document.getElementById('app').style.display = 'none';
    document.getElementById('setup-screen').style.display = 'none';
    document.getElementById('project-screen').style.display = 'flex';
    try {
        const raw = await api('/api/projects');
        _projects = Array.isArray(raw) ? raw : [];
    } catch {}
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
                <div class="project-select-meta">${escHtml(p.description ?? '')}${String(p.role ?? '').toLowerCase() === 'admin' ? '<span style="color:var(--accent);font-size:.72rem;margin-left:6px">Admin</span>' : ''}</div>
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

let _managedUsers = [];

function openUserManagementModal() {
    document.getElementById('um-create-username').value = '';
    document.getElementById('um-create-password').value = '';
    document.getElementById('um-create-admin').checked = false;
    document.getElementById('um-reset-username').value = '';
    document.getElementById('um-reset-password').value = '';
    document.getElementById('user-management-modal').style.display = 'flex';
    loadManagedUsers();
}

function closeUserManagementModal() {
    document.getElementById('user-management-modal').style.display = 'none';
}

async function loadManagedUsers() {
    const listEl = document.getElementById('um-users-list');
    const resetSelect = document.getElementById('um-reset-username');
    if (!listEl || !resetSelect) return;
    listEl.innerHTML = '<div class="empty-state" style="padding:18px 12px">Lädt...</div>';

    try {
        const users = await api('/api/users');
        _managedUsers = Array.isArray(users) ? users : [];
    } catch (err) {
        listEl.innerHTML = `<div class="user-mgmt-error">${escHtml(err?.message || 'Fehler beim Laden der Benutzer.')}</div>`;
        return;
    }

    resetSelect.innerHTML = '<option value="">— Benutzer wählen —</option>' + _managedUsers
        .map(u => `<option value="${escHtml(u.username)}">${escHtml(u.username)}</option>`)
        .join('');

    if (_managedUsers.length === 0) {
        listEl.innerHTML = '<div class="empty-state" style="padding:18px 12px">Keine Benutzer gefunden.</div>';
        return;
    }

    listEl.innerHTML = _managedUsers.map(u => {
        const isAdmin = !!u.isAdmin;
        const isPlatformAdmin = !!u.isPlatformAdmin;
        const createdAt = escHtml(u.createdAt ?? '');
        return `
            <div class="user-mgmt-row">
                <div>
                    <div class="user-mgmt-name">${escHtml(u.username)}</div>
                    <div class="user-mgmt-meta">
                        ${isAdmin ? '<span class="user-badge">Admin</span>' : ''}
                        ${isPlatformAdmin ? '<span class="user-badge">Platform</span>' : ''}
                        <span>Erstellt: ${createdAt}</span>
                    </div>
                </div>
                <div class="user-mgmt-actions">
                    <button class="btn-ghost btn-sm btn-danger" onclick="deleteManagedUser('${escHtml(u.username).replace(/'/g, "\\'")}')">Löschen</button>
                </div>
            </div>`;
    }).join('');
}

async function createManagedUser() {
    const username = document.getElementById('um-create-username').value.trim();
    const password = document.getElementById('um-create-password').value;
    const isAdmin = !!document.getElementById('um-create-admin').checked;

    if (!username || !password) {
        showToast('Benutzername und Passwort sind Pflicht.');
        return;
    }

    try {
        await api('/api/users', 'POST', { username, password, isAdmin });
        showToast('Benutzer erstellt.');
        document.getElementById('um-create-username').value = '';
        document.getElementById('um-create-password').value = '';
        document.getElementById('um-create-admin').checked = false;
        await loadManagedUsers();
    } catch (err) {
        showToast(err?.message || 'Fehler beim Erstellen.');
    }
}

async function deleteManagedUser(username) {
    if (!username) return;
    if (!confirm(`Benutzer "${username}" wirklich löschen?`)) return;

    try {
        await api('/api/users/' + encodeURIComponent(username), 'DELETE');
        showToast('Benutzer gelöscht.');
        await loadManagedUsers();
    } catch (err) {
        showToast(err?.message || 'Fehler beim Löschen.');
    }
}

async function resetManagedUserPassword() {
    const username = document.getElementById('um-reset-username').value;
    const password = document.getElementById('um-reset-password').value;
    if (!username || !password) {
        showToast('Benutzer und neues Passwort angeben.');
        return;
    }

    try {
        await api('/api/users/' + encodeURIComponent(username) + '/password', 'PUT', { password });
        showToast('Passwort aktualisiert.');
        document.getElementById('um-reset-password').value = '';
    } catch (err) {
        showToast(err?.message || 'Fehler beim Aktualisieren.');
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

async function checkInviteParam() {
    const params = new URLSearchParams(window.location.search);
    const token = params.get('invite');
    if (!token) return;
    try {
        const res = await fetch(`/api/invites/${token}`);
        const info = await res.json();
        if (!res.ok) { return; } // abgelaufen / ungültig — still login screen
        if (info.type === 'platform') {
            // Registrierung für neuen User — Login-Felder ausblenden
            const loginForm = document.getElementById('login-form-section');
            if (loginForm) loginForm.style.display = 'none';
            const regSection = document.getElementById('register-section');
            if (regSection) regSection.style.display = 'block';
            const regToken = document.getElementById('_reg_token');
            if (regToken) regToken.value = token;
        } else if (info.type === 'project') {
            // Projekt-Einladung: Token speichern, Hinweis zeigen
            sessionStorage.setItem('pendingProjectInvite', token);
            const notice = document.getElementById('project-invite-notice');
            if (notice) {
                notice.textContent = `Du wurdest eingeladen dem Projekt „${info.projectName}" beizutreten. Bitte melde dich an.`;
                notice.style.display = 'block';
            }
        }
    } catch { /* Netzwerkfehler — einfach Login zeigen */ }
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
    const isProjectAdmin = (typeof isCurrentProjectAdmin === 'function')
        ? isCurrentProjectAdmin()
        : String(_currentProject?.role ?? '').toLowerCase() === 'admin';
    const card = document.getElementById('members-card');
    const dangerCard = document.getElementById('danger-zone-card');
    if (!card) return;
    if (!isProjectAdmin) {
        card.style.display = 'none';
        if (dangerCard) dangerCard.style.display = 'none';
        return;
    }
    card.style.display = '';
    if (dangerCard) dangerCard.style.display = '';
    const list = document.getElementById('members-list');
    if (!list) return;

    try {
        const members = await api(`/api/projects/${_currentProjectId}/members`);
        if (!Array.isArray(members) || members.length === 0) {
            list.innerHTML = '<div style="font-size:13px;color:var(--text3)">Keine Mitglieder gefunden.</div>';
            return;
        }

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
    } catch (err) {
        list.innerHTML = '<div style="font-size:13px;color:var(--red)">Mitglieder konnten nicht geladen werden.</div>';
        showToast(err?.message || 'Fehler beim Laden der Mitglieder.');
    }
}
function openProjectInviteModal() {
    document.getElementById('project-invite-hours').value = '48';
    document.getElementById('project-invite-role').value = 'member';
    document.getElementById('project-invite-link-wrap').style.display = 'none';
    document.getElementById('project-invite-modal').style.display = 'flex';
}
function closeProjectInviteModal() {
    document.getElementById('project-invite-modal').style.display = 'none';
}
async function generateProjectInvite() {
    const hours = parseInt(document.getElementById('project-invite-hours').value) || 48;
    const role = document.getElementById('project-invite-role').value;
    try {
        const invite = await api(`/api/projects/${_currentProjectId}/invites`, 'POST', { hoursValid: hours, role });
        const link = `${location.origin}/?invite=${invite.token}`;
        document.getElementById('project-invite-link-input').value = link;
        document.getElementById('project-invite-link-wrap').style.display = '';
    } catch { showToast('Fehler beim Generieren des Links.'); }
}
function copyProjectInviteLink() {
    const input = document.getElementById('project-invite-link-input');
    if (!input) return;
    navigator.clipboard.writeText(input.value)
        .then(() => showToast('Link kopiert!'))
        .catch(() => { input.select(); document.execCommand('copy'); showToast('Link kopiert!'); });
}
async function removeMember(userId) {
    if (!confirm('Mitglied wirklich entfernen?')) return;
    const res = await fetch(`/api/projects/${_currentProjectId}/members/${userId}`, { method: 'DELETE' });
    if (res.ok) { renderMemberList(); showToast('Mitglied entfernt.'); }
    else {
        try {
            const err = await res.json();
            showToast(err.error ?? 'Fehler.');
        } catch {
            showToast('Fehler.');
        }
    }
}

async function deleteProject() {
    if (!confirm(`Projekt "${_currentProject?.name}" wirklich löschen? Alle Daten gehen verloren.`)) return;
    const res = await fetch(`/api/projects/${_currentProjectId}`, { method: 'DELETE' });
    if (res.ok) {
        _projects = _projects.filter(p => p.id !== _currentProjectId);
        showToast('Projekt gelöscht.');
        showProjectScreen();
    } else {
        try { const err = await res.json(); showToast(err.error ?? 'Fehler.'); } catch { showToast('Fehler.'); }
    }
}
