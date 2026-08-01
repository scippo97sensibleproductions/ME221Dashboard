import { mount } from 'svelte'
import '@fontsource-variable/orbitron'
import '@fontsource/dseg7-classic/400.css'
import './app.css'
import App from './App.svelte'

const app = mount(App, {
  target: document.getElementById('app')!,
})

export default app
