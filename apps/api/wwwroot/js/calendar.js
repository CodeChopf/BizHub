// ── KALENDER ──
let calEvents = [];
let calYear = new Date().getFullYear();
let calMonth = new Date().getMonth(); // 0-based
let _calEditId = null;
let _calColor = '#4f8ef7';
let _calPopupDate = null;

async function renderKalender() {
    calEvents = await api(withProject('/api/calendar'));
    drawCalendar();
}

function calToday() { calYear = new Date().getFullYear(); calMonth = new Date().getMonth(); drawCalendar(); }
function calPrevMonth() { calMonth--; if (calMonth < 0) { calMonth = 11; calYear--; } drawCalendar(); }
function calNextMonth() { calMonth++; if (calMonth > 11) { calMonth = 0; calYear++; } drawCalendar(); }

const CAL_MONTHS = ['Januar','Februar','März','April','Mai','Juni','Juli','August','September','Oktober','November','Dezember'];
const EVENT_TYPE_ICONS = { event:'📌', deadline:'⏰', meeting:'🤝', delivery:'📦' };

function drawCalendar() {
    const label = document.getElementById('cal-month-label');
    if (label) label.textContent = CAL_MONTHS[calMonth] + ' ' + calYear;

    const grid = document.getElementById('cal-grid');
    if (!grid) return;

    const firstDay = new Date(calYear, calMonth, 1);
    let startOffset = firstDay.getDay() - 1; // Mon-based
    if (startOffset < 0) startOffset = 6;

    const daysInMonth = new Date(calYear, calMonth + 1, 0).getDate();
    const daysInPrevMonth = new Date(calYear, calMonth, 0).getDate();

    const todayStr = today.toISOString().split('T')[0];

    const weekRanges = appData ? getWeekRanges() : [];

    const expensesByDate = {};
    if (financeData) {
        for (const exp of (financeData.expenses ?? [])) {
            if (!expensesByDate[exp.date]) expensesByDate[exp.date] = [];
            expensesByDate[exp.date].push(exp);
        }
    }

    const totalCells = 42;
    let html = '';

    for (let i = 0; i < totalCells; i++) {
        const dayNum = i - startOffset + 1;
        let dateStr, isOtherMonth = false;

        if (dayNum < 1) {
            const d = daysInPrevMonth + dayNum;
            const m = calMonth === 0 ? 12 : calMonth;
            const y = calMonth === 0 ? calYear - 1 : calYear;
            dateStr = `${y}-${String(m).padStart(2,'0')}-${String(d).padStart(2,'0')}`;
            isOtherMonth = true;
        } else if (dayNum > daysInMonth) {
            const d = dayNum - daysInMonth;
            const m = calMonth === 11 ? 1 : calMonth + 2;
            const y = calMonth === 11 ? calYear + 1 : calYear;
            dateStr = `${y}-${String(m).padStart(2,'0')}-${String(d).padStart(2,'0')}`;
            isOtherMonth = true;
        } else {
            dateStr = `${calYear}-${String(calMonth+1).padStart(2,'0')}-${String(dayNum).padStart(2,'0')}`;
        }

        const isToday = dateStr === todayStr;
        const isActiveWeek = weekRanges.some(r => dateStr >= fmtISO(r.start) && dateStr <= fmtISO(r.end) && weekRanges.indexOf(r) + 1 === window._currentWeek);

        let classes = 'cal-day';
        if (isOtherMonth) classes += ' other-month';
        if (isToday) classes += ' today';
        if (isActiveWeek) classes += ' week-active';

        let chips = '';
        for (const ev of calEvents) {
            const evStart = ev.date;
            const evEnd = ev.endDate ?? ev.date;
            if (dateStr >= evStart && dateStr <= evEnd) {
                const icon = EVENT_TYPE_ICONS[ev.type] ?? '📌';
                chips += `<div class="cal-chip" style="background:${ev.color}20;border-left:3px solid ${ev.color}"
                    onclick="openCalEventModal(${ev.id});event.stopPropagation()">
                    ${icon} ${escHtml(ev.title)}${ev.time ? ' · ' + ev.time : ''}
                </div>`;
            }
        }
        const dayExpenses = expensesByDate[dateStr] ?? [];
        if (dayExpenses.length > 0) {
            const total = dayExpenses.reduce((s, e) => s + e.amount, 0);
            chips += `<div class="cal-chip cal-chip-expense">💰 ${formatCurrency(total)}</div>`;
        }

        const displayDay = isOtherMonth ? '' : dayNum;
        html += `<div class="${classes}" onclick="openCalDayPopup('${dateStr}', event)">
            <div class="cal-day-num">${displayDay}</div>
            <div class="cal-chips">${chips}</div>
        </div>`;
    }

    grid.innerHTML = html;
    closeCalDayPopup();
}

function fmtISO(d) {
    if (!d) return '';
    const dt = new Date(d);
    return dt.toISOString().split('T')[0];
}

function formatCurrency(amount) {
    const cur = getCurrency();
    return amount.toFixed(2) + ' ' + cur;
}

// ── Tages-Detail-Popup ──
function openCalDayPopup(dateStr, e) {
    _calPopupDate = dateStr;
    const popup = document.getElementById('cal-day-popup');
    const dateLabel = document.getElementById('cal-popup-date');
    const items = document.getElementById('cal-popup-items');
    if (!popup) return;

    const [y, m, d] = dateStr.split('-');
    dateLabel.textContent = `${d}. ${CAL_MONTHS[parseInt(m)-1]} ${y}`;

    let html = '';
    for (const ev of calEvents) {
        if (dateStr >= ev.date && dateStr <= (ev.endDate ?? ev.date)) {
            const icon = EVENT_TYPE_ICONS[ev.type] ?? '📌';
            html += `<div class="cal-popup-item" onclick="openCalEventModal(${ev.id})">
                <span class="cal-popup-dot" style="background:${ev.color}"></span>
                <div>
                    <div class="cal-popup-title">${icon} ${escHtml(ev.title)}</div>
                    ${ev.time ? `<div class="cal-popup-meta">${ev.time}</div>` : ''}
                    ${ev.description ? `<div class="cal-popup-meta">${escHtml(ev.description)}</div>` : ''}
                </div>
            </div>`;
        }
    }
    if (financeData) {
        for (const exp of (financeData.expenses ?? [])) {
            if (exp.date === dateStr) {
                html += `<div class="cal-popup-item">
                    <span class="cal-popup-dot" style="background:var(--green)"></span>
                    <div>
                        <div class="cal-popup-title">💰 ${escHtml(exp.description)}</div>
                        <div class="cal-popup-meta">${formatCurrency(exp.amount)}</div>
                    </div>
                </div>`;
            }
        }
    }
    if (!html) html = '<div style="color:var(--text3);font-size:13px;padding:8px 0">Keine Einträge</div>';
    items.innerHTML = html;

    const rect = e.currentTarget.getBoundingClientRect();
    const mainRect = document.querySelector('.main-content').getBoundingClientRect();
    popup.style.display = 'block';
    popup.style.top = (rect.bottom - mainRect.top + document.querySelector('.main-content').scrollTop + 4) + 'px';
    const left = Math.min(rect.left - mainRect.left, mainRect.width - 280);
    popup.style.left = Math.max(0, left) + 'px';
}

function closeCalDayPopup() {
    const popup = document.getElementById('cal-day-popup');
    if (popup) popup.style.display = 'none';
    _calPopupDate = null;
}

// ── Event Modal ──
function openCalEventModal(id, prefillDate) {
    _calEditId = id;
    _calColor = '#4f8ef7';
    document.getElementById('cal-modal-title').textContent = id ? 'Ereignis bearbeiten' : 'Ereignis erstellen';
    document.getElementById('cal-ev-delete-btn').style.display = id ? '' : 'none';
    document.getElementById('cal-ev-title').value = '';
    document.getElementById('cal-ev-date').value = prefillDate ?? _calPopupDate ?? new Date().toISOString().split('T')[0];
    document.getElementById('cal-ev-enddate').value = '';
    document.getElementById('cal-ev-time').value = '';
    document.getElementById('cal-ev-type').value = 'event';
    document.getElementById('cal-ev-desc').value = '';
    document.querySelectorAll('#cal-ev-color-picker .color-dot').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.color === '#4f8ef7');
    });

    if (id) {
        const ev = calEvents.find(e => e.id === id);
        if (ev) {
            _calColor = ev.color;
            document.getElementById('cal-ev-title').value = ev.title;
            document.getElementById('cal-ev-date').value = ev.date;
            document.getElementById('cal-ev-enddate').value = ev.endDate ?? '';
            document.getElementById('cal-ev-time').value = ev.time ?? '';
            document.getElementById('cal-ev-type').value = ev.type;
            document.getElementById('cal-ev-desc').value = ev.description ?? '';
            document.querySelectorAll('#cal-ev-color-picker .color-dot').forEach(btn => {
                btn.classList.toggle('active', btn.dataset.color === ev.color);
            });
        }
    }
    document.getElementById('cal-event-modal').classList.add('open');
}

function closeCalEventModal() {
    document.getElementById('cal-event-modal').classList.remove('open');
}

function selectCalColor(color, btn) {
    _calColor = color;
    document.querySelectorAll('#cal-ev-color-picker .color-dot').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
}

async function saveCalEvent() {
    const title = document.getElementById('cal-ev-title').value.trim();
    const date  = document.getElementById('cal-ev-date').value;
    if (!title || !date) { showToast('Titel und Datum sind Pflicht.'); return; }
    const endDate     = document.getElementById('cal-ev-enddate').value || null;
    const time        = document.getElementById('cal-ev-time').value || null;
    const description = document.getElementById('cal-ev-desc').value.trim() || null;
    const type        = document.getElementById('cal-ev-type').value;
    const payload = { title, date, endDate, time, description, color: _calColor, type };

    if (_calEditId) {
        await api(withProject(`/api/calendar/${_calEditId}`), 'PUT', payload);
        showToast('✓ Ereignis aktualisiert');
    } else {
        await api(withProject('/api/calendar'), 'POST', payload);
        showToast('✓ Ereignis erstellt');
    }
    closeCalEventModal();
    calEvents = await api(withProject('/api/calendar'));
    drawCalendar();
}

async function deleteCalEvent() {
    if (!_calEditId || !confirm('Ereignis löschen?')) return;
    await api(withProject(`/api/calendar/${_calEditId}`), 'DELETE');
    showToast('✓ Ereignis gelöscht');
    closeCalEventModal();
    calEvents = await api(withProject('/api/calendar'));
    drawCalendar();
}
