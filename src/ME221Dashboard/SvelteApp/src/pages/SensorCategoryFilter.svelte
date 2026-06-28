<script lang="ts">
  type Category = {
    name: string;
    total: number;
    selected: number;
  };

  let { categories, selectedCategory, onSelect }: {
    categories: Category[];
    selectedCategory: string;
    onSelect: (name: string) => void;
  } = $props();
</script>

<!-- Desktop sidebar -->
<div class="hidden w-48 shrink-0 overflow-y-auto p-2 lg:block" style="background-color: var(--metro-card); border: 1px solid var(--metro-border);">
  <div class="mb-1 flex items-center gap-2 border-l-[4px] pl-2 pb-2" style="border-color: var(--metro-purple);">
    <span class="text-[10px] font-extrabold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Categories</span>
  </div>
  {#each categories as cat}
    {@const isActive = cat.name === selectedCategory}
    <button
            class="mb-0.5 flex w-full items-center justify-between px-3 py-2.5 text-left text-[13px] transition-colors duration-150"
            style={isActive
              ? `background-color: var(--metro-card-hover); color: var(--metro-purple); border-left: 3px solid var(--metro-purple);`
              : `color: var(--metro-text-secondary); border-left: 3px solid transparent;`}
            onmouseenter={(e) => { if (!isActive) { e.currentTarget.style.backgroundColor = 'var(--metro-card-hover)'; e.currentTarget.style.color = 'var(--metro-text)'; }}}
            onmouseleave={(e) => { if (!isActive) { e.currentTarget.style.backgroundColor = 'transparent'; e.currentTarget.style.color = 'var(--metro-text-secondary)'; }}}
            onclick={() => onSelect(cat.name)}
    >
      <span class="truncate">{cat.name}</span>
      <span class="ml-2 shrink-0 px-2 py-0.5 text-[10px] font-bold uppercase"
            style={isActive
              ? 'background-color: var(--metro-purple); color: var(--metro-text-on-accent);'
              : 'background-color: var(--metro-border); color: var(--metro-text-secondary);'}
      >
        {cat.selected}/{cat.total}
      </span>
    </button>
  {/each}
</div>

<!-- Mobile chips -->
<div class="mb-3 flex shrink-0 gap-1.5 overflow-x-auto pb-1 lg:hidden">
  {#each categories as cat}
    {@const isActive = cat.name === selectedCategory}
    <button
            class="shrink-0 px-3 py-1.5 text-[11px] font-medium uppercase tracking-wider transition-colors duration-150"
            style={isActive
              ? 'background-color: var(--metro-purple); color: var(--metro-text-on-accent);'
              : 'background-color: var(--metro-card); color: var(--metro-text-secondary); border: 1px solid var(--metro-border);'}
            onmouseenter={(e) => { if (!isActive) { e.currentTarget.style.backgroundColor = 'var(--metro-card-hover)'; e.currentTarget.style.color = 'var(--metro-text)'; }}}
            onmouseleave={(e) => { if (!isActive) { e.currentTarget.style.backgroundColor = 'var(--metro-card)'; e.currentTarget.style.color = 'var(--metro-text-secondary)'; }}}
            onclick={() => onSelect(cat.name)}
    >
      {cat.name} ({cat.selected}/{cat.total})
    </button>
  {/each}
</div>
