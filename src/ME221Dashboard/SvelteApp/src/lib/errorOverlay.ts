// Error overlays for uncaught runtime errors (e.g. component render failures)
// that svelte-hmr cannot show, so a broken edit never looks like a silent blank.
//
// - DEV: technical overlay with stack trace, auto-clears on the next hot update.
// - Release: user-friendly modal that matches the app theme.

const THROTTLE_MS = 2000;
let lastShown = 0;
let lastErrorAt = 0;

// Chromium (incl. Android WebView) reports ResizeObserver feedback loops as
// window 'error' events. They are layout diagnostics, not app crashes —
// never surface them to the user (or the console).
const BENIGN_ERROR_MESSAGES = [
  'ResizeObserver loop completed with undelivered notifications',
  'ResizeObserver loop limit exceeded',
];

function isBenignError(e: ErrorEvent | PromiseRejectionEvent): boolean {
  const err = e instanceof ErrorEvent ? (e.error ?? e.message) : e.reason;
  const msg = err instanceof Error ? err.message : String(err ?? '');
  return BENIGN_ERROR_MESSAGES.some((m) => msg.includes(m));
}

function throttled(): boolean {
  const now = Date.now();
  if (now - lastShown < THROTTLE_MS) return false;
  lastShown = now;
  return true;
}

// ── Dev overlay ──────────────────────────────────────────────────────────────

let devRoot: HTMLDivElement | null = null;
let devMsg: HTMLDivElement | null = null;
let devStack: HTMLPreElement | null = null;

function ensureDevEl(): void {
  if (devRoot) return;
  devRoot = document.createElement('div');
  devRoot.style.cssText = [
    'position:fixed',
    'top:12px',
    'right:12px',
    'max-width:min(560px,calc(100vw - 24px))',
    'z-index:99999',
    'background:#2a0f0f',
    'border:1px solid #ef4444',
    'border-radius:8px',
    'padding:12px 14px',
    'color:#f8fafc',
    'font:12px/1.45 ui-monospace,Consolas,monospace',
    'box-shadow:0 8px 30px rgba(0,0,0,.5)',
    'display:none',
  ].join(';');

  const title = document.createElement('div');
  title.textContent = 'Runtime error';
  title.style.cssText = 'font-weight:700;color:#f87171;margin-bottom:6px';

  devMsg = document.createElement('div');
  devMsg.style.cssText = 'white-space:pre-wrap;word-break:break-word;color:#fff;margin-bottom:8px';

  const stack = document.createElement('details');
  const summary = document.createElement('summary');
  summary.textContent = 'stack';
  summary.style.cssText = 'cursor:pointer;color:#94a3b8';
  devStack = document.createElement('pre');
  devStack.style.cssText = 'margin:6px 0 0;max-height:240px;overflow:auto;font-size:11px;color:#94a3b8;white-space:pre-wrap;word-break:break-word';
  stack.append(summary, devStack);

  const buttons = document.createElement('div');
  buttons.style.cssText = 'display:flex;gap:8px;margin-top:10px';
  const btnStyle = 'padding:4px 10px;border-radius:6px;border:1px solid #475569;background:#1e293b;color:#e2e8f0;cursor:pointer;font:11px ui-sans-serif,system-ui';
  const dismiss = document.createElement('button');
  dismiss.textContent = 'Dismiss';
  dismiss.style.cssText = btnStyle;
  dismiss.onclick = hideDevError;
  const reload = document.createElement('button');
  reload.textContent = 'Reload app';
  reload.style.cssText = btnStyle;
  reload.onclick = () => location.reload();
  buttons.append(dismiss, reload);

  devRoot.append(title, devMsg, stack, buttons);
  (document.body ?? document.documentElement).appendChild(devRoot);
}

export function showDevError(message: string, stack?: string): void {
  ensureDevEl();
  lastErrorAt = Date.now();
  if (devMsg) devMsg.textContent = message;
  if (devStack) devStack.textContent = stack ?? '';
  if (devRoot) devRoot.style.display = 'block';
}

export function hideDevError(): void {
  if (devRoot) devRoot.style.display = 'none';
}

/**
 * Called on every successful hot update. Hides the overlay only when the
 * last error is not the update that just ran (a broken edit throws DURING
 * the update, so its error is fresh when `vite:afterUpdate` fires).
 */
export function handleDevHotUpdate(): void {
  if (Date.now() - lastErrorAt > 1000) hideDevError();
}

export function attachDevErrorOverlay(): void {
  window.addEventListener('error', (e) => {
    if (isBenignError(e)) {
      e.preventDefault();
      return;
    }
    const err = e.error instanceof Error ? e.error : new Error(String(e.message));
    showDevError(err.message, err.stack);
  });
  window.addEventListener('unhandledrejection', (e) => {
    if (isBenignError(e)) return;
    const reason = e.reason instanceof Error ? e.reason : new Error(String(e.reason));
    showDevError(reason.message, reason.stack);
  });
}

// ── Release overlay ──────────────────────────────────────────────────────────
// Metro-style error dialog (docs/ui-style-guide.md §5.5):
// solid #222 body, 1px #444 border, zero radius, 48px solid domain-color
// header bar (red = error domain), Segoe UI type, no blur, no shadows.

const METRO_FONT = "'Segoe UI', 'Inter', system-ui, -apple-system, sans-serif";
const METRO_MONO = "'Cascadia Code', 'JetBrains Mono', 'Fira Code', 'Consolas', monospace";

let relRoot: HTMLDivElement | null = null;
let relCard: HTMLDivElement | null = null;
let relDetail: HTMLPreElement | null = null;
let relToggle: HTMLButtonElement | null = null;
let relPrimary: HTMLButtonElement | null = null;
let relVisible = false;

const ALERT_OCTAGON = `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M7.86 2h8.28L22 7.86v8.28L16.14 22H7.86L2 16.14V7.86L7.86 2z"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`;

function injectReleaseStyles(): void {
  const style = document.createElement('style');
  style.textContent = `
    .me221-ov-card { animation: me221-ov-in .2s ease both; }
    @keyframes me221-ov-in { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
    .me221-ov-btn:focus-visible { box-shadow: inset 0 0 0 2px #ffffff; }
    @media (prefers-reduced-motion: reduce) { .me221-ov-card { animation: none !important; } }`;
  document.head.appendChild(style);
}

function ensureReleaseEl(): void {
  if (relRoot) return;
  injectReleaseStyles();

  relRoot = document.createElement('div');
  relRoot.setAttribute('role', 'dialog');
  relRoot.setAttribute('aria-modal', 'true');
  relRoot.setAttribute('aria-labelledby', 'me221-ov-title');
  relRoot.style.cssText = [
    'position:fixed',
    'inset:0',
    'z-index:99998',
    'display:none',
    'align-items:center',
    'justify-content:center',
    'padding:20px',
    'background:rgba(0,0,0,0.8)',
    'font:13px/1.4 ' + METRO_FONT,
    'color:#FFFFFF',
  ].join(';');
  relRoot.onclick = (e) => {
    if (e.target === relRoot) hideReleaseError();
  };

  relCard = document.createElement('div');
  relCard.className = 'me221-ov-card';
  relCard.style.cssText = [
    'width:100%',
    'max-width:400px',
    'background:#222222',
    'border:1px solid #444444',
    'border-radius:0',
    'box-shadow:0 8px 24px rgba(0,0,0,0.5)',
  ].join(';');

  // 48px solid header bar (error domain accent)
  const header = document.createElement('div');
  header.style.cssText = [
    'height:48px',
    'padding:0 16px',
    'display:flex',
    'align-items:center',
    'justify-content:space-between',
    'background:#E81123',
    'color:#FFFFFF',
  ].join(';');

  const headerTitle = document.createElement('div');
  headerTitle.id = 'me221-ov-title';
  headerTitle.style.cssText = 'display:flex;align-items:center;gap:8px;font-size:13px;font-weight:700;letter-spacing:0.5px;text-transform:uppercase';
  headerTitle.innerHTML = `${ALERT_OCTAGON}<span>Error</span>`;

  const closeBtn = document.createElement('button');
  closeBtn.type = 'button';
  closeBtn.setAttribute('aria-label', 'Close');
  closeBtn.textContent = '\u00d7';
  closeBtn.className = 'me221-ov-btn';
  closeBtn.style.cssText = 'width:32px;height:32px;background:none;border:none;color:#FFFFFF;font-size:20px;line-height:1;cursor:pointer';
  closeBtn.onclick = hideReleaseError;
  header.append(headerTitle, closeBtn);

  const body = document.createElement('div');
  body.style.cssText = 'padding:20px 16px 16px';

  const message = document.createElement('div');
  message.textContent = 'Something went wrong. All your dashboards and settings are safe. Press RESTART APP to continue.';
  message.style.cssText = 'font-size:13px;line-height:1.5;color:#FFFFFF';

  relDetail = document.createElement('pre');
  relDetail.style.cssText = [
    'display:none',
    'margin:12px 0 0',
    'max-height:150px',
    'overflow:auto',
    'background:#1A1A1A',
    'border:1px solid #333333',
    'padding:8px 10px',
    'font:11px/1.5 ' + METRO_MONO,
    'color:#A0A0A0',
    'white-space:pre-wrap',
    'word-break:break-word',
  ].join(';');

  relToggle = document.createElement('button');
  relToggle.type = 'button';
  relToggle.textContent = 'Show technical details';
  relToggle.className = 'me221-ov-btn';
  relToggle.style.cssText = 'margin-top:12px;padding:0;background:none;border:none;cursor:pointer;font:inherit;font-size:10px;font-weight:600;letter-spacing:0.5px;text-transform:uppercase;color:#A0A0A0';
  relToggle.onmouseenter = () => (relToggle!.style.color = '#FFFFFF');
  relToggle.onmouseleave = () => (relToggle!.style.color = '#A0A0A0');
  relToggle.onclick = () => {
    const show = relDetail!.style.display !== 'block';
    relDetail!.style.display = show ? 'block' : 'none';
    relToggle!.textContent = show ? 'Hide technical details' : 'Show technical details';
  };

  const buttons = document.createElement('div');
  buttons.style.cssText = 'display:flex;justify-content:flex-end;gap:8px;margin-top:20px';

  const secondary = document.createElement('button');
  secondary.type = 'button';
  secondary.textContent = 'Stay here';
  secondary.className = 'me221-ov-btn';
  secondary.style.cssText = [
    'min-height:36px',
    'padding:0 16px',
    'background:transparent',
    'border:1px solid #444444',
    'color:#A0A0A0',
    'font:inherit',
    'font-size:13px',
    'font-weight:700',
    'letter-spacing:0.5px',
    'text-transform:uppercase',
    'cursor:pointer',
  ].join(';');
  secondary.onmouseenter = () => { secondary.style.background = '#2A2A2A'; secondary.style.color = '#FFFFFF'; };
  secondary.onmouseleave = () => { secondary.style.background = 'transparent'; secondary.style.color = '#A0A0A0'; };
  secondary.onclick = hideReleaseError;

  relPrimary = document.createElement('button');
  relPrimary.type = 'button';
  relPrimary.textContent = 'Restart app';
  relPrimary.className = 'me221-ov-btn';
  relPrimary.style.cssText = [
    'min-height:36px',
    'padding:0 16px',
    'background:#E81123',
    'border:none',
    'color:#FFFFFF',
    'font:inherit',
    'font-size:13px',
    'font-weight:700',
    'letter-spacing:0.5px',
    'text-transform:uppercase',
    'cursor:pointer',
  ].join(';');
  relPrimary.onmouseenter = () => (relPrimary!.style.background = '#C50F1F');
  relPrimary.onmouseleave = () => (relPrimary!.style.background = '#E81123');
  relPrimary.onclick = () => location.reload();

  buttons.append(secondary, relPrimary);

  const footer = document.createElement('div');
  footer.style.cssText = 'display:flex;align-items:center;justify-content:space-between;gap:8px;padding:0 16px 16px';
  footer.append(relToggle, buttons);

  body.append(message, relDetail);
  relCard.append(header, body, footer);
  relRoot.appendChild(relCard);
  (document.body ?? document.documentElement).appendChild(relRoot);

  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && relVisible) hideReleaseError();
  });
}

export function showReleaseError(message: string, stack?: string): void {
  ensureReleaseEl();
  if (relDetail) relDetail.textContent = message + (stack ? `\n\n${stack}` : '');
  if (relRoot) relRoot.style.display = 'flex';
  relVisible = true;
  relPrimary?.focus({ preventScroll: true });
}

export function hideReleaseError(): void {
  if (relRoot) relRoot.style.display = 'none';
  relVisible = false;
}

export function attachReleaseErrorOverlay(): void {
  window.addEventListener('error', (e) => {
    if (isBenignError(e)) {
      e.preventDefault();
      return;
    }
    if (!throttled()) return;
    const err = e.error instanceof Error ? e.error : new Error(String(e.message));
    showReleaseError(err.message, err.stack);
  });
  window.addEventListener('unhandledrejection', (e) => {
    if (isBenignError(e)) return;
    if (!throttled()) return;
    const reason = e.reason instanceof Error ? e.reason : new Error(String(e.reason));
    showReleaseError(reason.message, reason.stack);
  });
}
