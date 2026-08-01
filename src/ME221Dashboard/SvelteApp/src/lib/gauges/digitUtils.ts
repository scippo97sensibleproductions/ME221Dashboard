export function formatDigits(
  value: number,
  decimals: number,
  zeroPad: boolean,
  minDigits: number
): string {
  if (!Number.isFinite(value)) return String(value);
  let s = decimals >= 0 ? value.toFixed(decimals) : String(value);
  const target = Math.max(0, minDigits);
  if ((zeroPad || target > 0) && s.length < target) {
    const sign = s.startsWith('-') ? '-' : '';
    const rest = sign ? s.slice(1) : s;
    s = sign + '0'.repeat(target - rest.length) + rest;
  }
  return s;
}
