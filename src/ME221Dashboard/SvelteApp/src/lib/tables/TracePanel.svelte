<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import type { OperatingPointSample } from '../stores/LiveDataStore.svelte';

  let { history, xAxisLabel = 'Time', yAxisLabel = 'RPM', xAxisLinkId = null, yAxisLinkId = null, width = 400, height = 250 }: {
    history: OperatingPointSample[];
    xAxisLabel?: string;
    yAxisLabel?: string;
    xAxisLinkId?: number | null;
    yAxisLinkId?: number | null;
    width?: number;
    height?: number;
  } = $props();

  let canvas: HTMLCanvasElement;
  let ctx: CanvasRenderingContext2D | null = null;
  let lastW = 0, lastH = 0;
  let rafId: number | null = null;
  let bgCanvas: OffscreenCanvas | null = null;
  let bgCtx: OffscreenCanvasRenderingContext2D | null = null;
  let bgKey = '';

  const TIME_WINDOW_SEC = 30;
  const PADDING = { left: 50, right: 16, top: 12, bottom: 28 };

  function getValue(sample: OperatingPointSample, linkId: number | null): number | null {
    if (linkId === null) return null;
    return sample.values[String(linkId)] ?? null;
  }

  function drawBackground(cW: number, cH: number, pL: number, pT: number, xMin: number, xMax: number, yMin: number, yMax: number, xIsTime: boolean) {
    const key = `${cW}|${cH}|${pL}|${pT}|${xMin.toFixed(1)}|${xMax.toFixed(1)}|${yMin.toFixed(1)}|${yMax.toFixed(1)}|${xIsTime}`;
    if (key === bgKey) return;
    bgKey = key;

    bgCanvas = new OffscreenCanvas(cW + pL + PADDING.right + 4, cH + pT + PADDING.bottom + 8);
    bgCtx = bgCanvas.getContext('2d') as OffscreenCanvasRenderingContext2D;
    if (!bgCtx) return;

    bgCtx.strokeStyle = 'rgba(255,255,255,0.07)';
    bgCtx.lineWidth = 1;
    for (let i = 0; i <= 5; i++) {
      const y = pT + (cH * i) / 5;
      bgCtx.beginPath(); bgCtx.moveTo(pL, y); bgCtx.lineTo(pL + cW, y); bgCtx.stroke();
    }
    for (let i = 0; i <= 5; i++) {
      const x = pL + (cW * i) / 5;
      bgCtx.beginPath(); bgCtx.moveTo(x, pT); bgCtx.lineTo(x, pT + cH); bgCtx.stroke();
    }

    bgCtx.fillStyle = 'rgba(255,255,255,0.35)';
    bgCtx.font = '9px monospace';
    bgCtx.textAlign = 'right';
    bgCtx.textBaseline = 'middle';
    const yRange = yMax - yMin;
    for (let i = 0; i <= 5; i++) {
      const y = pT + (cH * i) / 5;
      const v = yMax - (yRange * i) / 5;
      bgCtx.fillText(Math.abs(v) >= 1000 ? v.toFixed(0) : v.toFixed(1), pL - 4, y);
    }

    bgCtx.fillStyle = 'rgba(255,255,255,0.3)';
    bgCtx.textAlign = 'center';
    bgCtx.textBaseline = 'top';
    const xRange = xMax - xMin;
    for (let i = 0; i <= 5; i++) {
      const x = pL + (cW * i) / 5;
      const v = xMin + (xRange * i) / 5;
      if (xIsTime) {
        const secAgo = ((xMax - v) / 1000).toFixed(0);
        bgCtx.fillText(secAgo === '0' ? 'now' : `-${secAgo}s`, x, pT + cH + 4);
      } else {
        bgCtx.fillText(Math.abs(v) >= 1000 ? v.toFixed(0) : v.toFixed(1), x, pT + cH + 4);
      }
    }

    bgCtx.fillStyle = 'rgba(255,255,255,0.25)';
    bgCtx.font = '9px sans-serif';
    bgCtx.textAlign = 'center';
    bgCtx.textBaseline = 'top';
    bgCtx.fillText(xAxisLabel, pL + cW / 2, pT + cH + 16);
    bgCtx.save();
    bgCtx.translate(10, pT + cH / 2);
    bgCtx.rotate(-Math.PI / 2);
    bgCtx.textAlign = 'center';
    bgCtx.textBaseline = 'top';
    bgCtx.fillText(yAxisLabel, 0, 0);
    bgCtx.restore();
  }

  function tick() {
    if (!canvas || !history) return;
    if (!ctx) ctx = canvas.getContext('2d');
    if (!ctx) return;

    const dpr = window.devicePixelRatio || 1;
    const w = width, h = height;
    if (w !== lastW || h !== lastH) {
      canvas.width = w * dpr;
      canvas.height = h * dpr;
      canvas.style.width = w + 'px';
      canvas.style.height = h + 'px';
      lastW = w; lastH = h;
      bgKey = '';
    }
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, h);

    const pL = PADDING.left, pR = PADDING.right, pT = PADDING.top, pB = PADDING.bottom;
    const cW = w - pL - pR, cH = h - pT - pB;
    if (cW < 20 || cH < 20) return;

    if (!history || history.length < 2) {
      ctx.fillStyle = 'rgba(255,255,255,0.15)';
      ctx.font = '10px sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText('No data', pL + cW / 2, pT + cH / 2);
      return;
    }

    const now = performance.now();
    const winMs = TIME_WINDOW_SEC * 1000;
    const cutoff = now - winMs;
    const xIsTime = xAxisLinkId === null;

    const visible: { t: number; x: number; y: number }[] = [];
    for (let i = 0; i < history.length; i++) {
      const s = history[i];
      if (s.timestamp < cutoff) continue;
      const xVal = xIsTime ? s.timestamp : getValue(s, xAxisLinkId);
      const yVal = getValue(s, yAxisLinkId);
      if (xVal !== null && yVal !== null) {
        visible.push({ t: s.timestamp, x: xVal, y: yVal });
      }
    }

    if (visible.length < 2) {
      ctx.fillStyle = 'rgba(255,255,255,0.15)';
      ctx.font = '10px sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText('No data', pL + cW / 2, pT + cH / 2);
      return;
    }

    let xMin = Infinity, xMax = -Infinity, yMin = Infinity, yMax = -Infinity;
    for (const p of visible) {
      if (p.x < xMin) xMin = p.x;
      if (p.x > xMax) xMax = p.x;
      if (p.y < yMin) yMin = p.y;
      if (p.y > yMax) yMax = p.y;
    }

    if (xMin === xMax) { xMin -= 1; xMax += 1; }
    if (yMin === yMax) { yMin -= 1; yMax += 1; }

    const xPad = (xMax - xMin) * 0.08 || 1;
    const yPad = (yMax - yMin) * 0.08 || 1;
    xMin -= xPad; xMax += xPad;
    yMin -= yPad; yMax += yPad;

    const xRange = xMax - xMin;
    const yRange = yMax - yMin;
    if (xRange <= 0 || yRange <= 0) return;

    const toX = (v: number) => pL + ((v - xMin) / xRange) * cW;
    const toY = (v: number) => pT + cH - ((v - yMin) / yRange) * cH;

    drawBackground(cW, cH, pL, pT, xMin, xMax, yMin, yMax, xIsTime);
    if (bgCanvas) ctx.drawImage(bgCanvas, 0, 0);

    ctx.lineWidth = 2;
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';

    const total = visible.length;
    for (let i = 1; i < total; i++) {
      const prev = visible[i - 1];
      const curr = visible[i];
      const t = total > 1 ? i / (total - 1) : 0;
      const hue = 120 * (1 - t);
      ctx.strokeStyle = `hsl(${hue}, 80%, 55%)`;
      ctx.beginPath();
      ctx.moveTo(toX(prev.x), toY(prev.y));
      ctx.lineTo(toX(curr.x), toY(curr.y));
      ctx.stroke();
    }

    if (total > 0) {
      const last = visible[total - 1];
      ctx.beginPath();
      ctx.arc(toX(last.x), toY(last.y), 4, 0, Math.PI * 2);
      ctx.fillStyle = '#ef4444';
      ctx.fill();
      ctx.strokeStyle = '#000';
      ctx.lineWidth = 1.5;
      ctx.stroke();
    }
  }

  onMount(() => {
    const loop = () => {
      tick();
      rafId = requestAnimationFrame(loop);
    };
    rafId = requestAnimationFrame(loop);
  });

  onDestroy(() => {
    if (rafId !== null) {
      cancelAnimationFrame(rafId);
      rafId = null;
    }
  });
</script>

<div class="rounded border" style="background: rgba(0,0,0,0.25); border-color: var(--metro-border-subtle, rgba(255,255,255,0.08));">
  <canvas
    bind:this={canvas}
    class="block"
    style="width: {width}px; height: {height}px;"
  ></canvas>
</div>
