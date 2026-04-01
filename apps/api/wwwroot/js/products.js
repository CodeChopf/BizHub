// ── PRODUKTE GENERISCH (Legacy) ──
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

    if (!activeProductTypeId || !productData2.productTypes.find(t => t.id === activeProductTypeId)) {
        activeProductTypeId = productData2.productTypes[0].id;
    }

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

// ── PRODUKT MODAL (Legacy) ──
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
