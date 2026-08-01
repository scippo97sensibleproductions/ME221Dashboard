import { defineConfig } from 'vitest/config';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  plugins: [
    // Required for .svelte.ts runes files (stores) imported by tests
    svelte({ hot: false }),
  ],
  test: {
    include: ['src/**/*.test.ts'],
  },
});
