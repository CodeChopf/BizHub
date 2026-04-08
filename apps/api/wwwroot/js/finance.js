// ── FINANZEN ──
let _finTab = 'expense'; // 'expense' | 'income'

function setFinTab(type) {
    _finTab = type;
    document.getElementById('fin-tab-expense').classList.toggle('active', type === 'expense');
    document.getElementById('fin-tab-income').classList.toggle('active', type === 'income');
    renderFinanzen();
}

function renderFinanzen() {
    if (!financeData) return;

    const strip = document.getElementById('fin-kpi-strip');
    const currency = getCurrency();
    const income   = financeData.totalIncome   ?? 0;
    const expenses = financeData.totalExpenses ?? 0;
    const balance  = financeData.netBalance    ?? (income - expenses);
    const balanceColor = balance >= 0 ? 'green' : 'red';
    const balanceSign  = balance >= 0 ? '+' : '−';

    strip.innerHTML = `
    <div class="kpi-card green">
      <div class="kpi-icon">📈</div>
      <div class="kpi-val">${currency} ${fmtChf(income)}</div>
      <div class="kpi-label">Einnahmen</div>
    </div>
    <div class="kpi-card red">
      <div class="kpi-icon">📉</div>
      <div class="kpi-val">${currency} ${fmtChf(expenses)}</div>
      <div class="kpi-label">Ausgaben</div>
    </div>
    <div class="kpi-card ${balanceColor}">
      <div class="kpi-icon">💰</div>
      <div class="kpi-val">${balanceSign}${currency} ${fmtChf(Math.abs(balance))}</div>
      <div class="kpi-label">Bilanz</div>
    </div>`;

    // Kategorie-Summary nach aktivem Tab
    const catSummary = document.getElementById('category-summary');
    const titleEl = document.getElementById('fin-cat-title');
    if (titleEl) titleEl.textContent = _finTab === 'income' ? 'Einnahmen nach Kategorie' : 'Ausgaben nach Kategorie';

    const tabEntries = (financeData.expenses ?? []).filter(e => (e.type ?? 'expense') === _finTab);
    const summaryData = tabEntries
        .reduce((acc, e) => {
            const key = e.categoryName;
            if (!acc[key]) acc[key] = { name: e.categoryName, color: e.categoryColor, total: 0, count: 0 };
            acc[key].total += e.amount;
            acc[key].count++;
            return acc;
        }, {});
    const summaryList = Object.values(summaryData).sort((a, b) => b.total - a.total);

    if (summaryList.length === 0) {
        catSummary.innerHTML = `<div class="empty-state">Noch keine ${_finTab === 'income' ? 'Einnahmen' : 'Ausgaben'} erfasst.</div>`;
    } else {
        const maxTotal = Math.max(...summaryList.map(s => s.total));
        catSummary.innerHTML = summaryList.map(s => `
      <div class="cat-row">
        <div class="cat-dot" style="background:${s.color}"></div>
        <div class="cat-name">${s.name}</div>
        <div class="cat-bar-wrap">
          <div class="cat-bar-fill" style="width:${Math.round((s.total / maxTotal) * 100)}%;background:${s.color}"></div>
        </div>
        <div class="cat-count">${s.count}×</div>
        <div class="cat-amount">${currency} ${fmtChf(s.total)}</div>
      </div>`).join('');
    }

    // Einträge-Liste nach aktivem Tab
    const expList = document.getElementById('expenses-list');
    if (tabEntries.length === 0) {
        expList.innerHTML = `<div class="empty-state">Noch keine ${_finTab === 'income' ? 'Einnahmen' : 'Ausgaben'}. Klicke auf "+ ${_finTab === 'income' ? 'Einnahme' : 'Ausgabe'}".</div>`;
    } else {
        expList.innerHTML = tabEntries.map(e => {
            const weekLabel = e.weekNumber ? `<span class="exp-week-badge">W0${e.weekNumber}</span>` : '';
            const linkHtml  = e.link ? `<a href="${e.link}" target="_blank" class="exp-link">🔗 Link öffnen</a>` : '';
            const isIncome  = (e.type ?? 'expense') === 'income';
            const amountStr = isIncome
                ? `+${currency} ${fmtChf(e.amount)}`
                : `−${currency} ${fmtChf(e.amount)}`;
            return `
        <div class="exp-row">
          <span class="exp-cat-badge" style="background:${e.categoryColor}22;color:${e.categoryColor}">${e.categoryName}</span>
          <div>
            <div class="exp-desc">${e.description}</div>
            <div class="exp-meta">${formatDateStr(e.date)} ${weekLabel} ${linkHtml}</div>
            <div id="attachments-${e.id}" class="exp-attachments"></div>
          </div>
          <div class="exp-amount${isIncome ? ' income' : ''}">${amountStr}</div>
          <button class="btn-icon" onclick="openEditExpenseModal(${e.id})" title="Bearbeiten">✏️</button>
          <button class="exp-delete" onclick="deleteExpense(${e.id})" title="Löschen">✕</button>
        </div>`;
        }).join('');

        tabEntries.forEach(async e => {
            const res = await fetch(withProject('/api/expenses/' + e.id + '/attachments'));
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
        const res = await fetch(withProject('/api/expenses/' + expenseId + '/attachments'));
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
let _expModalType = 'expense';

function setExpModalType(type) {
    _expModalType = type;
    const isIncome = type === 'income';
    document.getElementById('exp-type-btn-expense').classList.toggle('active', !isIncome);
    document.getElementById('exp-type-btn-income').classList.toggle('active', isIncome);
    const title = document.getElementById('expense-modal-title');
    if (title) title.textContent = isIncome ? 'Einnahme erfassen' : 'Ausgabe erfassen';

    const descLabel = document.getElementById('exp-description-label');
    if (descLabel) descLabel.textContent = isIncome ? 'Beschreibung' : 'Beschreibung';
    const descInput = document.getElementById('exp-description');
    if (descInput) descInput.placeholder = isIncome ? 'z.B. Etsy-Verkauf, Kursgebühren...' : 'z.B. Filament PLA 1kg';

    const linkLabel = document.getElementById('exp-link-label');
    if (linkLabel) linkLabel.textContent = isIncome ? 'Plattform (optional)' : 'Link (optional)';
    const linkInput = document.getElementById('exp-link');
    if (linkInput) {
        linkInput.type = isIncome ? 'text' : 'url';
        linkInput.placeholder = isIncome ? 'z.B. Etsy, Shopify, Amazon...' : 'https://...';
    }
    const attachGroup = document.getElementById('exp-attachment-group');
    if (attachGroup) attachGroup.style.display = isIncome ? 'none' : '';
}

function openExpenseModal(type) {
    _expModalType = type ?? _finTab ?? 'expense';

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
    setExpModalType(_expModalType);
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
    const type = _expModalType;

    if (!description || !amount || amount <= 0) { showToast('Bitte Beschreibung und Betrag eingeben.'); return; }

    try {
        const expense = await api(withProject('/api/expenses'), 'POST', { categoryId, amount, description, link, date, weekNumber, taskId, type });

        const fileInput = document.getElementById('exp-file');
        if (fileInput.files.length > 0) {
            const file = fileInput.files[0];
            const base64 = await fileToBase64(file);
            await api(withProject(`/api/expenses/${expense.id}/attachments`), 'POST', { fileName: file.name, mimeType: file.type, data: base64 });
        }

        financeData = await api(withProject('/api/finance'));
        _finTab = type; // Tab auf gespeicherten Typ wechseln
        setFinTab(type);
        closeExpenseModal();
        showToast(type === 'income' ? '✓ Einnahme gespeichert' : '✓ Ausgabe gespeichert');
        document.getElementById('exp-amount').value = '';
        document.getElementById('exp-description').value = '';
        document.getElementById('exp-link').value = '';
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteExpense(id) {
    if (!confirm('Eintrag löschen?')) return;
    try {
        await api(withProject('/api/expenses/' + id), 'DELETE');
        financeData = await api(withProject('/api/finance'));
        renderFinanzen();
        showToast('✓ Eintrag gelöscht');
    } catch { showToast('Fehler beim Löschen.'); }
}

// ── EDIT EXPENSE MODAL ──
function setEditExpModalType(type) {
    document.getElementById('edit-exp-type').value = type;
    const isIncome = type === 'income';
    document.getElementById('edit-exp-type-btn-expense').classList.toggle('active', !isIncome);
    document.getElementById('edit-exp-type-btn-income').classList.toggle('active', isIncome);
    const title = document.getElementById('edit-expense-modal-title');
    if (title) title.textContent = isIncome ? 'Einnahme bearbeiten' : 'Ausgabe bearbeiten';

    const descInput = document.getElementById('edit-exp-description');
    if (descInput) descInput.placeholder = isIncome ? 'z.B. Etsy-Verkauf, Kursgebühren...' : 'z.B. Filament PLA 1kg';

    const linkLabel = document.getElementById('edit-exp-link-label');
    if (linkLabel) linkLabel.textContent = isIncome ? 'Plattform (optional)' : 'Link (optional)';
    const linkInput = document.getElementById('edit-exp-link');
    if (linkInput) {
        linkInput.type = isIncome ? 'text' : 'url';
        linkInput.placeholder = isIncome ? 'z.B. Etsy, Shopify, Amazon...' : 'https://...';
    }
    const attachGroup = document.getElementById('edit-exp-attachment-group');
    if (attachGroup) attachGroup.style.display = isIncome ? 'none' : '';
}

function openEditExpenseModal(id) {
    const expense = financeData.expenses.find(e => e.id === id);
    if (!expense) return;

    document.getElementById('edit-exp-id').value = expense.id;
    setEditExpModalType(expense.type ?? 'expense');
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
    const type = document.getElementById('edit-exp-type').value || 'expense';
    const categoryId = parseInt(document.getElementById('edit-exp-category').value);
    const amount = parseFloat(document.getElementById('edit-exp-amount').value);
    const description = document.getElementById('edit-exp-description').value.trim();
    const link = document.getElementById('edit-exp-link').value.trim() || null;
    const date = document.getElementById('edit-exp-date').value;
    const weekNumber = document.getElementById('edit-exp-week').value ? parseInt(document.getElementById('edit-exp-week').value) : null;
    const taskId = document.getElementById('edit-exp-task').value ? parseInt(document.getElementById('edit-exp-task').value) : null;

    if (!description || !amount || amount <= 0) { showToast('Bitte Beschreibung und Betrag eingeben.'); return; }

    try {
        await api(withProject('/api/expenses/' + id), 'PUT', { categoryId, amount, description, link, date, weekNumber, taskId, type });

        const fileInput = document.getElementById('edit-exp-file');
        if (fileInput.files.length > 0) {
            const file = fileInput.files[0];
            const base64 = await fileToBase64(file);
            await api(withProject(`/api/expenses/${id}/attachments`), 'POST', { fileName: file.name, mimeType: file.type, data: base64 });
        }

        financeData = await api(withProject('/api/finance'));
        setFinTab(type);
        closeEditExpenseModal();
        showToast('✓ Eintrag aktualisiert');
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
    await api(withProject('/api/categories/' + id), 'PUT', { name: name.trim(), color });
    financeData = await api(withProject('/api/finance'));
    renderFinanzen();
}

async function updateCategoryColor(id, color) {
    const cat = financeData.categories.find(c => c.id === id);
    if (!cat) return;
    await api(withProject('/api/categories/' + id), 'PUT', { name: cat.name, color });
    financeData = await api(withProject('/api/finance'));
    renderFinanzen();
}

async function addCategory() {
    const name = document.getElementById('new-cat-name').value.trim();
    const color = document.getElementById('new-cat-color').value;
    if (!name) { showToast('Bitte einen Namen eingeben.'); return; }
    await api(withProject('/api/categories'), 'POST', { name, color });
    financeData = await api(withProject('/api/finance'));
    renderCategoryList();
    renderFinanzen();
    document.getElementById('new-cat-name').value = '';
    showToast('✓ Kategorie hinzugefügt');
}

async function deleteCategory(id) {
    if (!confirm('Kategorie löschen?')) return;
    await api(withProject('/api/categories/' + id), 'DELETE');
    financeData = await api(withProject('/api/finance'));
    renderCategoryList();
    renderFinanzen();
    showToast('✓ Kategorie gelöscht');
}

function closeModals(event) {
    if (event.target.classList.contains('modal-backdrop')) {
        document.querySelectorAll('.modal-backdrop').forEach(m => m.classList.remove('open'));
    }
}
