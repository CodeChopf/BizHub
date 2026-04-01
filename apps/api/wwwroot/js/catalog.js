// ── PRODUKTE KATALOG ──
let catalogData = null;
let activeCategoryId = null;
let editingCatalogProductId = null;
let managingAttributesCategoryId = null;
let editingVariationId = null;

async function loadCatalog() {
    catalogData = await api(withProject('/api/catalog'));
}

async function renderProdukte() {
    await loadCatalog();

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
    await api(withProject('/api/catalog/categories'), 'POST', { name, color, description: desc });
    catalogData = await api(withProject('/api/catalog'));
    renderCatalogCategoriesList();
    document.getElementById('new-cat-type-name').value = '';
    document.getElementById('new-cat-type-desc').value = '';
    showToast('✓ Kategorie erstellt');
}

async function deleteCatalogCategory(id) {
    const cat = catalogData.categories.find(c => c.id === id);
    const count = catalogData.products.filter(p => p.categoryId === id).length;
    if (!confirm(`Kategorie "${cat?.name}" löschen? ${count > 0 ? `${count} Produkte werden ebenfalls gelöscht.` : ''}`)) return;
    await api(withProject('/api/catalog/categories/' + id), 'DELETE');
    catalogData = await api(withProject('/api/catalog'));
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
    await api(withProject(`/api/catalog/categories/${managingAttributesCategoryId}/attributes`), 'POST', { name, fieldType, options, required, sortOrder });
    catalogData = await api(withProject('/api/catalog'));
    renderAttributesList();
    document.getElementById('new-attr-name').value = '';
    document.getElementById('new-attr-options').value = '';
    document.getElementById('new-attr-required').checked = false;
    showToast('✓ Attribut hinzugefügt');
}

async function deleteCatalogAttribute(id) {
    if (!confirm('Attribut löschen?')) return;
    await api(withProject('/api/catalog/attributes/' + id), 'DELETE');
    catalogData = await api(withProject('/api/catalog'));
    renderAttributesList();
    showToast('✓ Attribut gelöscht');
}

// ── PRODUKT MODAL (Katalog) ──
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
            await api(withProject('/api/catalog/products/' + editingCatalogProductId), 'PUT', { name, description: desc, attributeValues: JSON.stringify(attributeValues) });
            showToast('✓ Produkt aktualisiert');
        } else {
            await api(withProject('/api/catalog/products'), 'POST', { categoryId, name, description: desc, attributeValues: JSON.stringify(attributeValues) });
            showToast('✓ Produkt hinzugefügt');
        }
        catalogData = await api(withProject('/api/catalog'));
        closeCatalogProductModal();
        renderCatalogMain();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteCatalogProduct(id) {
    const product = catalogData.products.find(p => p.id === id);
    if (!confirm(`Produkt "${product?.name}" löschen? Alle Variationen werden ebenfalls gelöscht.`)) return;
    await api(withProject('/api/catalog/products/' + id), 'DELETE');
    catalogData = await api(withProject('/api/catalog'));
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
        const res = await fetch(withProject(`/api/catalog/products/${productId}/sku?variationName=${encodeURIComponent(varName)}`));
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
            const res = await fetch(withProject('/api/catalog/variations/' + editingVariationId), {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name, sku, price, stock })
            });
            const data = await res.json();
            if (data.error) { skuError.textContent = data.error; skuError.style.display = 'block'; return; }
            showToast('✓ Variation aktualisiert');
        } else {
            const res = await fetch(withProject('/api/catalog/variations'), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ productId, name, sku, price, stock })
            });
            const data = await res.json();
            if (data.error) { skuError.textContent = data.error; skuError.style.display = 'block'; return; }
            showToast('✓ Variation hinzugefügt');
        }
        catalogData = await api(withProject('/api/catalog'));
        closeVariationModal();
        renderCatalogMain();
    } catch { showToast('Fehler beim Speichern.'); }
}

async function deleteVariation(id) {
    if (!confirm('Variation löschen?')) return;
    await api(withProject('/api/catalog/variations/' + id), 'DELETE');
    catalogData = await api(withProject('/api/catalog'));
    renderCatalogMain();
    showToast('✓ Variation gelöscht');
}
