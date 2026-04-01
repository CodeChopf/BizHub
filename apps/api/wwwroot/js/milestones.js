// ── MEILENSTEINE ──
let milestoneData = [];

async function renderMilestones() {
    milestoneData = await api(withProject('/api/milestones'));
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
        await api(withProject('/api/milestones'), 'POST', { name, description, snapshot: JSON.stringify(snapshot) });
        closeMilestoneModal();
        renderMilestones();
        showToast('✓ Meilenstein gespeichert');
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteMilestone(id) {
    if (!confirm('Meilenstein löschen?')) return;
    try {
        await api(withProject('/api/milestones/' + id), 'DELETE');
        renderMilestones();
        showToast('✓ Meilenstein gelöscht');
    } catch { showToast('Fehler beim Löschen.'); }
}

async function openMilestoneDetail(id) {
    const milestone = await api(withProject('/api/milestones/' + id));
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
