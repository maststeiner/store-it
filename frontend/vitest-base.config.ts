import { defineConfig } from 'vitest/config';

// Runner config for the Angular unit-test builder (--runner-config).
// Enforces the line-coverage gate at 70% — consistent with the backend
// coverlet threshold. The run fails when coverage drops below it.
export default defineConfig({
  test: {
    coverage: {
      provider: 'v8',
      thresholds: {
        lines: 70,
      },
    },
  },
});
