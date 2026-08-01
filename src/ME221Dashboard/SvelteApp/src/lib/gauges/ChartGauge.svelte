<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { buildColorLuts, gaugeValueColor } from './types';
  import { lineDashFor, type ChartSample } from './chartDataUtils';

  let { gauge, pixelWidth, pixelHeight, overlayHistories = {} }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
    overlayHistories?: Record<string, ChartSample[]>;
  } = $props();

  const MAX_POINTS = 6000;
  interface Pt { t: number; v: number }
  const buffers = new Map<number, Pt[]>();
  function getBuf(eid: number): Pt[] {
    let b = buffers.get(eid);
    if (!b) { b = []; buffers.set(eid, b); }
    return b;
  }
  function bisect(buf: Pt[] | ChartSample[], cutoff: number): number {
    let lo = 0, hi = buf.length;
    while (lo < hi) { const m = (lo + hi) >> 1; buf[m].t < cutoff ? lo = m + 1 : hi = m; }
    return lo;
  }

  // Reused scratch (no per-frame allocation): visible start index per overlay
  let ovStarts: number[] = [];

  let canvas: HTMLCanvasElement;
  let ctx: CanvasRenderingContext2D | null = null;
  let lastW = 0, lastH = 0;

  // ── Offscreen cache for static elements (background, grid, labels) ──
  let bgCanvas: OffscreenCanvas | null = null;
  let bgCtx: OffscreenCanvasRenderingContext2D | null = null;
  let bgKey = '';

  function drawBackground(cW: number, cH: number, pL: number, pT: number, yMin: number, yMax: number, yR: number) {
    const bg = gauge.chartBackgroundColor ?? '';
    const key = `${cW}|${cH}|${pL}|${yMin}|${yMax}|${bg}`;
    if (key === bgKey) return;
    bgKey = key;

    bgCanvas = new OffscreenCanvas(cW + pL + 8, cH + pT + 16);
    bgCtx = bgCanvas.getContext('2d') as OffscreenCanvasRenderingContext2D;
    if (!bgCtx) return;

    // Background color behind the grid ('' = transparent)
    if (bg) {
      bgCtx.fillStyle = bg;
      bgCtx.fillRect(0, 0, cW + pL + 8, cH + pT + 16);
    }

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

  // ── Readout pill (R23): 4-corner placement + font scale ──
  function drawReadout(pillValueColor: (v: number, pv: number) => string) {
    const c = ctx as CanvasRenderingContext2D;
    const w = pixelWidth, h = pixelHeight;
    const hasValue = gauge.showValue && gauge.formattedValue;
    const hasUnit = gauge.showUnit && gauge.unit;
    const hasName = gauge.showName && gauge.name;

    const fs = gauge.overlayFontScale || 1;
    const baseFs = Math.max(10, Math.min(16, w * 0.035)) * fs;
    const valueFs = Math.round(baseFs * 1.6);
    const unitFs = Math.round(baseFs * 0.9);
    const nameFs = Math.round(baseFs * 0.8);

    // Measure text widths for layout
    c.font = `bold ${valueFs}px 'Orbitron Variable', sans-serif`;
    const valueW = hasValue ? c.measureText(gauge.formattedValue).width : 0;
    c.font = `${unitFs}px sans-serif`;
    const unitW = hasUnit ? c.measureText(gauge.unit).width : 0;
    c.font = `${nameFs}px sans-serif`;
    const nameW = hasName ? c.measureText(gauge.name).width : 0;

    const gap = 4;
    const totalW = valueW + (hasValue && hasUnit ? gap + unitW : 0);
    const pad = 6;
    const margin = 4;

    // Background pill for readability
    const pillH = valueFs + 6;
    const pillW = Math.max(
      hasName ? nameW + pad * 2 : 0,
      totalW + pad * 2
    );

    const pos = gauge.overlayPillPosition || 0;
    let pillX: number, pillY: number, rx: number, nameX: number;
    if (pos === 1) { pillX = margin; pillY = 3; rx = pillX + pillW - pad; nameX = pillX + pad + 2; }
    else if (pos === 2) { pillX = w - pillW - margin; pillY = h - pillH - 3; rx = w - pad - margin; nameX = pad + 2; }
    else if (pos === 3) { pillX = margin; pillY = h - pillH - 3; rx = pillX + pillW - pad; nameX = pillX + pad + 2; }
    else { pillX = w - pillW - margin; pillY = 3; rx = w - pad - margin; nameX = pad + 2; }

    if (pillW > 0) {
      c.fillStyle = 'rgba(0,0,0,0.55)';
      c.beginPath();
      const r = 4;
      c.roundRect(pillX, pillY, pillW, pillH, r);
      c.fill();
    }

    // Draw value + unit (right-aligned)
    const ty = pillY + pillH * 0.55;
    if (hasValue) {
      c.font = `bold ${valueFs}px 'Orbitron Variable', sans-serif`;
      c.fillStyle = pillValueColor(gauge.value, gauge.value);
      c.textAlign = 'right'; c.textBaseline = 'middle';
      c.fillText(gauge.formattedValue, rx, ty);
      rx -= valueW;
    }
    if (hasUnit) {
      c.font = `${unitFs}px sans-serif`;
      c.fillStyle = 'rgba(255,255,255,0.6)';
      c.textAlign = 'right'; c.textBaseline = 'middle';
      if (hasValue) rx -= gap;
      c.fillText(gauge.unit, rx, ty);
    }

    // Draw name (left-aligned)
    if (hasName) {
      c.font = `${nameFs}px sans-serif`;
      c.fillStyle = 'rgba(255,255,255,0.5)';
      c.textAlign = 'left'; c.textBaseline = 'middle';
      c.fillText(gauge.name, nameX, ty);
    }
  }

  // ── Overlay lines (R22): batched by (color, width, style) runs ──
  function drawOverlays(cW: number, pL: number, winMs: number, cutoff: number, toY: (v: number) => number, factor: number) {
    const c = ctx as CanvasRenderingContext2D;
    const ols = gauge.chartOverlays;
    c.lineJoin = 'round'; c.lineCap = 'round';
    let i = 0;
    while (i < ols.length) {
      if (ovStarts[i] < 0) { i++; continue; }
      const ov = ols[i];
      const color = ov.color;
      const width = ov.lineWidth;
      const style = ov.lineStyle;
      let any = false;
      c.beginPath();
      while (i < ols.length) {
        const o2 = ols[i];
        if (o2.color !== color || o2.lineWidth !== width || o2.lineStyle !== style) break;
        const s2 = ovStarts[i];
        if (s2 < 0) { i++; continue; }
        const b2 = overlayHistories[o2.entityId];
        if (!b2) { i++; continue; }
        let first = true;
        let lastRounded: number | null = null;
        for (let k = s2; k < b2.length; k++) {
          const p = b2[k];
          const rounded = Math.round(p.v * factor) / factor;
          if (first || rounded !== lastRounded) {
            const x = ((p.t - cutoff) / winMs) * cW + pL;
            const y = toY(p.v);
            if (first) c.moveTo(x, y); else c.lineTo(x, y);
            lastRounded = rounded;
            first = false;
          }
        }
        if (!first) any = true;
        i++;
      }
      if (any) {
        c.strokeStyle = color;
        c.lineWidth = width;
        c.setLineDash(lineDashFor(style));
        c.stroke();
      }
    }
    c.setLineDash([]);
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

    // Overlay prepass: visible start index per overlay (-1 = no data)
    const ols = gauge.chartOverlays;
    let anyOverlayData = false;
    ovStarts.length = 0;
    for (let i = 0; i < ols.length; i++) {
      const ob = overlayHistories[ols[i].entityId];
      if (!ob) { ovStarts[i] = -1; continue; }
      const s = bisect(ob, cutoff);
      ovStarts[i] = s;
      if (ob.length - s >= 2) anyOverlayData = true;
    }

    if (visibleCount < 2 && !anyOverlayData) {
      ctx.fillStyle = 'rgba(255,255,255,0.15)';
      ctx.font = '10px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.fillText('Waiting for data...', pL + cW / 2, pT + cH / 2);

      // Still render text overlay even with no data
      if (gauge.showName || gauge.showValue || gauge.showUnit) {
        drawReadout(() => gauge.chartLineColor || '#22c55e');
      }
      return;
    }

    // Y range from visible data: primary + all overlay series (R22)
    let dMin = Infinity, dMax = -Infinity;
    for (let i = startIdx; i < buf.length; i++) {
      if (buf[i].v < dMin) dMin = buf[i].v;
      if (buf[i].v > dMax) dMax = buf[i].v;
    }
    for (let i = 0; i < ols.length; i++) {
      const s = ovStarts[i];
      if (s < 0) continue;
      const ob = overlayHistories[ols[i].entityId];
      for (let k = s; k < ob.length; k++) {
        if (ob[k].v < dMin) dMin = ob[k].v;
        if (ob[k].v > dMax) dMax = ob[k].v;
      }
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
    if (pts.length >= 4 && gauge.chartFillUnder) {
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

    // ── Overlay lines ──
    if (anyOverlayData) drawOverlays(cW, pL, winMs, cutoff, toY, factor);

    if (pts.length < 4) return;

    // ── Line (batched by color) — primary line style (R24) ──
    ctx.lineWidth = gauge.chartLineWidth;
    ctx.lineJoin = 'round'; ctx.lineCap = 'round';
    ctx.setLineDash(lineDashFor(gauge.chartLineStyle));
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
    ctx.setLineDash([]);

    // ── Dot ──
    const lx = pts[pts.length - 2], ly = toY(pts[pts.length - 1]);
    ctx.beginPath(); ctx.arc(lx, ly, 3, 0, Math.PI * 2);
    ctx.fillStyle = vc(gauge.value, pts[pts.length - 1]); ctx.fill();
    ctx.strokeStyle = '#000'; ctx.lineWidth = 1; ctx.stroke();

    // ── Text overlay ──
    if (gauge.showName || gauge.showValue || gauge.showUnit) {
      drawReadout(vc);
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
