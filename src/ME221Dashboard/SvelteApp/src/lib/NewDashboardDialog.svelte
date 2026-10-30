<script lang="ts">
  import MetroModal from './ui/MetroModal.svelte';

  let { open, name, error, onCreate, onClose }: {
    open: boolean;
    name: string;
    error: string | null;
    onCreate: (name: string) => void;
    onClose: () => void;
  } = $props();

  let inputValue = $state('');

  $effect(() => {
    if (open) inputValue = name;
  });

  function handleSubmit() {
    onCreate(inputValue);
  }
</script>

<MetroModal bind:open title="New Dashboard" size="xs" onclose={onClose}>
  <label class="block space-y-1">
    <span class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary)">Dashboard name</span>
    <input
      type="text"
      placeholder="e.g. Track Day"
      bind:value={inputValue}
      class="metro-input w-full"
      onkeydown={(e) => { if (e.key === 'Enter') handleSubmit(); }}
    />
  </label>
  {#if error}
    <p class="mt-1 text-[11px]" style="color: var(--metro-red)">{error}</p>
  {/if}

  {#snippet footer()}
    <button type="button" class="metro-btn-secondary" onclick={onClose}>Cancel</button>
    <button type="button" class="metro-btn-primary" onclick={handleSubmit}>Create</button>
  {/snippet}
</MetroModal>
