export type ToastType = 'success' | 'error' | 'warning' | 'info';

export interface WarningToastMeta {
  dataId: number;
  levelId: string;
  levelName: string;
  evictionClass: 'activation' | 'escalation';
  deadline: number;
  remaining: number;
  announced: boolean;
  displayed: boolean;
  firstRunQueued: boolean;
}

export interface Toast {
  id: number;
  message: string;
  type: ToastType;
  dismissing: boolean;
  meta?: WarningToastMeta;
}

let nextId = 0;
let toasts = $state<Toast[]>([]);

export function getToasts(): Toast[] {
  return toasts;
}

export function clearToasts(): void {
  toasts = [];
}

export function toast(message: string, type: ToastType = 'info', duration = 3000): void {
  const id = nextId++;
  const t: Toast = { id, message, type, dismissing: false };
  toasts = [...toasts, t];

  setTimeout(() => {
    toasts = toasts.map(x => x.id === id ? { ...x, dismissing: true } : x);
    setTimeout(() => {
      toasts = toasts.filter(x => x.id !== id);
    }, 300);
  }, duration);
}

export function addToast(t: { message: string; type?: ToastType; meta?: WarningToastMeta }): number {
  const id = nextId++;
  toasts = [...toasts, { id, message: t.message, type: t.type ?? 'info', dismissing: false, meta: t.meta }];
  return id;
}

export function updateToast(id: number, patch: Partial<Toast>): void {
  toasts = toasts.map(t => (t.id === id ? { ...t, ...patch } : t));
}

export function removeToast(id: number): void {
  toasts = toasts.filter(t => t.id !== id);
}

export function setToastDisplayed(id: number, displayed: boolean): void {
  toasts = toasts.map(t => {
    if (t.id !== id || !t.meta) return t;
    return { ...t, meta: { ...t.meta, displayed } };
  });
}

