// ── PROJEKT SETTINGS ──
let projectSettings = null;
let START = new Date();
let _currentProjectId = 1;
let _projects = [];
let _currentProject = null;
let _importData = null;

function getStart() { return START; }

function applySettings(settings) {
    projectSettings = settings;
    START = new Date(settings.startDate);

    // Sidebar Name
    const nameEl = document.getElementById('sidebar-project-name');
    const iconEl = document.getElementById('sidebar-logo-icon');
    if (nameEl) nameEl.textContent = settings.projectName;

    // Sidebar Icon — Bild oder Initialen
    if (iconEl) {
        if (settings.projectImage) {
            iconEl.style.padding = '0';
            iconEl.style.overflow = 'hidden';
            iconEl.innerHTML = `<img src="data:image/jpeg;base64,${settings.projectImage}"
                style="width:100%;height:100%;object-fit:cover;border-radius:10px">`;
        } else {
            iconEl.style.padding = '';
            iconEl.style.overflow = '';
            iconEl.textContent = settings.projectName.substring(0, 2).toUpperCase();
        }
    }

    // Overview Header
    const titleEl = document.getElementById('overview-title');
    if (titleEl) titleEl.textContent = settings.projectName;

    // Overview Projektbild
    const imgWrap = document.getElementById('overview-project-image');
    if (imgWrap) {
        imgWrap.innerHTML = settings.projectImage
            ? `<img src="data:image/jpeg;base64,${settings.projectImage}"
                style="width:100%;max-height:180px;object-fit:cover;border-radius:var(--radius);
                border:1px solid var(--border)">`
            : '';
    }

    // Einstellungen Felder
    const sName = document.getElementById('settings-name');
    const sStart = document.getElementById('settings-start');
    const sDesc = document.getElementById('settings-desc');
    const sCurr = document.getElementById('settings-currency');
    if (sName) sName.value = settings.projectName;
    if (sStart) sStart.value = settings.startDate;
    if (sDesc) sDesc.value = settings.description ?? '';
    if (sCurr) sCurr.value = settings.currency ?? 'CHF';

    // Bild-Vorschau in Einstellungen
    renderSettingsImagePreview(settings.projectImage ?? null);

    document.title = settings.projectName + ' — BizHub';

    // Tab-Sichtbarkeit
    applyTabVisibility(settings.visibleTabs ?? null);
}

function renderSettingsImagePreview(base64) {
    const preview = document.getElementById('project-image-preview');
    const removeBtn = document.getElementById('btn-remove-image');
    if (!preview) return;
    if (base64) {
        preview.innerHTML = `<img src="data:image/jpeg;base64,${base64}"
            style="width:100%;max-height:120px;object-fit:cover;border-radius:8px;
            border:1px solid var(--border2)">`;
        if (removeBtn) removeBtn.style.display = 'block';
    } else {
        preview.innerHTML = '';
        if (removeBtn) removeBtn.style.display = 'none';
    }
}

function handleProjectImagePreview() {
    const file = document.getElementById('settings-image').files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = e => {
        const base64 = e.target.result.split(',')[1];
        if (!projectSettings) projectSettings = {};
        projectSettings.projectImage = base64;
        renderSettingsImagePreview(base64);
    };
    reader.readAsDataURL(file);
}

function removeProjectImage() {
    if (projectSettings) projectSettings.projectImage = null;
    document.getElementById('settings-image').value = '';
    renderSettingsImagePreview(null);
}

function getCurrency() {
    return projectSettings?.currency ?? 'CHF';
}
