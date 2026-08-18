// Minimal ambient types for the Node built-ins used by file-reading specs.
// The project has no @types/node (frontend runs in the browser); vitest, however,
// executes specs in Node, so these modules exist at runtime. Kept intentionally
// tiny — add members only when a spec needs them.

declare module 'node:fs' {
  export function readFileSync(path: string, encoding: 'utf-8'): string;
}

declare module 'node:path' {
  export function join(...parts: string[]): string;
}

declare const process: { cwd(): string };
