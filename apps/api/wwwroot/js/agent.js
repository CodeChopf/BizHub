// ─── KI-Assistent Chat ────────────────────────────────────────────────────────

let _agentOpen    = false;
let _agentHistory = [];   // { role: 'user'|'assistant', content: string }[]  — Browser-Memory only
let _agentPending = null; // AgentAction | null
let _agentBusy    = false;

function toggleAgentChat() {
    _agentOpen = !_agentOpen;
    const panel = document.getElementById('agent-panel');
    const fab   = document.getElementById('agent-fab');
    if (panel) panel.classList.toggle('open', _agentOpen);
    if (fab)   fab.classList.toggle('active', _agentOpen);
    if (_agentOpen && _agentHistory.length === 0) {
        _agentWelcome();
    }
}

function _agentWelcome() {
    _agentRenderMessage('assistant',
        'Hallo! Ich bin dein KI-Assistent für BizHub. Ich kann dir helfen, deinen Projektplan zu verwalten — Wochen, Tasks und Produkte lesen und erstellen. Was kann ich für dich tun?\n\n' +
        '*(Hello! I\'m your AI assistant for BizHub. I can help you manage your project plan — read and create weeks, tasks and products. What can I do for you?)*');
}

function agentSend() {
    if (_agentBusy) return;
    const input = document.getElementById('agent-input');
    const text  = (input?.value ?? '').trim();
    if (!text) return;
    input.value = '';

    _agentRenderMessage('user', escHtml(text));
    _agentHistory.push({ role: 'user', content: text });

    _agentPost({ messages: _agentHistory, confirmAction: null });
}

function agentConfirm() {
    if (_agentBusy || !_agentPending) return;
    _agentPost({ messages: _agentHistory, confirmAction: _agentPending });
}

function agentCancel() {
    _agentPending = null;
    _agentHideConfirmBar();
    _agentRenderMessage('assistant', 'Aktion abgebrochen. / Action cancelled.');
    _agentHistory.push({ role: 'assistant', content: 'Aktion abgebrochen. / Action cancelled.' });
}

async function _agentPost(body) {
    _agentBusy = true;
    _agentSetInputEnabled(false);

    const typingId = _agentShowTyping();

    let data;
    try {
        const res = await fetch(withProject('/api/agent/chat'), {
            method:  'POST',
            headers: { 'Content-Type': 'application/json' },
            body:    JSON.stringify(body)
        });

        _agentRemoveTyping(typingId);

        if (res.status === 402) {
            _agentShowLimitBar();
            return;
        }

        if (!res.ok) {
            _agentRenderMessage('assistant', 'Fehler: ' + res.status + ' ' + res.statusText);
            return;
        }

        data = await res.json();
    } catch (err) {
        _agentRemoveTyping(typingId);
        _agentRenderMessage('assistant', 'Verbindungsfehler. Bitte erneut versuchen. / Connection error, please try again.');
        return;
    } finally {
        _agentBusy = false;
        _agentSetInputEnabled(true);
        document.getElementById('agent-input')?.focus();
    }

    if (data.status === 'ok') {
        _agentPending = null;
        _agentHideConfirmBar();
        _agentRenderMessage('assistant', _agentMarkdown(data.message));
        _agentHistory.push({ role: 'assistant', content: data.message });

    } else if (data.status === 'confirmation_required') {
        _agentRenderMessage('assistant', _agentMarkdown(data.message));
        _agentHistory.push({ role: 'assistant', content: data.message });
        _agentPending = data.pendingAction;
        _agentShowConfirmBar(data.pendingAction?.summary ?? '');

    } else if (data.status === 'limit_exceeded') {
        _agentShowLimitBar();
    }
}

// ─── Confirm Bar ─────────────────────────────────────────────────────────────

function _agentShowConfirmBar(summary) {
    const bar  = document.getElementById('agent-confirm-bar');
    const text = document.getElementById('agent-confirm-summary');
    if (bar)  bar.style.display = 'block';
    if (text) text.textContent = summary;
}

function _agentHideConfirmBar() {
    const bar = document.getElementById('agent-confirm-bar');
    if (bar) bar.style.display = 'none';
}

function _agentShowLimitBar() {
    const bar = document.getElementById('agent-limit-bar');
    if (bar) bar.style.display = 'flex';
}

// ─── Message Rendering ────────────────────────────────────────────────────────

function _agentRenderMessage(role, html) {
    const list = document.getElementById('agent-messages');
    if (!list) return;

    const div = document.createElement('div');
    div.className = 'agent-msg agent-msg-' + role;
    div.innerHTML  = html;
    list.appendChild(div);
    list.scrollTop = list.scrollHeight;
}

function _agentShowTyping(id) {
    const uid = 'typing-' + Date.now();
    const list = document.getElementById('agent-messages');
    if (!list) return uid;
    const div = document.createElement('div');
    div.id        = uid;
    div.className = 'agent-msg agent-msg-assistant agent-typing';
    div.innerHTML = '<span></span><span></span><span></span>';
    list.appendChild(div);
    list.scrollTop = list.scrollHeight;
    return uid;
}

function _agentRemoveTyping(uid) {
    document.getElementById(uid)?.remove();
}

// ─── Markdown (minimal, no external deps) ────────────────────────────────────

function _agentMarkdown(text) {
    if (!text) return '';
    // Escape HTML first (except we allow certain tags)
    let s = text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
    // Bold **text**
    s = s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    // Italic *text*
    s = s.replace(/\*(.+?)\*/g, '<em>$1</em>');
    // Inline code `code`
    s = s.replace(/`([^`]+)`/g, '<code>$1</code>');
    // Bullet lists
    s = s.replace(/^[\-\*] (.+)$/gm, '<li>$1</li>');
    s = s.replace(/(<li>.*<\/li>(\n|$))+/g, '<ul>$&</ul>');
    // Newlines → <br>
    s = s.replace(/\n/g, '<br>');
    return s;
}

// ─── Input Helpers ────────────────────────────────────────────────────────────

function _agentSetInputEnabled(enabled) {
    const input = document.getElementById('agent-input');
    const btn   = document.getElementById('agent-send-btn');
    if (input) input.disabled = !enabled;
    if (btn)   btn.disabled   = !enabled;
}
