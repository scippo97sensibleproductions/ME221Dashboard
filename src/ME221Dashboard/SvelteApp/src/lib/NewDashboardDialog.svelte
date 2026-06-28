<script lang="ts">
  import { Modal, Button } from 'flowbite-svelte';

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

<Modal form bind:open size="xs" placement="center" outsideclose={true} class="backdrop:bg-gray-900/80" ontoggle={(e) => { if (e.newState === 'closed') onClose(); }}>
  <h3 class="mb-3 text-sm font-bold text-cyan-400">New Dashboard</h3>
  <input
    type="text"
    placeholder="e.g. Track Day"
    value={inputValue}
    oninput={(e) => { inputValue = (e.target as HTMLInputElement).value; }}
    class="w-full rounded-lg border border-gray-600 bg-gray-800 px-3 py-2 text-sm text-gray-200 outline-none focus:border-cyan-500"
    onkeydown={(e) => { if (e.key === 'Enter') handleSubmit(); }}
  />
  {#if error}
    <p class="mt-1 text-xs text-red-400">{error}</p>
  {/if}
  <div class="mt-3 flex justify-end gap-2">
    <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600" onclick={onClose}>
      Cancel
    </Button>
    <Button type="submit" class="!bg-cyan-600 hover:!bg-cyan-500 !text-white border-cyan-600" onclick={handleSubmit}>
      Create
    </Button>
  </div>
</Modal>
