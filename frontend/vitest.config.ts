import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Date/time tests must not depend on the machine's zone (Constitution I).
process.env.TZ = 'America/Chicago'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    env: { TZ: 'America/Chicago' },
    setupFiles: ['src/test/setup.ts'],
  },
})
