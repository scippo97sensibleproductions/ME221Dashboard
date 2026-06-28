<script lang="ts">
  let { label, value, onchange }: {
    label: string;
    value: number;
    onchange: (value: number) => void;
  } = $props();

  const positions = [
    { value: 0, label: '↖' },
    { value: 1, label: '↑' },
    { value: 2, label: '↗' },
    { value: 3, label: '←' },
    { value: 4, label: '●' },
    { value: 5, label: '→' },
    { value: 6, label: '↙' },
    { value: 7, label: '↓' },
    { value: 8, label: '↘' },
  ];

  const positionLabels = ['Top L', 'Top', 'Top R', 'Left', 'Center', 'Right', 'Bot L', 'Bottom', 'Bot R'];
</script>

<div>
  <p class="mb-1 text-[10px] text-gray-500">{label}</p>
  <div class="grid grid-cols-3 gap-0.5 w-fit">
    {#each positions as pos, i}
      <button
        class="flex items-center justify-center h-8 w-8 rounded text-[10px] font-medium transition-all
          {value === pos.value
            ? 'bg-cyan-500 text-white shadow-sm shadow-cyan-500/30'
            : 'bg-gray-800 text-gray-500 border border-gray-700 hover:border-gray-500 hover:text-gray-300'}"
        onclick={() => onchange(pos.value)}
        title={positionLabels[i]}
      >{pos.label}</button>
    {/each}
  </div>
</div>
