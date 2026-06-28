<script lang="ts">
  import { Modal, Button } from 'flowbite-svelte';

  let { open, tableId, tableName, onclose }: {
    open: boolean;
    tableId: number;
    tableName: string;
    onclose: () => void;
  } = $props();

  let noteText = $state('');

  $effect(() => {
    if (open) {
      try {
        const stored = localStorage.getItem(`me221-note-${tableId}`);
        noteText = stored || '';
      } catch {
        noteText = '';
      }
    }
  });

  function save() {
    try {
      if (noteText.trim()) {
        localStorage.setItem(`me221-note-${tableId}`, noteText);
      } else {
        localStorage.removeItem(`me221-note-${tableId}`);
      }
    } catch {}
    onclose();
  }

  function deleteNote() {
    try {
      localStorage.removeItem(`me221-note-${tableId}`);
    } catch {}
    noteText = '';
    onclose();
  }
</script>

<Modal bind:open placement="bottom-center" size="lg" outsideclose={true} class="backdrop:bg-gray-900/80 rounded-t-2xl sm:rounded-2xl" ontoggle={(e) => { if (e.newState === 'closed') onclose(); }}>
  <div class="mb-3 flex items-center justify-between">
    <div>
      <div class="text-xs text-gray-500">{tableName}</div>
      <div class="text-sm font-medium text-gray-100">Notes</div>
    </div>
  </div>
  <textarea
    bind:value={noteText}
    placeholder="Add notes about this table..."
    rows="4"
    class="w-full rounded-lg border border-gray-600 bg-gray-900 px-3 py-2.5 text-sm text-gray-100 outline-none focus:border-cyan-500 placeholder-gray-500"
  ></textarea>
  <div class="mt-3 flex gap-2">
    {#if noteText.trim()}
      <Button color="red" class="!border-red-800/30 !bg-gray-700 !text-red-400 hover:!bg-red-900/30" onclick={deleteNote}>
        Delete
      </Button>
    {/if}
    <div class="flex-1"></div>
    <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600" onclick={onclose}>
      Cancel
    </Button>
    <Button class="!bg-cyan-600 hover:!bg-cyan-500 !text-white !border-cyan-600" onclick={save}>
      Save
    </Button>
  </div>
</Modal>
