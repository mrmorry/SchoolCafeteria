import { defineConfig } from 'vitest/config';

export default defineConfig({
  // Uses esbuild's automatic JSX runtime directly (no @vitejs/plugin-react needed) so test files
  // don't have to `import React` just to use JSX, matching Next.js's own JSX transform.
  esbuild: {
    jsx: 'automatic',
    jsxImportSource: 'react'
  },
  test: {
    environment: 'jsdom',
    globals: true
  }
});
