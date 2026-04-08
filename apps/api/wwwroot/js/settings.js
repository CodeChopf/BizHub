// ── APP-VERSION ──
async function loadAppVersion() {
    try {
        const res = await fetch('/api/version');
        if (res.ok) {
            const { version } = await res.json();
            const el = document.getElementById('app-version');
            if (el) el.textContent = version;
        }
    } catch {}
}

// ── EINSTELLUNGEN ──
async function saveSettings() {
    const name = document.getElementById('settings-name').value.trim();
    const start = document.getElementById('settings-start').value;
    const desc = document.getElementById('settings-desc').value.trim();
    const currency = document.getElementById('settings-currency').value;

    if (!name || !start) { showToast('Name und Startdatum sind Pflicht.'); return; }

    const settings = await api(withProject('/api/settings'), 'POST', {
        projectName: name,
        startDate: start,
        description: desc,
        currency,
        projectImage: projectSettings?.projectImage ?? null,
        visibleTabs: projectSettings?.visibleTabs ?? null
    });
    applySettings(settings);
    if (_currentProject) { _currentProject.name = name; _currentProject.description = desc; }
    const _pi = _projects.findIndex(p => p.id === _currentProjectId);
    if (_pi !== -1) { _projects[_pi].name = name; _projects[_pi].description = desc; }
    showToast('✓ Einstellungen gespeichert');
}

// ── TAB VISIBILITY ──
const TOGGLEABLE_TABS = ['roadmap', 'produkte', 'finanzen', 'meilensteine', 'admin', 'produktion', 'kalender'];

function applyTabVisibility(visibleTabsJson) {
    const tabs = visibleTabsJson ? JSON.parse(visibleTabsJson) : {};
    TOGGLEABLE_TABS.forEach(id => {
        const visible = tabs[id] !== false;
        const navEl = document.getElementById('nav-' + id);
        if (navEl) navEl.style.display = visible ? '' : 'none';
        // Mirror visibility on dashboard card
        const dashCard = document.getElementById('dash-card-' + id);
        if (dashCard) dashCard.style.display = visible ? '' : 'none';
        // Update toggle checkbox in settings
        const toggle = document.getElementById('tab-toggle-' + id);
        if (toggle) toggle.checked = visible;
    });
}

async function saveTabVisibility() {
    const tabs = {};
    TOGGLEABLE_TABS.forEach(id => {
        const toggle = document.getElementById('tab-toggle-' + id);
        tabs[id] = toggle ? toggle.checked : true;
    });
    const visibleTabs = JSON.stringify(tabs);
    if (projectSettings) projectSettings.visibleTabs = visibleTabs;
    applyTabVisibility(visibleTabs);

    // Wenn aktive Page jetzt ausgeblendet ist → Übersicht
    const activePage = document.querySelector('.page.active');
    if (activePage) {
        const pageId = activePage.id.replace('page-', '');
        if (TOGGLEABLE_TABS.includes(pageId) && tabs[pageId] === false) {
            showPage('overview');
        }
    }

    // Persist
    const name = document.getElementById('settings-name')?.value.trim() || projectSettings?.projectName || '';
    const start = document.getElementById('settings-start')?.value || projectSettings?.startDate || '';
    const desc = document.getElementById('settings-desc')?.value.trim() || projectSettings?.description || '';
    const currency = document.getElementById('settings-currency')?.value || projectSettings?.currency || 'CHF';
    const updated = await api(withProject('/api/settings'), 'POST', {
        projectName: name, startDate: start, description: desc, currency,
        projectImage: projectSettings?.projectImage ?? null,
        visibleTabs
    });
    applySettings(updated);
}

// ── EXPORT / IMPORT ──
function exportData() {
    if (!confirm('Projektdaten jetzt exportieren?')) return;
    const a = document.createElement('a');
    a.href = withProject('/api/export');
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
        const res = await fetch(withProject('/api/import'), {
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
