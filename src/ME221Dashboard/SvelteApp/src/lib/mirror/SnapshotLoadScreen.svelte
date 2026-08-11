<script lang="ts">
</script>

<!-- R6: full-page snapshot loading state shown until the frame gate opens. -->
<div
  class="fixed inset-0 z-50 flex flex-col items-center justify-center gap-4"
  style="background-color: var(--metro-bg);"
  role="status"
  aria-label="Loading snapshot"
>
  <span class="inline-block h-8 w-8 animate-spin rounded-full border-2 border-gray-500 border-t-teal-400"></span>
  <p class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-teal);">Loading Snapshot</p>
  <p class="text-[13px]" style="color: var(--metro-text-secondary);">Synchronizing host state</p>
</div>
