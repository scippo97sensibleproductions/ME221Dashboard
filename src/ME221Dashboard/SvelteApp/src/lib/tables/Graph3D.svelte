<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import * as THREE from 'three';
  import type { TableDefinition, TableData, ColorScheme } from './types';
  import { getOutputValue, heatColor, getDataRange, findNearestIndex, fromRaw, findInterpolationRange } from './types';
  import { IconRotate2, IconEye, IconGridDots, IconDownload, IconPaint, IconKeyboard, IconChevronRight, IconChevronDown, IconArrowUp, IconArrowDown, IconPlus, IconMinus, IconWaveSine } from '@tabler/icons-svelte';
  import { HybridBridge } from '../HybridBridge';

  let {
    tableDef,
    tableData,
    colorScheme = 'thermal',
    showContours = false,
    opRow = -1,
    opCol = -1,
    operatingPointHistory = [],
    onEditCell,
    onBatchEdit,
    onUndo,
    onRedo,
    onSelectionChange,
  }: {
    tableDef: TableDefinition;
    tableData: TableData;
    colorScheme?: ColorScheme;
    showContours?: boolean;
    opRow?: number;
    opCol?: number;
    operatingPointHistory?: Array<{ timestamp: number; values: Record<string, number | null> }>;
    onEditCell: (row: number, col: number, newVal: number) => void;
    onBatchEdit?: (edits: Array<{ row: number; col: number; newVal: number }>) => void;
    onUndo?: () => void;
    onRedo?: () => void;
    onSelectionChange?: (row: number, col: number) => void;
  } = $props();

  let containerEl: HTMLDivElement;
  let renderer: THREE.WebGLRenderer;
  let scene: THREE.Scene;
  let camera: THREE.PerspectiveCamera;
  let animFrameId: number;
  let mounted = false;

  // State
  let selRow = $state(0);
  let selCol = $state(0);
  let smoothRadius = $state(2);
  let increment = $state(1);
  let showWireframe = $state(true);
  let showSurface = $state(true);
  let showContoursLocal = $state(false);
  let autoRotate = $state(false);
  let brushMode = $state(false);
  let brushSelection = $state<Set<string>>(new Set());
  let hoveredVertex = $state<{ row: number; col: number; value: number } | null>(null);
  let showHelp = $state(false);
  let viewMode = $state<'perspective' | 'front' | 'back' | 'left' | 'right' | 'top' | 'bottom' | 'iso'>('perspective');

  // Three.js objects
  let surfaceMesh: THREE.Mesh;
  let wireframeMesh: THREE.LineSegments;
  let contourLines: THREE.LineSegments;
  let selectionMarker: THREE.Mesh;
  let smoothRadiusRing: THREE.Line;
  let opMarker: THREE.Mesh;
  let opTrailLine: THREE.Line;
  let gridHelper: THREE.GridHelper;
  let axisGroup: THREE.Group;
  let brushMarkers: THREE.InstancedMesh;
  let hoverMarker: THREE.Mesh;

  // ViewCube
  let viewCubeEl: HTMLDivElement;
  let vcRenderer: THREE.WebGLRenderer;
  let vcScene: THREE.Scene;
  let vcCamera: THREE.OrthographicCamera;
  let vcCube: THREE.Group;
  let vcRaycaster = new THREE.Raycaster();
  let vcMouse = new THREE.Vector2();

  // Orbit state
  let isDragging = false;
  let isPanning = false;
  let lastMouse = { x: 0, y: 0 };
  let spherical = { radius: 8, theta: Math.PI / 4, phi: Math.PI / 3 };
  let panOffset = { x: 0, y: 0, z: 0 };
  let pivotPoint = new THREE.Vector3(0, 0, 0);

  const { min: zMin, max: zMax } = $derived(getDataRange(tableData.output));

  function getColor(val: number): THREE.Color {
    const css = heatColor(val, zMin, zMax, colorScheme);
    const m = css.match(/rgb\((\d+),\s*(\d+),\s*(\d+)\)/);
    if (m) return new THREE.Color(parseInt(m[1]) / 255, parseInt(m[2]) / 255, parseInt(m[3]) / 255);
    return new THREE.Color(0.5, 0.5, 0.5);
  }

  function buildSurface() {
    const rows = tableDef.rows;
    const cols = tableDef.cols;
    if (rows === 0 || cols === 0) return;
    const scaleX = cols > 1 ? 10 / (cols - 1) : 10;
    const scaleZ = rows > 1 ? 10 / (rows - 1) : 10;
    const range = zMax - zMin;
    const heightScale = range > 0 ? 5 / range : 0;
    const offsetX = -5;
    const offsetZ = -5;

    // Vertices: rows * cols positions
    const positions = new Float32Array(rows * cols * 3);
    const colors = new Float32Array(rows * cols * 3);

    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const idx = r * cols + c;
        const val = getOutputValue(tableData, r, c, cols);
        const x = offsetX + c * scaleX;
        const y = Number.isFinite(val) ? (val - zMin) * heightScale : 0;
        const z = offsetZ + r * scaleZ;

        positions[idx * 3] = x;
        positions[idx * 3 + 1] = y;
        positions[idx * 3 + 2] = z;

        const col3 = getColor(val);
        colors[idx * 3] = col3.r;
        colors[idx * 3 + 1] = col3.g;
        colors[idx * 3 + 2] = col3.b;
      }
    }

    // Triangles: (rows-1) * (cols-1) * 2 triangles * 3 indices
    const indices: number[] = [];
    for (let r = 0; r < rows - 1; r++) {
      for (let c = 0; c < cols - 1; c++) {
        const tl = r * cols + c;
        const tr = r * cols + c + 1;
        const bl = (r + 1) * cols + c;
        const br = (r + 1) * cols + c + 1;
        indices.push(tl, bl, tr);
        indices.push(tr, bl, br);
      }
    }

    const geom = new THREE.BufferGeometry();
    geom.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geom.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geom.setIndex(indices);
    geom.computeVertexNormals();

    if (surfaceMesh) {
      surfaceMesh.geometry.dispose();
      surfaceMesh.geometry = geom;
    } else {
      const mat = new THREE.MeshPhongMaterial({
        vertexColors: true,
        side: THREE.DoubleSide,
        flatShading: false,
        shininess: 30,
      });
      surfaceMesh = new THREE.Mesh(geom, mat);
      surfaceMesh.frustumCulled = false;
      scene.add(surfaceMesh);
    }

    // Wireframe
    const wireGeom = new THREE.BufferGeometry();
    wireGeom.setAttribute('position', new THREE.BufferAttribute(positions.slice(), 3));
    wireGeom.setIndex(indices);
    if (wireframeMesh) {
      wireframeMesh.geometry.dispose();
      wireframeMesh.geometry = wireGeom;
    } else {
      const wireMat = new THREE.LineBasicMaterial({ color: 0xffffff, opacity: 0.15, transparent: true });
      wireframeMesh = new THREE.LineSegments(wireGeom, wireMat);
      wireframeMesh.frustumCulled = false;
      scene.add(wireframeMesh);
    }
    wireframeMesh.visible = showWireframe;

    // Contour lines
    buildContours(positions, rows, cols, offsetX, offsetZ, scaleX, scaleZ, heightScale);

    return { positions, rows, cols, offsetX, offsetZ, scaleX, scaleZ, heightScale };
  }

  function buildContours(positions: Float32Array, rows: number, cols: number, offsetX: number, offsetZ: number, scaleX: number, scaleZ: number, heightScale: number) {
    if (zMax === zMin) {
      if (contourLines) contourLines.visible = false;
      return;
    }
    const contourPositions: number[] = [];
    const numContours = 12;
    const step = (zMax - zMin) / numContours;

    for (let level = 0; level <= numContours; level++) {
      const threshold = zMin + level * step;
      const y = (threshold - zMin) * heightScale;

      for (let r = 0; r < rows - 1; r++) {
        for (let c = 0; c < cols - 1; c++) {
          const v00 = getOutputValue(tableData, r, c, cols);
          const v10 = getOutputValue(tableData, r, c + 1, cols);
          const v01 = getOutputValue(tableData, r + 1, c, cols);
          const v11 = getOutputValue(tableData, r + 1, c + 1, cols);

          const edges: THREE.Vector3[] = [];

          if ((v00 <= threshold) !== (v10 <= threshold)) {
            const denom = v10 - v00;
            const t = denom !== 0 ? (threshold - v00) / denom : 0.5;
            edges.push(new THREE.Vector3(
              offsetX + (c + t) * scaleX,
              y,
              offsetZ + r * scaleZ
            ));
          }
          if ((v10 <= threshold) !== (v11 <= threshold)) {
            const denom = v11 - v10;
            const t = denom !== 0 ? (threshold - v10) / denom : 0.5;
            edges.push(new THREE.Vector3(
              offsetX + (c + 1) * scaleX,
              y,
              offsetZ + (r + t) * scaleZ
            ));
          }
          if ((v01 <= threshold) !== (v11 <= threshold)) {
            const denom = v11 - v01;
            const t = denom !== 0 ? (threshold - v01) / denom : 0.5;
            edges.push(new THREE.Vector3(
              offsetX + (c + t) * scaleX,
              y,
              offsetZ + (r + 1) * scaleZ
            ));
          }
          if ((v00 <= threshold) !== (v01 <= threshold)) {
            const denom = v01 - v00;
            const t = denom !== 0 ? (threshold - v00) / denom : 0.5;
            edges.push(new THREE.Vector3(
              offsetX + c * scaleX,
              y,
              offsetZ + (r + t) * scaleZ
            ));
          }

          if (edges.length >= 2) {
            contourPositions.push(edges[0].x, edges[0].y, edges[0].z);
            contourPositions.push(edges[1].x, edges[1].y, edges[1].z);
          }
        }
      }
    }

    if (contourPositions.length === 0) {
      if (contourLines) contourLines.visible = false;
      return;
    }
    const cGeom = new THREE.BufferGeometry();
    cGeom.setAttribute('position', new THREE.Float32BufferAttribute(contourPositions, 3));
    if (contourLines) {
      contourLines.geometry.dispose();
      contourLines.geometry = cGeom;
    } else {
      const cMat = new THREE.LineBasicMaterial({ color: 0xffffff, opacity: 0.3, transparent: true });
      contourLines = new THREE.LineSegments(cGeom, cMat);
      contourLines.frustumCulled = false;
      scene.add(contourLines);
    }
    contourLines.visible = showContoursLocal;
  }

  function buildAxisLabels() {
    if (axisGroup) {
      axisGroup.children.forEach(c => {
        if ((c as any).geometry) (c as any).geometry.dispose();
        if ((c as any).material) {
          if ((c as any).material.map) (c as any).material.map.dispose();
          (c as any).material.dispose();
        }
      });
      scene.remove(axisGroup);
    }
    axisGroup = new THREE.Group();
    axisGroup.frustumCulled = false;

    const rows = tableDef.rows;
    const cols = tableDef.cols;
    if (rows === 0 || cols === 0) { scene.add(axisGroup); return; }

    const scaleX = cols > 1 ? 10 / (cols - 1) : 10;
    const scaleZ = rows > 1 ? 10 / (rows - 1) : 10;
    const offsetX = -5;
    const offsetZ = -5;
    const range = zMax - zMin;
    const heightScale = range > 0 ? 5 / range : 0;

    function makeTextSprite(text: string, fontSize = 10, color = '#aaa'): THREE.Sprite {
      const canvas = document.createElement('canvas');
      const dpr = 2;
      const measure = text.length;
      canvas.width = Math.max(32, measure * fontSize * dpr * 0.65);
      canvas.height = fontSize * dpr * 1.6;
      const ctx = canvas.getContext('2d')!;
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.font = `bold ${fontSize * dpr}px monospace`;
      ctx.fillStyle = color;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(text, canvas.width / 2, canvas.height / 2);
      const texture = new THREE.CanvasTexture(canvas);
      texture.minFilter = THREE.LinearFilter;
      const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthTest: false });
      const sprite = new THREE.Sprite(material);
      const aspect = canvas.width / canvas.height;
      sprite.scale.set(aspect * 0.4, 0.4, 1);
      return sprite;
    }

    function formatVal(v: number): string {
      if (Math.abs(v) >= 1000) return v.toFixed(0);
      if (Math.abs(v) >= 100) return v.toFixed(0);
      if (Math.abs(v) >= 10) return v.toFixed(1);
      return v.toFixed(2);
    }

    const input0 = tableData.input0;
    const input1 = tableData.input1;

    const maxXTicks = Math.min(cols, 8);
    const maxZTicks = Math.min(rows, 8);
    const xStep = Math.max(1, Math.floor(cols / maxXTicks));
    const zStep = Math.max(1, Math.floor(rows / maxZTicks));

    for (let c = 0; c < cols; c += xStep) {
      const x = offsetX + c * scaleX;
      const label = input0.length > c ? formatVal(input0[c]) : c.toString();
      const sprite = makeTextSprite(label, 9, '#888');
      sprite.position.set(x, -0.6, offsetZ + (rows - 1) * scaleZ + 0.8);
      axisGroup.add(sprite);
    }

    for (let r = 0; r < rows; r += zStep) {
      const z = offsetZ + r * scaleZ;
      const label = input1.length > r ? formatVal(input1[r]) : r.toString();
      const sprite = makeTextSprite(label, 9, '#888');
      sprite.position.set(offsetX - 0.8, -0.6, z);
      axisGroup.add(sprite);
    }

    const ySteps = 5;
    for (let i = 0; i <= ySteps; i++) {
      const t = i / ySteps;
      const val = zMin + t * range;
      const y = t * 5;
      const label = formatVal(val);
      const sprite = makeTextSprite(label, 9, '#888');
      sprite.position.set(offsetX - 0.8, y, offsetZ - 0.8);
      axisGroup.add(sprite);
    }

    const xName = makeTextSprite(tableDef.input0Name || 'X', 11, '#ff8800');
    xName.position.set(0, -1.2, offsetZ + (rows - 1) * scaleZ + 1.5);
    axisGroup.add(xName);

    const zName = makeTextSprite(tableDef.input1Name || 'Z', 11, '#ff8800');
    zName.position.set(offsetX - 1.8, -1.2, (offsetZ + offsetZ + (rows - 1) * scaleZ) / 2);
    axisGroup.add(zName);

    const yName = makeTextSprite(tableDef.outputName || 'Output', 11, '#ff8800');
    yName.position.set(offsetX - 1.8, 2.5, offsetZ - 0.8);
    yName.rotation.z = Math.PI / 2;
    axisGroup.add(yName);

    const edgeMat = new THREE.LineBasicMaterial({ color: 0x555555, transparent: true, opacity: 0.5 });
    const edgeVerts: number[] = [];
    edgeVerts.push(offsetX, 0, offsetZ, offsetX + (cols - 1) * scaleX, 0, offsetZ);
    edgeVerts.push(offsetX, 0, offsetZ, offsetX, 0, offsetZ + (rows - 1) * scaleZ);
    edgeVerts.push(offsetX, 0, offsetZ, offsetX, 5, offsetZ);
    const edgeGeom = new THREE.BufferGeometry();
    edgeGeom.setAttribute('position', new THREE.Float32BufferAttribute(edgeVerts, 3));
    const edges = new THREE.LineSegments(edgeGeom, edgeMat);
    edges.frustumCulled = false;
    axisGroup.add(edges);

    scene.add(axisGroup);
  }

  function updateVertexPosition(row: number, col: number, newVal: number) {
    if (!surfaceMesh || !Number.isFinite(newVal)) return;
    const posAttr = surfaceMesh.geometry.getAttribute('position') as THREE.BufferAttribute;
    const idx = row * tableDef.cols + col;
    const rows = tableDef.rows;
    const cols = tableDef.cols;
    const scaleX = cols > 1 ? 10 / (cols - 1) : 10;
    const scaleZ = rows > 1 ? 10 / (rows - 1) : 10;
    const range = zMax - zMin;
    const heightScale = range > 0 ? 5 / range : 0;

    posAttr.setY(idx, (newVal - zMin) * heightScale);
    posAttr.needsUpdate = true;
    surfaceMesh.geometry.computeVertexNormals();

    // Update color
    const colAttr = surfaceMesh.geometry.getAttribute('color') as THREE.BufferAttribute;
    const c = getColor(newVal);
    colAttr.setXYZ(idx, c.r, c.g, c.b);
    colAttr.needsUpdate = true;

    // Update wireframe
    if (wireframeMesh) {
      const wPosAttr = wireframeMesh.geometry.getAttribute('position') as THREE.BufferAttribute;
      wPosAttr.setY(idx, (newVal - zMin) * heightScale);
      wPosAttr.needsUpdate = true;
    }
  }

  function rebuildAll() {
    buildSurface();
    buildAxisLabels();
    updateSelectionMarker();
    updateSmoothRadiusRing();
    updateOpMarker();
    updateOpTrail();
    updateBrushMarkers();
  }

  function updateSelectionMarker() {
    if (!selectionMarker || !surfaceMesh) return;
    const posAttr = surfaceMesh.geometry.getAttribute('position') as THREE.BufferAttribute;
    const idx = selRow * tableDef.cols + selCol;
    const x = posAttr.getX(idx);
    const y = posAttr.getY(idx);
    const z = posAttr.getZ(idx);
    selectionMarker.position.set(x, y + 0.15, z);
    selectionMarker.visible = true;
  }

  const RING_MAX = 49;
  const TRAIL_MAX = 60;
  let ringPointCount = 0;
  let trailPointCount = 0;

  function updateSmoothRadiusRing() {
    if (!smoothRadiusRing || !surfaceMesh) return;
    const posAttr = smoothRadiusRing.geometry.getAttribute('position') as THREE.BufferAttribute;
    const cols = tableDef.cols;
    const rows = tableDef.rows;
    const scaleX = cols > 1 ? 10 / (cols - 1) : 10;
    const scaleZ = rows > 1 ? 10 / (rows - 1) : 10;
    const offsetX = -5;
    const offsetZ = -5;

    const cx = offsetX + selCol * scaleX;
    const cz = offsetZ + selRow * scaleZ;
    const radiusWorld = smoothRadius * Math.max(scaleX, scaleZ);
    const segments = 48;
    const count = segments + 1;
    for (let i = 0; i < count; i++) {
      const angle = (i / segments) * Math.PI * 2;
      posAttr.setXYZ(i, cx + Math.cos(angle) * radiusWorld, 0.05, cz + Math.sin(angle) * radiusWorld);
    }
    ringPointCount = count;
    posAttr.needsUpdate = true;
    smoothRadiusRing.geometry.setDrawRange(0, ringPointCount);
    smoothRadiusRing.visible = smoothRadius > 0;
  }

  function updateOpMarker() {
    if (!opMarker || !surfaceMesh) return;
    if (opRow < 0 || opCol < 0) { opMarker.visible = false; return; }
    const posAttr = surfaceMesh.geometry.getAttribute('position') as THREE.BufferAttribute;
    const idx = opRow * tableDef.cols + opCol;
    opMarker.position.set(posAttr.getX(idx), posAttr.getY(idx) + 0.2, posAttr.getZ(idx));
    opMarker.visible = true;
  }

  function updateOpTrail() {
    if (!opTrailLine || !surfaceMesh) return;
    if (operatingPointHistory.length < 2) { opTrailLine.visible = false; return; }

    const cols = tableDef.cols;
    const rows = tableDef.rows;
    const posAttr = opTrailLine.geometry.getAttribute('position') as THREE.BufferAttribute;

    const maxTrail = Math.min(operatingPointHistory.length, TRAIL_MAX);
    const startIdx = operatingPointHistory.length - maxTrail;
    let count = 0;

    for (let i = startIdx; i < operatingPointHistory.length; i++) {
      const sample = operatingPointHistory[i];
      if (!sample || !sample.values) continue;
      // Extract x/y from entity values using table's input link IDs
      const raw0 = tableDef.input0LinkId ? sample.values[String(tableDef.input0LinkId)] : null;
      if (raw0 == null) continue;
      const x = fromRaw(raw0, tableDef.input0UnitType ?? 0);
      const colRange = findInterpolationRange(x, tableData.input0);
      let colFrac = colRange.lower + (colRange.lower === colRange.upper ? 0.5 : colRange.fraction);
      colFrac = Math.max(0, Math.min(cols - 1, colFrac));
      let rowFrac = 0.5;
      if (tableDef.input1LinkId && tableDef.input1LinkId !== 0) {
        const raw1 = sample.values[String(tableDef.input1LinkId)];
        if (raw1 == null) continue;
        const y = fromRaw(raw1, tableDef.input1UnitType ?? 0);
        const rowRange = findInterpolationRange(y, tableData.input1);
        rowFrac = rowRange.lower + (rowRange.lower === rowRange.upper ? 0.5 : rowRange.fraction);
        rowFrac = Math.max(0, Math.min(rows - 1, rowFrac));
      }
      const c = Math.round(colFrac);
      const r = Math.round(rowFrac);
      const idx = r * cols + c;
      const sx = surfaceMesh.geometry.getAttribute('position') as THREE.BufferAttribute;
      const px = sx.getX(idx);
      const py = sx.getY(idx);
      const pz = sx.getZ(idx);
      if (!Number.isFinite(px) || !Number.isFinite(py) || !Number.isFinite(pz)) continue;
      posAttr.setXYZ(count, px, py + 0.1, pz);
      count++;
    }

    if (count < 2) { opTrailLine.visible = false; return; }
    trailPointCount = count;
    posAttr.needsUpdate = true;
    opTrailLine.geometry.setDrawRange(0, trailPointCount);
    opTrailLine.visible = true;
  }

  function updateBrushMarkers() {
    if (!brushMarkers) return;
    const dummy = new THREE.Object3D();
    let count = 0;
    for (const key of brushSelection) {
      const [r, c] = key.split(',').map(Number);
      if (surfaceMesh) {
        const posAttr = surfaceMesh.geometry.getAttribute('position') as THREE.BufferAttribute;
        const idx = r * tableDef.cols + c;
        dummy.position.set(posAttr.getX(idx), posAttr.getY(idx) + 0.25, posAttr.getZ(idx));
        dummy.updateMatrix();
        brushMarkers.setMatrixAt(count, dummy.matrix);
        count++;
      }
    }
    brushMarkers.count = count;
    brushMarkers.instanceMatrix.needsUpdate = true;
    brushMarkers.visible = count > 0;
  }

  function gridToWorld(row: number, col: number): THREE.Vector3 {
    const cols = tableDef.cols;
    const rows = tableDef.rows;
    const scaleX = cols > 1 ? 10 / (cols - 1) : 10;
    const scaleZ = rows > 1 ? 10 / (rows - 1) : 10;
    const offsetX = -5;
    const offsetZ = -5;
    const val = getOutputValue(tableData, row, col, cols);
    const range = zMax - zMin;
    const heightScale = range > 0 ? 5 / range : 0;
    return new THREE.Vector3(
      offsetX + col * scaleX,
      (val - zMin) * heightScale + 0.15,
      offsetZ + row * scaleZ
    );
  }

  function findNearestVertex(screenX: number, screenY: number): { row: number; col: number } | null {
    if (!surfaceMesh) return null;
    const rect = containerEl.getBoundingClientRect();
    const mouse = new THREE.Vector2(
      ((screenX - rect.left) / rect.width) * 2 - 1,
      -((screenY - rect.top) / rect.height) * 2 + 1
    );
    const raycaster = new THREE.Raycaster();
    raycaster.setFromCamera(mouse, camera);
    const intersects = raycaster.intersectObject(surfaceMesh);
    if (intersects.length === 0) return null;

    const point = intersects[0].point;
    const cols = tableDef.cols;
    const rows = tableDef.rows;
    const scaleX = cols > 1 ? 10 / (cols - 1) : 10;
    const scaleZ = rows > 1 ? 10 / (rows - 1) : 10;
    const offsetX = -5;
    const offsetZ = -5;

    const col = Math.round((point.x - offsetX) / scaleX);
    const row = Math.round((point.z - offsetZ) / scaleZ);

    if (row >= 0 && row < rows && col >= 0 && col < cols) {
      return { row, col };
    }
    return null;
  }

  // ─── Smooth editing ─────────────────────────────────────────────────────

  function getSmoothFalloff(centerRow: number, centerCol: number, r: number, c: number, radius: number): number {
    const dist = Math.sqrt((r - centerRow) ** 2 + (c - centerCol) ** 2);
    if (dist > radius) return 0;
    return 1 - dist / (radius + 0.5);
  }

  function applySmoothRaiseLower(delta: number) {
    const radius = smoothRadius;
    const edits: Array<{ row: number; col: number; newVal: number }> = [];
    for (let r = 0; r < tableDef.rows; r++) {
      for (let c = 0; c < tableDef.cols; c++) {
        const falloff = getSmoothFalloff(selRow, selCol, r, c, radius);
        if (falloff <= 0) continue;
        const oldVal = getOutputValue(tableData, r, c, tableDef.cols);
        if (!Number.isFinite(oldVal)) continue;
        let newVal = Math.round((oldVal + delta * falloff) * 100) / 100;
        newVal = Number.isFinite(newVal) ? newVal : oldVal;
        if (onBatchEdit) {
          edits.push({ row: r, col: c, newVal });
        } else {
          onEditCell(r, c, newVal);
        }
      }
    }
    if (onBatchEdit && edits.length > 0) onBatchEdit(edits);
    rebuildAll();
  }

  function applySmoothEdit() {
    const radius = smoothRadius;
    const snapshot = [...tableData.output];
    const edits: Array<{ row: number; col: number; newVal: number }> = [];
    for (let r = 0; r < tableDef.rows; r++) {
      for (let c = 0; c < tableDef.cols; c++) {
        const falloff = getSmoothFalloff(selRow, selCol, r, c, radius);
        if (falloff <= 0) continue;
        const oldVal = snapshot[r * tableDef.cols + c];
        if (!Number.isFinite(oldVal)) continue;
        let sum = 0, count = 0;
        for (let dr = -radius; dr <= radius; dr++) {
          for (let dc = -radius; dc <= radius; dc++) {
            const nr = r + dr, nc = c + dc;
            if (nr >= 0 && nr < tableDef.rows && nc >= 0 && nc < tableDef.cols) {
              const nv = snapshot[nr * tableDef.cols + nc];
              if (Number.isFinite(nv)) { sum += nv; count++; }
            }
          }
        }
        if (count === 0) continue;
        const avg = sum / count;
        const newVal = Math.round((oldVal + 0.5 * falloff * (avg - oldVal)) * 100) / 100;
        if (!Number.isFinite(newVal) || Math.abs(newVal - oldVal) < 0.001) continue;
        if (onBatchEdit) {
          edits.push({ row: r, col: c, newVal });
        } else {
          onEditCell(r, c, newVal);
        }
      }
    }
    if (onBatchEdit && edits.length > 0) onBatchEdit(edits);
    rebuildAll();
  }

  // ─── Camera ──────────────────────────────────────────────────────────────

  function updateCamera() {
    const x = spherical.radius * Math.sin(spherical.phi) * Math.cos(spherical.theta) + panOffset.x;
    const y = spherical.radius * Math.cos(spherical.phi) + panOffset.y;
    const z = spherical.radius * Math.sin(spherical.phi) * Math.sin(spherical.theta) + panOffset.z;
    camera.position.set(
      pivotPoint.x + x,
      pivotPoint.y + y,
      pivotPoint.z + z
    );
    camera.lookAt(pivotPoint);
  }

  function setView(mode: string) {
    viewMode = mode as typeof viewMode;
    const dist = spherical.radius;
    switch (mode) {
      case 'front': spherical.theta = 0; spherical.phi = Math.PI / 2; break;
      case 'back': spherical.theta = Math.PI; spherical.phi = Math.PI / 2; break;
      case 'left': spherical.theta = -Math.PI / 2; spherical.phi = Math.PI / 2; break;
      case 'right': spherical.theta = Math.PI / 2; spherical.phi = Math.PI / 2; break;
      case 'top': spherical.phi = 0.01; break;
      case 'bottom': spherical.phi = Math.PI - 0.01; break;
      case 'iso': spherical.theta = Math.PI / 4; spherical.phi = Math.PI / 3; break;
      case 'perspective': spherical.theta = Math.PI / 4; spherical.phi = Math.PI / 3; break;
    }
    panOffset = { x: 0, y: 0, z: 0 };
    updateCamera();
  }

  // ─── Mouse handlers ─────────────────────────────────────────────────────

  function onMouseDown(e: MouseEvent) {
    if (e.button === 1 || (e.button === 0 && e.altKey)) {
      // Middle mouse (or Alt+Left) = orbit
      if (e.shiftKey) {
        isPanning = true;
      } else {
        isDragging = true;
      }
      e.preventDefault();
    } else if (e.button === 0 && e.shiftKey) {
      isPanning = true;
    } else if (e.button === 0) {
      if (brushMode) {
        const vert = findNearestVertex(e.clientX, e.clientY);
        if (vert) {
          const key = `${vert.row},${vert.col}`;
          const newSet = new Set(brushSelection);
          if (newSet.has(key)) newSet.delete(key);
          else newSet.add(key);
          brushSelection = newSet;
          updateBrushMarkers();
        }
      } else {
        isDragging = true;
      }
    } else if (e.button === 2) {
      isPanning = true;
    }
    lastMouse = { x: e.clientX, y: e.clientY };
  }

  function onMouseMove(e: MouseEvent) {
    if (isDragging) {
      const dx = e.clientX - lastMouse.x;
      const dy = e.clientY - lastMouse.y;
      spherical.theta -= dx * 0.005;
      spherical.phi = Math.max(0.05, Math.min(Math.PI - 0.05, spherical.phi - dy * 0.005));
      updateCamera();
    } else if (isPanning) {
      const dx = e.clientX - lastMouse.x;
      const dy = e.clientY - lastMouse.y;
      const panSpeed = spherical.radius * 0.002;
      const right = new THREE.Vector3();
      const up = new THREE.Vector3(0, 1, 0);
      camera.getWorldDirection(right);
      right.cross(up).normalize();
      panOffset.x += (-dx * right.x + dy * up.x) * panSpeed;
      panOffset.y += (-dx * right.y + dy * up.y) * panSpeed;
      panOffset.z += (-dx * right.z + dy * up.z) * panSpeed;
      updateCamera();
    } else {
      // Hover
      const vert = findNearestVertex(e.clientX, e.clientY);
      if (vert) {
        const val = getOutputValue(tableData, vert.row, vert.col, tableDef.cols);
        hoveredVertex = { row: vert.row, col: vert.col, value: val };
        if (hoverMarker) {
          const pos = gridToWorld(vert.row, vert.col);
          hoverMarker.position.set(pos.x, pos.y + 0.1, pos.z);
          hoverMarker.visible = true;
        }
      } else {
        hoveredVertex = null;
        if (hoverMarker) hoverMarker.visible = false;
      }
    }
    lastMouse = { x: e.clientX, y: e.clientY };
  }

  function onMouseUp(e: MouseEvent) {
    if (isDragging && !brushMode) {
      // Check if it was a click (minimal movement)
      const dx = Math.abs(e.clientX - lastMouse.x);
      const dy = Math.abs(e.clientY - lastMouse.y);
      if (dx < 3 && dy < 3) {
        const vert = findNearestVertex(e.clientX, e.clientY);
        if (vert) {
          selRow = vert.row;
          selCol = vert.col;
          updateSelectionMarker();
          updateSmoothRadiusRing();
          onSelectionChange?.(vert.row, vert.col);
        }
      }
    }
    isDragging = false;
    isPanning = false;
  }

  function onWheel(e: WheelEvent) {
    e.preventDefault();
    const zoomFactor = 1 + e.deltaY * 0.001;
    spherical.radius = Math.max(2, Math.min(30, spherical.radius * zoomFactor));
    updateCamera();
  }

  function onContextMenu(e: MouseEvent) {
    e.preventDefault();
  }

  // ─── Keyboard ────────────────────────────────────────────────────────────

  function handleKeyDown(e: KeyboardEvent) {
    if (!containerEl || !document.activeElement) return;
    // Only handle if container or its children are focused
    const active = document.activeElement;
    if (!containerEl.contains(active) && active !== containerEl) return;

    const rows = tableDef.rows;
    const cols = tableDef.cols;

    switch (e.key) {
      case 'ArrowUp': e.preventDefault(); selRow = Math.max(0, selRow - 1); break;
      case 'ArrowDown': e.preventDefault(); selRow = Math.min(rows - 1, selRow + 1); break;
      case 'ArrowLeft': e.preventDefault(); selCol = Math.max(0, selCol - 1); break;
      case 'ArrowRight': e.preventDefault(); selCol = Math.min(cols - 1, selCol + 1); break;
      case 'q': case 'Q': e.preventDefault(); applySmoothRaiseLower(increment); return;
      case 'w': case 'W': e.preventDefault(); applySmoothRaiseLower(-increment); return;
      case 'a': case 'A': {
        if (e.ctrlKey || e.metaKey) return; // Don't intercept Ctrl+A
        e.preventDefault();
        const oldVal = getOutputValue(tableData, selRow, selCol, cols);
        onEditCell(selRow, selCol, Math.round((oldVal + increment) * 100) / 100);
        rebuildAll();
        return;
      }
      case 's': case 'S': {
        if (e.ctrlKey || e.metaKey) return;
        e.preventDefault();
        const oldVal = getOutputValue(tableData, selRow, selCol, cols);
        onEditCell(selRow, selCol, Math.round((oldVal - increment) * 100) / 100);
        rebuildAll();
        return;
      }
      case 'e': case 'E': e.preventDefault(); applySmoothEdit(); return;
      case 'r': case 'R': {
        if (e.ctrlKey || e.metaKey) return;
        e.preventDefault();
        autoRotate = !autoRotate;
        return;
      }
      case 'Escape':
        if (brushMode) { brushSelection = new Set(); updateBrushMarkers(); }
        break;
    }

    updateSelectionMarker();
    updateSmoothRadiusRing();
    onSelectionChange?.(selRow, selCol);
  }

  // ─── Touch editing (mobile) ─────────────────────────────────────────────

  function touchRaise() { applySmoothRaiseLower(increment); }
  function touchLower() { applySmoothRaiseLower(-increment); }
  function touchIncrement() {
    const oldVal = getOutputValue(tableData, selRow, selCol, tableDef.cols);
    onEditCell(selRow, selCol, Math.round((oldVal + increment) * 100) / 100);
    rebuildAll();
  }
  function touchDecrement() {
    const oldVal = getOutputValue(tableData, selRow, selCol, tableDef.cols);
    onEditCell(selRow, selCol, Math.round((oldVal - increment) * 100) / 100);
    rebuildAll();
  }
  function touchSmooth() { applySmoothEdit(); }

  // ─── Export ──────────────────────────────────────────────────────────────

  async function exportPNG() {
    renderer.render(scene, camera);
    const dataUrl = renderer.domElement.toDataURL('image/png');
    const base64 = dataUrl.split(',')[1];
    const result = await HybridBridge.saveBinaryFile(`${tableDef.name}_3D`, base64, '.png');
    if (!result.success && result.error !== 'Save cancelled') {
      console.error('Export PNG failed:', result.error);
    }
  }

  async function exportOBJ() {
    if (!surfaceMesh) return;
    const geom = surfaceMesh.geometry;
    const pos = geom.getAttribute('position') as THREE.BufferAttribute;
    const idx = geom.getIndex();
    let obj = `# ${tableDef.name} 3D surface\n`;
    for (let i = 0; i < pos.count; i++) {
      obj += `v ${pos.getX(i).toFixed(4)} ${pos.getY(i).toFixed(4)} ${pos.getZ(i).toFixed(4)}\n`;
    }
    if (idx) {
      for (let i = 0; i < idx.count; i += 3) {
        obj += `f ${idx.getX(i) + 1} ${idx.getX(i + 1) + 1} ${idx.getX(i + 2) + 1}\n`;
      }
    }
    const result = await HybridBridge.saveFile(`${tableDef.name}_3D`, obj, '.obj');
    if (!result.success && result.error !== 'Save cancelled') {
      console.error('Export OBJ failed:', result.error);
    }
  }

  // ─── Lifecycle ───────────────────────────────────────────────────────────

  onMount(() => {
    mounted = true;
    const width = containerEl.clientWidth;
    const height = containerEl.clientHeight;

    // Renderer
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(0x0a0a0a, 1);
    containerEl.appendChild(renderer.domElement);

    // Scene
    scene = new THREE.Scene();

    // Camera
    camera = new THREE.PerspectiveCamera(50, width / height, 0.1, 100);
    spherical.radius = 12;
    spherical.phi = Math.PI / 3;
    spherical.theta = Math.PI / 4;
    updateCamera();

    // Lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
    scene.add(ambientLight);
    const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
    dirLight.position.set(5, 10, 7);
    scene.add(dirLight);
    const backLight = new THREE.DirectionalLight(0x4488cc, 0.3);
    backLight.position.set(-5, -3, -5);
    scene.add(backLight);

    // Grid
    gridHelper = new THREE.GridHelper(10, 10, 0x333333, 0x222222);
    gridHelper.position.y = -0.05;
    gridHelper.frustumCulled = false;
    scene.add(gridHelper);

    // Selection marker (glowing sphere)
    const selGeom = new THREE.SphereGeometry(0.18, 16, 16);
    const selMat = new THREE.MeshBasicMaterial({ color: 0x00ff88, transparent: true, opacity: 0.9 });
    selectionMarker = new THREE.Mesh(selGeom, selMat);
    selectionMarker.frustumCulled = false;
    scene.add(selectionMarker);

    // Hover marker
    const hoverGeom = new THREE.SphereGeometry(0.12, 8, 8);
    const hoverMat = new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.6 });
    hoverMarker = new THREE.Mesh(hoverGeom, hoverMat);
    hoverMarker.frustumCulled = false;
    hoverMarker.visible = false;
    scene.add(hoverMarker);

    // Smooth radius ring (pre-allocated, updated in-place)
    const ringMat = new THREE.LineBasicMaterial({ color: 0x00ff88, transparent: true, opacity: 0.4 });
    const ringPos = new Float32Array(RING_MAX * 3);
    const ringGeom = new THREE.BufferGeometry();
    ringGeom.setAttribute('position', new THREE.BufferAttribute(ringPos, 3));
    ringGeom.setDrawRange(0, 0);
    smoothRadiusRing = new THREE.Line(ringGeom, ringMat);
    smoothRadiusRing.frustumCulled = false;
    smoothRadiusRing.visible = false;
    scene.add(smoothRadiusRing);

    // Operating point marker (pulsing orange)
    const opGeom = new THREE.SphereGeometry(0.22, 16, 16);
    const opMat = new THREE.MeshBasicMaterial({ color: 0xff8800, transparent: true, opacity: 0.9 });
    opMarker = new THREE.Mesh(opGeom, opMat);
    opMarker.frustumCulled = false;
    opMarker.visible = false;
    scene.add(opMarker);

    // Operating point trail (pre-allocated, updated in-place)
    const trailMat = new THREE.LineBasicMaterial({ color: 0xff8800, transparent: true, opacity: 0.5 });
    const trailPos = new Float32Array(TRAIL_MAX * 3);
    const trailGeom = new THREE.BufferGeometry();
    trailGeom.setAttribute('position', new THREE.BufferAttribute(trailPos, 3));
    trailGeom.setDrawRange(0, 0);
    opTrailLine = new THREE.Line(trailGeom, trailMat);
    opTrailLine.frustumCulled = false;
    opTrailLine.visible = false;
    scene.add(opTrailLine);

    // Brush markers (instanced)
    const brushGeom = new THREE.SphereGeometry(0.14, 8, 8);
    const brushMat = new THREE.MeshBasicMaterial({ color: 0x6366f1, transparent: true, opacity: 0.7 });
    brushMarkers = new THREE.InstancedMesh(brushGeom, brushMat, 256);
    brushMarkers.frustumCulled = false;
    brushMarkers.visible = false;
    scene.add(brushMarkers);

    // Build surface
    rebuildAll();

    // ─── ViewCube (AutoCAD/Blender-style) ─────────────────────────────────
    vcScene = new THREE.Scene();
    vcCamera = new THREE.OrthographicCamera(-1.6, 1.6, 1.6, -1.6, 0.1, 20);
    vcCamera.position.set(0, 0, 5);
    vcCamera.lookAt(0, 0, 0);
    vcScene.add(new THREE.AmbientLight(0xffffff, 0.9));

    vcCube = new THREE.Group();

    // BoxGeometry face order: +X(0) -X(1) +Y(2) -Y(3) +Z(4) -Z(5)
    // Camera at +X = "front" view → +X face = "Front"
    const faceSize = 1;
    const faceConfigs = [
      { label: 'FRONT',  color: [70, 120, 200] },   // +X
      { label: 'BACK',   color: [70, 120, 200] },   // -X
      { label: 'TOP',    color: [70, 160, 80] },    // +Y
      { label: 'BOTTOM', color: [70, 160, 80] },    // -Y
      { label: 'RIGHT',  color: [200, 70, 60] },    // +Z
      { label: 'LEFT',   color: [200, 70, 60] },    // -Z
    ];

    const faceMaterials = faceConfigs.map(fc => {
      const canvas = document.createElement('canvas');
      canvas.width = 128; canvas.height = 128;
      const ctx = canvas.getContext('2d')!;
      const [r, g, b] = fc.color;
      ctx.fillStyle = `rgba(${r},${g},${b},0.7)`;
      ctx.fillRect(0, 0, 128, 128);
      ctx.strokeStyle = 'rgba(255,255,255,0.6)';
      ctx.lineWidth = 2;
      ctx.strokeRect(1, 1, 126, 126);
      ctx.font = 'bold 22px Arial, sans-serif';
      ctx.fillStyle = '#fff';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.shadowColor = 'rgba(0,0,0,0.6)';
      ctx.shadowBlur = 3;
      ctx.fillText(fc.label, 64, 64);
      const tex = new THREE.CanvasTexture(canvas);
      tex.minFilter = THREE.LinearFilter;
      return new THREE.MeshBasicMaterial({ map: tex, transparent: true, opacity: 0.85, side: THREE.FrontSide });
    });

    const boxMesh = new THREE.Mesh(new THREE.BoxGeometry(faceSize, faceSize, faceSize), faceMaterials);
    vcCube.add(boxMesh);

    // Wireframe edges
    const edgeMat = new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.4 });
    vcCube.add(new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.BoxGeometry(faceSize * 1.001, faceSize * 1.001, faceSize * 1.001)), edgeMat));

    vcScene.add(vcCube);

    // Fixed compass ring + axis labels (NOT children of vcCube — don't rotate)
    const compassGroup = new THREE.Group();
    const ringRad = 1.15;
    const compassRingPos: number[] = [];
    for (let i = 0; i <= 64; i++) {
      const a = (i / 64) * Math.PI * 2;
      compassRingPos.push(Math.cos(a) * ringRad, -faceSize / 2 - 0.3, Math.sin(a) * ringRad);
    }
    const compassRingGeom2 = new THREE.BufferGeometry();
    compassRingGeom2.setAttribute('position', new THREE.Float32BufferAttribute(compassRingPos, 3));
    compassGroup.add(new THREE.Line(compassRingGeom2, new THREE.LineBasicMaterial({ color: 0x666666 })));

    // Axis label sprites (fixed position, don't rotate with cube)
    function makeFixedLabel(text: string, x: number, y: number, z: number, color: string): THREE.Sprite {
      const canvas = document.createElement('canvas');
      canvas.width = 64; canvas.height = 32;
      const ctx = canvas.getContext('2d')!;
      ctx.font = 'bold 24px Arial, sans-serif';
      ctx.fillStyle = color;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(text, 32, 16);
      const tex = new THREE.CanvasTexture(canvas);
      tex.minFilter = THREE.LinearFilter;
      const mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false });
      const sprite = new THREE.Sprite(mat);
      sprite.position.set(x, y, z);
      sprite.scale.set(0.4, 0.2, 1);
      return sprite;
    }
    compassGroup.add(makeFixedLabel('X', 1.5, -faceSize / 2 - 0.3, 0, '#ff4444'));
    compassGroup.add(makeFixedLabel('Y', 0, 1.5, 0, '#44cc44'));
    compassGroup.add(makeFixedLabel('Z', 0, -faceSize / 2 - 0.3, 1.5, '#4488ff'));
    vcScene.add(compassGroup);

    // Renderer
    vcRenderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    vcRenderer.setSize(120, 120);
    vcRenderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    vcRenderer.setClearColor(0x000000, 0);
    vcRenderer.domElement.id = 'viewcube-canvas';
    vcRenderer.domElement.style.cssText = 'position:absolute;top:44px;right:8px;border-radius:6px;background:rgba(10,10,10,0.8);border:1px solid rgba(255,255,255,0.12);cursor:pointer;z-index:15;';
    containerEl.appendChild(vcRenderer.domElement);

    vcRenderer.domElement.addEventListener('click', (e: MouseEvent) => {
      const rect = vcRenderer.domElement.getBoundingClientRect();
      vcMouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      vcMouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      vcRaycaster.setFromCamera(vcMouse, vcCamera);
      const hits = vcRaycaster.intersectObject(boxMesh, false);
      if (hits.length > 0) {
        const faceIdx = hits[0].face?.materialIndex ?? -1;
        const viewMap = ['front', 'back', 'top', 'bottom', 'right', 'left'];
        if (faceIdx >= 0 && faceIdx < viewMap.length) setView(viewMap[faceIdx]);
      }
    });

    // Animation loop
    let lastTime = performance.now();
    function animate(time: number) {
      animFrameId = requestAnimationFrame(animate);
      if (!mounted) return;

      const dt = (time - lastTime) / 1000;
      lastTime = time;

      if (autoRotate) {
        spherical.theta += dt * 0.3;
        updateCamera();
      }

      // Pulse selection marker
      const pulse = 0.8 + 0.2 * Math.sin(time * 0.005);
      selectionMarker.scale.setScalar(pulse);

      // Pulse op marker
      const opPulse = 0.85 + 0.15 * Math.sin(time * 0.003);
      opMarker.scale.setScalar(opPulse);

      try { renderer.render(scene, camera); } catch (e) { console.warn('[Graph3D] render error:', e); }

      // Sync ViewCube rotation with main camera
      if (vcCube && vcRenderer && vcScene && vcCamera) {
        const camDir = new THREE.Vector3(
          Math.sin(spherical.phi) * Math.cos(spherical.theta),
          Math.cos(spherical.phi),
          Math.sin(spherical.phi) * Math.sin(spherical.theta)
        ).normalize();
        const q = new THREE.Quaternion();
        q.setFromUnitVectors(camDir, new THREE.Vector3(0, 0, 1));
        vcCube.quaternion.copy(q);
        vcRenderer.render(vcScene, vcCamera);
      }
    }
    animFrameId = requestAnimationFrame(animate);

    // Resize observer
    const resizeObs = new ResizeObserver(() => {
      if (!containerEl || !renderer || !camera) return;
      const w = containerEl.clientWidth;
      const h = containerEl.clientHeight;
      renderer.setSize(w, h);
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
    });
    resizeObs.observe(containerEl);

    // Focus for keyboard
    containerEl.tabIndex = 0;
    containerEl.focus();
  });

  onDestroy(() => {
    mounted = false;
    if (animFrameId) cancelAnimationFrame(animFrameId);
    renderer?.dispose();
    containerEl?.removeChild(renderer?.domElement);
    vcRenderer?.dispose();
    if (vcRenderer && containerEl) containerEl.removeChild(vcRenderer.domElement);
  });

  // No $effect for rebuilds — smooth ops call rebuildAll() directly,
  // and external data loads are handled via the parent calling rebuild().
  export function rebuild() { rebuildAll(); }

  // Update operating point
  $effect(() => {
    updateOpMarker();
    updateOpTrail();
  });

  // Update contour visibility
  $effect(() => {
    if (contourLines) contourLines.visible = showContoursLocal;
  });

  // Update wireframe visibility
  $effect(() => {
    if (wireframeMesh) wireframeMesh.visible = showWireframe;
  });

  // Expose export functions
  export function getRenderer() { return renderer; }
  export function getScene() { return scene; }
  export function getCamera() { return camera; }
</script>

<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
<div
  bind:this={containerEl}
  class="relative h-full w-full outline-none"
  style="background-color: #0a0a0a;"
  onmousedown={onMouseDown}
  onmousemove={onMouseMove}
  onmouseup={onMouseUp}
  onwheel={onWheel}
  oncontextmenu={onContextMenu}
  onkeydown={handleKeyDown}
  role="application"
  aria-label="3D table surface"
>
  <!-- ─── Top Bar ────────────────────────────────────────────────────── -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div
    class="absolute top-0 left-0 right-0 z-10 flex items-center gap-3 border-b px-3 py-2"
    style="background-color: var(--metro-surface); border-color: var(--metro-border);"
    onpointerdown={(e) => e.stopPropagation()}
    onmousedown={(e) => e.stopPropagation()}
    onclick={(e) => e.stopPropagation()}
  >
    <!-- Selected cell readout — expressed in axis domain terms -->
    <div class="min-w-0 flex-1 overflow-hidden">
      <div class="flex items-baseline gap-1.5 text-[12px] whitespace-nowrap">
        <span class="shrink-0 font-bold uppercase tracking-wider text-[var(--metro-orange)]">
          {tableDef.input0Name || 'X'}:
        </span>
        <span class="shrink-0 font-mono font-bold text-white">
          {typeof tableData.input0[selCol] === 'number' ? tableData.input0[selCol].toLocaleString() : (tableData.input0[selCol] ?? selCol)}
        </span>
        <span class="shrink-0 text-[var(--metro-text-muted)]">&times;</span>
        <span class="shrink-0 font-bold uppercase tracking-wider text-[var(--metro-orange)]">
          {tableDef.input1Name || 'Y'}:
        </span>
        <span class="shrink-0 font-mono font-bold text-white">
          {typeof tableData.input1[selRow] === 'number' ? tableData.input1[selRow].toLocaleString() : (tableData.input1[selRow] ?? selRow)}
        </span>
        <span class="shrink-0 text-[var(--metro-text-muted)]">&rarr;</span>
        <span class="shrink-0 font-mono font-bold text-[var(--metro-teal)]">
          {getOutputValue(tableData, selRow, selCol, tableDef.cols).toFixed(1)}{tableDef.outputUnit ? tableDef.outputUnit : ''}
        </span>
      </div>
    </div>

    <!-- View presets -->
    <div class="flex shrink-0 overflow-hidden border border-[var(--metro-border)]">
      {#each [
        { id: 'perspective', label: '3D' },
        { id: 'front', label: 'Front' },
        { id: 'back', label: 'Back' },
        { id: 'left', label: 'Left' },
        { id: 'right', label: 'Right' },
        { id: 'top', label: 'Top' },
      ] as view, i}
        {#if i > 0}<div class="h-5 w-px" style="background-color: var(--metro-border);"></div>{/if}
        <button
          class="flex h-5 items-center px-2 text-[9px] font-bold uppercase tracking-wider transition-colors duration-150"
          style="background-color: {viewMode === view.id ? 'var(--metro-orange)' : 'transparent'}; color: {viewMode === view.id ? '#fff' : 'var(--metro-text-secondary)'};"
          onclick={() => setView(view.id)}
        >
          {view.label}
        </button>
      {/each}
    </div>

    <!-- Display toggles -->
    <div class="flex shrink-0 overflow-hidden border border-[var(--metro-border)]">
      <button
        class="flex h-5 items-center gap-1 px-2 text-[9px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="background-color: {showWireframe ? 'rgba(0,165,165,0.2)' : 'transparent'}; color: {showWireframe ? 'var(--metro-teal)' : 'var(--metro-text-secondary)'};"
        onclick={() => { showWireframe = !showWireframe; }}
        title="Toggle wireframe mesh overlay"
      >
        <IconGridDots size={11} />
        <span class="hidden sm:inline">Wire</span>
      </button>
      <div class="h-5 w-px" style="background-color: var(--metro-border);"></div>
      <button
        class="flex h-5 items-center gap-1 px-2 text-[9px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="background-color: {showContoursLocal ? 'rgba(0,165,165,0.2)' : 'transparent'}; color: {showContoursLocal ? 'var(--metro-teal)' : 'var(--metro-text-secondary)'};"
        onclick={() => { showContoursLocal = !showContoursLocal; }}
        title="Toggle contour lines on the surface"
      >
        <IconEye size={11} />
        <span class="hidden sm:inline">Contour</span>
      </button>
      <div class="h-5 w-px" style="background-color: var(--metro-border);"></div>
      <button
        class="flex h-5 items-center gap-1 px-2 text-[9px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="background-color: {autoRotate ? 'rgba(216,59,1,0.2)' : 'transparent'}; color: {autoRotate ? 'var(--metro-orange)' : 'var(--metro-text-secondary)'};"
        onclick={() => { autoRotate = !autoRotate; }}
        title="Auto-rotate the view"
      >
        <IconRotate2 size={11} />
        <span class="hidden sm:inline">Spin</span>
      </button>
    </div>
  </div>

  <!-- ─── Mobile Touch Toolbar (right side, hidden on desktop) ──────── -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div
    class="absolute right-2 top-1/2 z-20 -translate-y-1/2 flex flex-col gap-1 lg:hidden"
    onpointerdown={(e) => e.stopPropagation()}
    onmousedown={(e) => e.stopPropagation()}
    onclick={(e) => e.stopPropagation()}
  >
    <!-- Raise (smooth +delta) -->
    <button
      class="flex h-10 w-10 items-center justify-center transition-colors duration-150 active:scale-95"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); color: var(--metro-green);"
      onclick={touchRaise}
      title="Raise surface"
    >
      <IconArrowUp size={18} />
    </button>

    <!-- Decrement cell (-step) -->
    <button
      class="flex h-10 w-10 items-center justify-center transition-colors duration-150 active:scale-95"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); color: var(--metro-red);"
      onclick={touchDecrement}
      title="Decrement value"
    >
      <IconMinus size={18} />
    </button>

    <!-- Current value readout -->
    <div
      class="flex h-10 w-10 items-center justify-center font-mono text-[10px] font-bold text-white"
      style="background-color: var(--metro-bg); border: 1px solid var(--metro-border);"
    >
      {getOutputValue(tableData, selRow, selCol, tableDef.cols).toFixed(1)}
    </div>

    <!-- Increment cell (+step) -->
    <button
      class="flex h-10 w-10 items-center justify-center transition-colors duration-150 active:scale-95"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); color: var(--metro-orange);"
      onclick={touchIncrement}
      title="Increment value"
    >
      <IconPlus size={18} />
    </button>

    <!-- Lower (smooth -delta) -->
    <button
      class="flex h-10 w-10 items-center justify-center transition-colors duration-150 active:scale-95"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); color: var(--metro-red);"
      onclick={touchLower}
      title="Lower surface"
    >
      <IconArrowDown size={18} />
    </button>

    <!-- Separator -->
    <div class="mx-auto h-px w-6" style="background-color: var(--metro-border);"></div>

    <!-- Smooth region -->
    <button
      class="flex h-10 w-10 items-center justify-center transition-colors duration-150 active:scale-95"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); color: var(--metro-teal);"
      onclick={touchSmooth}
      title="Smooth region"
    >
      <IconWaveSine size={18} />
    </button>
  </div>

  <!-- ─── Bottom Bar ──────────────────────────────────────────────────── -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div
    class="absolute bottom-0 left-0 right-0 z-10 flex items-center gap-3 border-t px-3 py-1.5"
    style="background-color: var(--metro-surface); border-color: var(--metro-border);"
    onpointerdown={(e) => e.stopPropagation()}
    onmousedown={(e) => e.stopPropagation()}
    onclick={(e) => e.stopPropagation()}
  >
    <!-- Keyboard help toggle -->
    <button
      class="flex shrink-0 items-center gap-1 text-[10px] font-bold uppercase tracking-wider transition-colors duration-150 {showHelp ? 'text-[var(--metro-teal)]' : 'text-[var(--metro-text-muted)] hover:text-[var(--metro-text-secondary)]'}"
      onclick={() => { showHelp = !showHelp; }}
    >
      <IconKeyboard size={14} />
      <span class="hidden sm:inline">Shortcuts</span>
      {#if showHelp}
        <IconChevronDown size={12} />
      {:else}
        <IconChevronRight size={12} />
      {/if}
    </button>

    {#if showHelp}
      <div class="flex items-center gap-3 border-l border-[var(--metro-border)] pl-3 text-[10px] text-[var(--metro-text-secondary)]">
        <span><kbd class="font-mono font-bold text-white">Q</kbd>/<kbd class="font-mono font-bold text-white">W</kbd> raise/lower</span>
        <span><kbd class="font-mono font-bold text-white">A</kbd>/<kbd class="font-mono font-bold text-white">S</kbd> &plusmn;step</span>
        <span><kbd class="font-mono font-bold text-white">E</kbd> smooth region</span>
        <span><kbd class="font-mono font-bold text-white">R</kbd> rotate</span>
        <span class="hidden lg:inline">Drag: orbit &middot; Shift+drag: pan</span>
      </div>
    {/if}

    <div class="flex-1"></div>

    <!-- Smooth radius -->
    <div class="flex items-center gap-1.5 text-[10px]">
      <span class="font-bold uppercase tracking-wider text-[var(--metro-text-muted)]">Smooth</span>
      <input
        type="number"
        value={smoothRadius}
        min="0"
        max="8"
        class="h-6 w-12 rounded border bg-[var(--metro-bg)] px-1 text-center font-mono text-[11px] text-white outline-none"
        style="border-color: var(--metro-border);"
        oninput={(e) => { smoothRadius = Number((e.target as HTMLInputElement).value) || 0; }}
        title="Neighboring cells averaged when pressing E"
      />
    </div>

    <!-- Step increment -->
    <div class="flex items-center gap-1.5 text-[10px]">
      <span class="font-bold uppercase tracking-wider text-[var(--metro-text-muted)]">Step</span>
      <input
        type="number"
        value={increment}
        min="0.1"
        max="100"
        step="0.5"
        class="h-6 w-12 rounded border bg-[var(--metro-bg)] px-1 text-center font-mono text-[11px] text-white outline-none"
        style="border-color: var(--metro-border);"
        oninput={(e) => { increment = Number((e.target as HTMLInputElement).value) || 1; }}
        title="Amount added/subtracted per keypress (A/S)"
      />
    </div>

    <!-- Brush count -->
    {#if brushSelection.size > 0}
      <div class="flex items-center gap-1.5 border-l border-[var(--metro-border)] pl-3 text-[10px]">
        <IconPaint size={11} class="text-[var(--metro-teal)]" />
        <span class="font-mono font-bold text-[var(--metro-teal)]">{brushSelection.size}</span>
        <span class="uppercase tracking-wider text-[var(--metro-text-muted)]">cells</span>
      </div>
    {/if}

    <!-- Export -->
    <div class="flex items-center gap-1 border-l border-[var(--metro-border)] pl-3">
      <button
        class="flex h-5 items-center gap-1 px-2 text-[9px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
        onclick={exportPNG}
        title="Save surface as PNG image"
      >
        <IconDownload size={11} />
        PNG
      </button>
      <button
        class="flex h-5 items-center gap-1 px-2 text-[9px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
        onclick={exportOBJ}
        title="Save surface as 3D OBJ model"
      >
        <IconDownload size={11} />
        OBJ
      </button>
    </div>
  </div>
</div>
