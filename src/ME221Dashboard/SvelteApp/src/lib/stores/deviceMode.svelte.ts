import { HybridBridge } from '../HybridBridge';
import type { DeviceProfile, UiMode } from '../HybridBridgeTypes';

/**
 * ─── Device UI Mode — single source of truth ─────────────────────────────────
 *
 * The UI mode is DECIDED ON THE NATIVE SIDE (MAUI DeviceInfo) and reported
 * over the bridge — never guessed from CSS media queries or viewport width:
 *
 *   Windows / MacCatalyst → 'desktop'  (keyboard + mouse fully expected)
 *   Android / iOS         → 'mobile'   (touch-first, EVEN ON TABLETS)
 *
 * In 'mobile' mode the width-based Tailwind breakpoints (sm/md/lg/xl/2xl) are
 * neutralized at runtime so a tablet's wide viewport can never bring in the
 * desktop layouts. The base (unprefixed) classes are the mobile UI.
 */

export const deviceMode = $state<DeviceProfile & { initialized: boolean }>({
  platform: '',
  idiom: '',
  uiMode: 'desktop',
  initialized: false,
});

/** Fetch the native device profile and apply the mode. Call once at startup. */
export async function initDeviceMode(): Promise<UiMode> {
  try {
    const profile = await HybridBridge.getDeviceProfile();
    deviceMode.platform = profile.platform;
    deviceMode.idiom = profile.idiom;
    deviceMode.uiMode = profile.uiMode;
    document.documentElement.dataset.ui = profile.uiMode;
    if (profile.uiMode === 'mobile') {
      disableWidthBreakpoints();
    }
  } catch {
    // Bridge unavailable (plain browser dev) — keep width-responsive behavior.
  }
  deviceMode.initialized = true;
  return deviceMode.uiMode;
}

/**
 * Neutralize every `@media (width >= …)` rule (Tailwind sm/md/lg/xl/2xl blocks)
 * so desktop layouts are impossible on mobile-mode devices at any viewport width.
 * Mutating `mediaText` to 'not all' is deterministic and re-evaluated by the
 * engine immediately.
 */
function disableWidthBreakpoints() {
  const walk = (rules: CSSRuleList) => {
    for (const rule of Array.from(rules)) {
      if (rule instanceof CSSMediaRule) {
        if (/\bwidth\s*>=\s*\d/.test(rule.conditionText)) {
          rule.media.mediaText = 'not all';
        }
      } else if (rule instanceof CSSGroupingRule) {
        walk(rule.cssRules);
      }
    }
  };
  for (const sheet of Array.from(document.styleSheets)) {
    try {
      walk(sheet.cssRules);
    } catch {
      // Cross-origin stylesheet — not ours, skip.
    }
  }
}
