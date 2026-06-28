export type ToastType = 'success' | 'error' | 'warning' | 'info';

export interface Toast {
  id: number;
  message: string;
  type: ToastType;
  dismissing: boolean;
}

let nextId = 0;
let toasts = $state<Toast[]>([]);

export function getToasts(): Toast[] {
  return toasts;
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
