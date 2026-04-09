// ── DATE HELPERS ──
function addDays(d, n) {
    const r = new Date(d); r.setDate(r.getDate() + n); return r;
}
function fmt(d) {
    return d.toLocaleDateString('de-CH', { day: '2-digit', month: '2-digit', year: 'numeric' });
}
function fmtShort(d) {
    return d.toLocaleDateString('de-CH', { day: '2-digit', month: '2-digit' });
}
function formatDateStr(dateStr) {
    if (!dateStr) return '';
    const [y, m, d] = dateStr.split('-');
    return `${d}.${m}.${y}`;
}
function fmtChf(amount) {
    return amount.toLocaleString('de-CH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}
function showToast(msg) {
    const t = document.getElementById('toast');
    t.textContent = msg;
    t.classList.add('show');
    setTimeout(() => t.classList.remove('show'), 2500);
}

function getWeekRanges() {
    const ranges = [];
    if (!appData?.weeks) return ranges;
    for (let i = 0; i < appData.weeks.length; i++) {
        ranges.push({ start: addDays(START, i * 7), end: addDays(START, i * 7 + 6) });
    }
    return ranges;
}

function escHtml(str) {
    return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

const today = new Date();
let state = {};
let appData = null;
let productData = null;
let financeData = null;

// ── API HELPERS ──
async function api(url, method = 'GET', body = null) {
    const opts = { method, headers: { 'Content-Type': 'application/json' } };
    if (body) opts.body = JSON.stringify(body);
    const res = await fetch(url, opts);
    const text = await res.text();
    const data = text ? (() => {
        try { return JSON.parse(text); } catch { return null; }
    })() : null;

    if (!res.ok) {
        const message = (data && typeof data === 'object' && data.error)
            ? data.error
            : (text || `${res.status} ${res.statusText}`);
        const err = new Error(message);
        err.status = res.status;
        err.payload = data;
        throw err;
    }

    return data;
}

function withProject(url) {
    const sep = url.includes('?') ? '&' : '?';
    return url + sep + 'projectId=' + _currentProjectId;
}

async function loadState() {
    try { state = await api(withProject('/api/state')); } catch { state = {}; }
}

async function saveState() {
    try { await api(withProject('/api/state'), 'POST', state); } catch { console.error('Speichern fehlgeschlagen'); }
}
