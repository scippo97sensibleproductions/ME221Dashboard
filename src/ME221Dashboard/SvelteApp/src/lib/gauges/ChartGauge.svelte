<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { buildColorLuts, gaugeValueColor } from './types';

  let { gauge, pixelWidth, pixelHeight }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
  } = $props();

  const MAX_POINTS = 6000;
  interface Pt { t: number; v: number }
  const buffers = new Map<number, Pt[]>();
  function getBuf(eid: number): Pt[] {
    let b = buffers.get(eid);
    if (!b) { b = []; buffers.set(eid, b); }
    return b;
  }
  function bisect(buf: Pt[], cutoff: number): number {
    let lo = 0, hi = buf.length;
    while (lo < hi) { const m = (lo + hi) >> 1; buf[m].t < cutoff ? lo = m + 1 : hi = m; }
    return lo;
  }

  let canvas: HTMLCanvasElement;
  let ctx: CanvasRenderingContext2D | null = null;
  let lastW = 0, lastH = 0;

  // ── Offscreen cache for static elements (grid, labels) ──
  let bgCanvas: OffscreenCanvas | null = null;
  let bgCtx: OffscreenCanvasRenderingContext2D | null = null;
  let bgKey = '';

  function drawBackground(cW: number, cH: number, pL: number, pT: number, yMin: number, yMax: number, yR: number) {
    const key = `${cW}|${cH}|${pL}|${yMin}|${yMax}`;
    if (key === bgKey) return;
    bgKey = key;

    bgCanvas = new OffscreenCanvas(cW + pL + 8, cH + pT + 16);
    bgCtx = bgCanvas.getContext('2d') as OffscreenCanvasRenderingContext2D;
    if (!bgCtx) return;

    // Grid
    if (gauge.chartShowGrid) {
      bgCtx.strokeStyle = 'rgba(255,255,255,0.08)'; bgCtx.lineWidth = 1;
      for (let i = 0; i <= 5; i++) {
        const y = pT + (cH * i) / 5;
        bgCtx.beginPath(); bgCtx.moveTo(pL, y); bgCtx.lineTo(pL + cW, y); bgCtx.stroke();
      }
    }

    // Y labels
    if (gauge.chartShowLabels) {
      bgCtx.fillStyle = 'rgba(255,255,255,0.4)'; bgCtx.font = '9px monospace';
      bgCtx.textAlign = 'right'; bgCtx.textBaseline = 'middle';
      for (let i = 0; i <= 5; i++) {
        const y = pT + (cH * i) / 5;
        const v = yMax - (yR * i) / 5;
        bgCtx.fillText(Math.abs(v) >= 1000 ? v.toFixed(0) : v.toFixed(1), pL - 4, y);
      }
    }

    // Time labels
    bgCtx.fillStyle = 'rgba(255,255,255,0.3)'; bgCtx.font = '8px monospace';
    bgCtx.textAlign = 'center'; bgCtx.textBaseline = 'top';
    const tl = Math.min(4, gauge.chartTimeWindowSec / 10);
    for (let i = 0; i <= tl; i++) {
      const x = pL + (cW * i) / tl;
      const s = Math.round(gauge.chartTimeWindowSec * (1 - i / tl));
      bgCtx.fillText(s === 0 ? 'now' : `-${s}s`, x, pT + cH + 4);
    }
  }

  function tick() {
    const eid = gauge.entityId;
    if (typeof eid !== 'number') return;

    const now = performance.now();
    const buf = getBuf(eid);
    buf.push({ t: now, v: gauge.value });
    const maxKeep = gauge.chartTimeWindowSec * 10 + 100;
    while (buf.length > maxKeep) buf.shift();

    if (!canvas) return;
    if (!ctx) ctx = canvas.getContext('2d');
    if (!ctx) return;

    const dpr = window.devicePixelRatio || 1;
    const w = pixelWidth, h = pixelHeight;
    if (w !== lastW || h !== lastH) {
      canvas.width = w * dpr;
      canvas.height = h * dpr;
      canvas.style.width = w + 'px';
      canvas.style.height = h + 'px';
      lastW = w; lastH = h;
      bgKey = ''; // force background redraw
    }
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, h);

    const pL = gauge.chartShowLabels ? 42 : 8, pR = 8, pT = 8, pB = 16;
    const cW = w - pL - pR, cH = h - pT - pB;
    if (cW < 10 || cH < 10) return;

    const winMs = gauge.chartTimeWindowSec * 1000;
    const cutoff = now - winMs;

    const startIdx = bisect(buf, cutoff);
    const visibleCount = buf.length - startIdx;
    if (visibleCount < 2) {
      ctx.fillStyle = 'rgba(255,255,255,0.15)';
      ctx.font = '10px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.fillText('Waiting for data...', pL + cW / 2, pT + cH / 2);

      // Still render text overlay even with no data
      if (gauge.showName || gauge.showValue || gauge.showUnit) {
        const baseFs = Math.max(10, Math.min(16, w * 0.035));
        const valueFs = Math.round(baseFs * 1.6);
        const unitFs = Math.round(baseFs * 0.9);
        const nameFs = Math.round(baseFs * 0.8);

        const hasValue = gauge.showValue && gauge.formattedValue;
        const hasUnit = gauge.showUnit && gauge.unit;
        const hasName = gauge.showName && gauge.name;

        ctx.font = `bold ${valueFs}px monospace`;
        const valueW = hasValue ? ctx.measureText(gauge.formattedValue).width : 0;
        ctx.font = `${unitFs}px sans-serif`;
        const unitW = hasUnit ? ctx.measureText(gauge.unit).width : 0;
        ctx.font = `${nameFs}px sans-serif`;
        const nameW = hasName ? ctx.measureText(gauge.name).width : 0;

        const gap = 4;
        const totalW = valueW + (hasValue && hasUnit ? gap + unitW : 0);
        const pad = 6;
        const pillH = valueFs + 6;
        const pillW = Math.max(hasName ? nameW + pad * 2 : 0, totalW + pad * 2);
        if (pillW > 0) {
          ctx.fillStyle = 'rgba(0,0,0,0.55)';
          ctx.beginPath();
          ctx.roundRect(w - pillW - 4, 3, pillW, pillH, 4);
          ctx.fill();
        }

        let rx = w - pad - 4;
        const ty = 3 + pillH * 0.55;
        if (hasValue) {
          ctx.font = `bold ${valueFs}px monospace`;
          ctx.fillStyle = gauge.chartLineColor || '#22c55e';
          ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
          ctx.fillText(gauge.formattedValue, rx, ty);
          rx -= valueW;
        }
        if (hasUnit) {
          ctx.font = `${unitFs}px sans-serif`;
          ctx.fillStyle = 'rgba(255,255,255,0.6)';
          ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
          if (hasValue) rx -= gap;
          ctx.fillText(gauge.unit, rx, ty);
        }
        if (hasName) {
          ctx.font = `${nameFs}px sans-serif`;
          ctx.fillStyle = 'rgba(255,255,255,0.5)';
          ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
          ctx.fillText(gauge.name, pad + 2, ty);
        }
      }
      return;
    }

    // Y range from visible data only
    let dMin = Infinity, dMax = -Infinity;
    for (let i = startIdx; i < buf.length; i++) {
      if (buf[i].v < dMin) dMin = buf[i].v;
      if (buf[i].v > dMax) dMax = buf[i].v;
    }
    let yMin: number, yMax: number;
    if (gauge.chartYMin != null && gauge.chartYMax != null) {
      yMin = gauge.chartYMin; yMax = gauge.chartYMax;
    } else {
      if (dMin === Infinity) { dMin = 0; dMax = 100; }
      const m = (dMax - dMin) * 0.1 || 1;
      yMin = gauge.chartYMin ?? (dMin - m);
      yMax = gauge.chartYMax ?? (dMax + m);
    }
    const yR = yMax - yMin;
    if (yR <= 0) return;
    const toY = (v: number) => pT + cH - ((v - yMin) / yR) * cH;

    // Draw cached background
    drawBackground(cW, cH, pL, pT, yMin, yMax, yR);
    if (bgCanvas) ctx.drawImage(bgCanvas, 0, 0);

    // Collect points with precision filtering
    const prec = Math.max(0, gauge.chartPrecision);
    const factor = Math.pow(10, prec);
    const pts: number[] = [];
    let lastRounded: number | null = null;
    for (let i = startIdx; i < buf.length; i++) {
      const p = buf[i];
      const x = ((p.t - cutoff) / winMs) * cW + pL;
      const rounded = Math.round(p.v * factor) / factor;
      if (lastRounded === null || rounded !== lastRounded) {
        pts.push(x, p.v);
        lastRounded = rounded;
      }
    }

    if (pts.length < 4) return;

    // ── Color LUT ──
    const cs = gauge.colorStops;
    const luts = cs && cs.length > 0 ? buildColorLuts(cs, gauge.colorHysteresis ?? 0.03) : null;
    const vc = (v: number, pv: number) => {
      if (!luts) return gauge.chartLineColor;
      return gaugeValueColor(
        Math.max(0, Math.min(1, (v - yMin) / yR)),
        Math.max(0, Math.min(1, (pv - yMin) / yR)), luts);
    };

    // ── Fill ──
    if (gauge.chartFillUnder) {
      ctx.beginPath();
      ctx.moveTo(pts[0], pT + cH);
      for (let i = 0; i < pts.length; i += 2) ctx.lineTo(pts[i], toY(pts[i + 1]));
      ctx.lineTo(pts[pts.length - 2], pT + cH); ctx.closePath();
      const gr = ctx.createLinearGradient(0, pT, 0, pT + cH);
      const fc = vc(gauge.value, gauge.value);
      gr.addColorStop(0, fc.replace('rgb(', 'rgba(').replace(')', ',0.25)'));
      gr.addColorStop(1, fc.replace('rgb(', 'rgba(').replace(')', ',0.02)'));
      ctx.fillStyle = gr; ctx.fill();
    }

    // ── Line (batched by color) ──
    ctx.lineWidth = gauge.chartLineWidth;
    ctx.lineJoin = 'round'; ctx.lineCap = 'round';
    let cc = vc(pts[1], pts[1]);
    ctx.beginPath(); ctx.moveTo(pts[0], toY(pts[1]));
    for (let i = 2; i < pts.length; i += 2) {
      const pv = i >= 4 ? pts[i - 3] : pts[1];
      const c = vc(pts[i + 1], pv);
      if (c !== cc) {
        ctx.lineTo(pts[i], toY(pts[i + 1]));
        ctx.strokeStyle = cc; ctx.stroke();
        ctx.beginPath(); ctx.moveTo(pts[i], toY(pts[i + 1])); cc = c;
      } else {
        ctx.lineTo(pts[i], toY(pts[i + 1]));
      }
    }
    ctx.strokeStyle = cc; ctx.stroke();

    // ── Dot ──
    const lx = pts[pts.length - 2], ly = toY(pts[pts.length - 1]);
    ctx.beginPath(); ctx.arc(lx, ly, 3, 0, Math.PI * 2);
    ctx.fillStyle = vc(gauge.value, pts[pts.length - 1]); ctx.fill();
    ctx.strokeStyle = '#000'; ctx.lineWidth = 1; ctx.stroke();

    // ── Text overlay ──
    if (gauge.showName || gauge.showValue || gauge.showUnit) {
      const hasValue = gauge.showValue && gauge.formattedValue;
      const hasUnit = gauge.showUnit && gauge.unit;
      const hasName = gauge.showName && gauge.name;

      const baseFs = Math.max(10, Math.min(16, w * 0.035));
      const valueFs = Math.round(baseFs * 1.6);
      const unitFs = Math.round(baseFs * 0.9);
      const nameFs = Math.round(baseFs * 0.8);

      // Measure text widths for layout
      ctx.font = `bold ${valueFs}px monospace`;
      const valueW = hasValue ? ctx.measureText(gauge.formattedValue).width : 0;
      ctx.font = `${unitFs}px sans-serif`;
      const unitW = hasUnit ? ctx.measureText(gauge.unit).width : 0;
      ctx.font = `${nameFs}px sans-serif`;
      const nameW = hasName ? ctx.measureText(gauge.name).width : 0;

      const gap = 4;
      const totalW = valueW + (hasValue && hasUnit ? gap + unitW : 0);
      const pad = 6;

      // Background pill for readability
      const pillH = valueFs + 6;
      const pillW = Math.max(
        hasName ? nameW + pad * 2 : 0,
        totalW + pad * 2
      );
      if (pillW > 0) {
        const pillX = w - pillW - 4;
        const pillY = 3;
        ctx.fillStyle = 'rgba(0,0,0,0.55)';
        ctx.beginPath();
        const r = 4;
        ctx.roundRect(pillX, pillY, pillW, pillH, r);
        ctx.fill();
      }

      // Draw value + unit (right-aligned)
      let rx = w - pad - 4;
      const ty = 3 + pillH * 0.55;
      if (hasValue) {
        ctx.font = `bold ${valueFs}px monospace`;
        ctx.fillStyle = vc(gauge.value, gauge.value);
        ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
        ctx.fillText(gauge.formattedValue, rx, ty);
        rx -= valueW;
      }
      if (hasUnit) {
        ctx.font = `${unitFs}px sans-serif`;
        ctx.fillStyle = 'rgba(255,255,255,0.6)';
        ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
        if (hasValue) rx -= gap;
        ctx.fillText(gauge.unit, rx, ty);
      }

      // Draw name (left-aligned, below value row if both present, otherwise same row)
      if (hasName) {
        ctx.font = `${nameFs}px sans-serif`;
        ctx.fillStyle = 'rgba(255,255,255,0.5)';
        ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
        const nameY = (hasValue || hasUnit) ? 3 + pillH * 0.55 : 3 + pillH * 0.55;
        ctx.fillText(gauge.name, pad + 2, nameY);
      }
    }
  }

  let timer = 0;
  $effect(() => { timer = window.setInterval(tick, 100); return () => clearInterval(timer); });
</script>

<div class="size-full relative overflow-hidden rounded select-none" style="background: rgba(0,0,0,0.2);">
  <canvas
    bind:this={canvas}
    class="block"
    style="width: {pixelWidth}px; height: {pixelHeight}px;"
  ></canvas>
</div>
