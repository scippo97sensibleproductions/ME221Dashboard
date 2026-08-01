import { mount } from 'svelte'
import '@fontsource-variable/orbitron'
import '@fontsource/dseg7-classic/400.css'
import './app.css'
import App from './App.svelte'
import { attachDevErrorOverlay, attachReleaseErrorOverlay, handleDevHotUpdate } from './lib/errorOverlay'

const app = mount(App, {
  target: document.getElementById('app')!,
})

if (import.meta.env.DEV) {
  attachDevErrorOverlay()
  // Clear the overlay once the fixed module is hot-updated back in.
  import.meta.hot?.on('vite:afterUpdate', () => handleDevHotUpdate())
} else {
  attachReleaseErrorOverlay()
}

export default app
