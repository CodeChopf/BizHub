// ── PRODUKTION ──
let prodFilter = 'all';
let productionData = [];

async function renderProduktion() {
    productionData = await api(withProject('/api/production'));
    renderProdList();
}

function setProdFilter(f) {
    prodFilter = f;
    ['all', 'open', 'done'].forEach(k => {
        document.getElementById('prod-filter-' + k)?.classList.toggle('active', k === f);
    });
    renderProdList();
}

function renderProdList() {
    const list = document.getElementById('prod-list');
    if (!list) return;

    const items = productionData.filter(i => {
        if (prodFilter === 'open') return !i.done;
        if (prodFilter === 'done') return i.done;
        return true;
    });

    const hasDone = productionData.some(i => i.done);
    const deleteBtn = document.getElementById('btn-delete-done-prod');
    if (deleteBtn) deleteBtn.style.display = hasDone ? '' : 'none';

    if (items.length === 0) {
        list.innerHTML = `<div class="prod-empty">
            ${prodFilter === 'done' ? 'Keine erledigten Artikel.' : prodFilter === 'open' ? 'Keine offenen Artikel. 🎉' : 'Keine Artikel in der Warteschlange.<br><small>Füge Artikel aus dem Produktkatalog hinzu.</small>'}
        </div>`;
        return;
    }

    list.innerHTML = items.map(item => {
        const dot = `<span class="cat-dot" style="background:${item.categoryColor}"></span>`;
        const variation = item.variationName
            ? `<span class="prod-item-variation">${item.variationName}${item.variationSku ? ' · ' + item.variationSku : ''}</span>`
            : '';
        const note = item.note ? `<div class="prod-item-note">${escHtml(item.note)}</div>` : '';
        return `
        <div class="prod-item-row ${item.done ? 'done' : ''}" id="prod-row-${item.id}">
            <label class="prod-item-check">
                <input type="checkbox" ${item.done ? 'checked' : ''} onchange="toggleProdDone(${item.id}, this.checked)">
                <span class="check-box"></span>
            </label>
            <div class="prod-item-info">
                <div class="prod-item-name">${dot}${escHtml(item.productName)}</div>
                ${variation}
                ${note}
                <div class="prod-item-cat">${escHtml(item.categoryName)}</div>
            </div>
            <div class="prod-item-qty">
                <button class="qty-btn" onclick="changeProdQty(${item.id}, ${item.quantity - 1})">−</button>
                <span class="qty-val">${item.quantity}</span>
                <button class="qty-btn" onclick="changeProdQty(${item.id}, ${item.quantity + 1})">+</button>
            </div>
            <button class="btn-icon" onclick="deleteProductionItem(${item.id})" title="Entfernen">🗑</button>
        </div>`;
    }).join('');
}

async function toggleProdDone(id, done) {
    await api(withProject(`/api/production/${id}/done`), 'PATCH', { done });
    const item = productionData.find(i => i.id === id);
    if (item) item.done = done;
    renderProdList();
}

async function changeProdQty(id, qty) {
    if (qty < 1) return;
    const item = productionData.find(i => i.id === id);
    if (!item) return;
    await api(withProject(`/api/production/${id}`), 'PUT', { quantity: qty, note: item.note ?? null });
    item.quantity = qty;
    renderProdList();
}

async function deleteProductionItem(id) {
    if (!confirm('Artikel aus der Produktionswarteschlange entfernen?')) return;
    await api(withProject(`/api/production/${id}`), 'DELETE');
    productionData = productionData.filter(i => i.id !== id);
    renderProdList();
    showToast('✓ Artikel entfernt');
}

async function deleteAllDoneProduction() {
    if (!confirm('Alle erledigten Artikel löschen?')) return;
    await api(withProject('/api/production/done'), 'DELETE');
    productionData = productionData.filter(i => !i.done);
    renderProdList();
    showToast('✓ Erledigte Artikel gelöscht');
}

async function openProductionAddModal() {
    if (!catalogData) catalogData = await api(withProject('/api/catalog'));
    const sel = document.getElementById('prod-add-product');
    sel.innerHTML = '<option value="">— Produkt wählen —</option>' +
        catalogData.products.map(p =>
            `<option value="${p.id}" data-variations='${JSON.stringify(p.variations)}'>${escHtml(p.name)}</option>`
        ).join('');
    document.getElementById('prod-add-qty').value = '1';
    document.getElementById('prod-add-note').value = '';
    document.getElementById('prod-add-variation-group').style.display = 'none';
    document.getElementById('prod-add-variation').innerHTML = '<option value="">— Variante wählen (optional) —</option>';
    document.getElementById('production-add-modal').classList.add('open');
}

function onProdProductChange() {
    const sel = document.getElementById('prod-add-product');
    const opt = sel.options[sel.selectedIndex];
    const varGroup = document.getElementById('prod-add-variation-group');
    const varSel = document.getElementById('prod-add-variation');
    if (!opt || !opt.value) { varGroup.style.display = 'none'; return; }
    const variations = JSON.parse(opt.dataset.variations || '[]');
    if (variations.length === 0) { varGroup.style.display = 'none'; return; }
    varSel.innerHTML = '<option value="">— Variante wählen (optional) —</option>' +
        variations.map(v => `<option value="${v.id}">${escHtml(v.name)} · ${v.sku}</option>`).join('');
    varGroup.style.display = '';
}

async function confirmAddProductionItem() {
    const productId = parseInt(document.getElementById('prod-add-product').value);
    if (!productId) { showToast('Bitte ein Produkt wählen.'); return; }
    const variationIdRaw = document.getElementById('prod-add-variation').value;
    const variationId = variationIdRaw ? parseInt(variationIdRaw) : null;
    const quantity = parseInt(document.getElementById('prod-add-qty').value) || 1;
    const note = document.getElementById('prod-add-note').value.trim() || null;
    await api(withProject('/api/production'), 'POST', { productId, variationId, quantity, note });
    closeProductionAddModal();
    productionData = await api(withProject('/api/production'));
    renderProdList();
    showToast('✓ Zur Produktion hinzugefügt');
}

function closeProductionAddModal() {
    document.getElementById('production-add-modal').classList.remove('open');
}

// ── ÜBERFÄLLIGE AUFGABEN ──
let _overdueDismissed = false;

function getOverdueTasks() {
    if (!appData || !state) return [];
    const cur = window._currentWeek ?? 1;
    const result = [];
    for (const week of appData.weeks) {
        if (week.number >= cur) continue;
        for (const task of week.tasks) {
            if (!state[`task-${task.id}`]) {
                result.push({ week, task });
            }
        }
    }
    return result;
}

function updateOverdueBanner() {
    const overdue = getOverdueTasks();
    const banner = document.getElementById('overdue-banner');
    const badge = document.getElementById('overdue-badge');
    const count = document.getElementById('overdue-count');
    if (!banner) return;
    if (overdue.length === 0) {
        banner.style.display = 'none';
        if (badge) badge.style.display = 'none';
        return;
    }
    if (badge) { badge.textContent = overdue.length; badge.style.display = ''; }
    if (_overdueDismissed) { banner.style.display = 'none'; return; }
    if (count) count.textContent = overdue.length;
    banner.style.display = '';
}

function dismissOverdueBanner() {
    _overdueDismissed = true;
    document.getElementById('overdue-banner').style.display = 'none';
}

function showOverdueTasks() {
    const overdue = getOverdueTasks();
    if (overdue.length === 0) return;
    showPage('roadmap');
    const firstWeekNr = overdue[0].week.number;
    const body = document.getElementById('wb-' + firstWeekNr);
    const chev = document.getElementById('chev-' + firstWeekNr);
    if (body && !body.classList.contains('open')) {
        body.classList.add('open');
        if (chev) chev.classList.add('open');
    }
    setTimeout(() => {
        const card = document.getElementById('week-' + firstWeekNr);
        if (card) card.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 100);
}
