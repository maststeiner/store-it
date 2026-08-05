import { expect, test } from '@playwright/test';

// End-to-end: auth redirect + session establishment via the dev-login shortcut.
// The dev-login endpoint is mapped ONLY when the backend runs in Development mode
// (see DevAuthEndpoints.cs / Program.cs). These tests never run against Production.

test('unauthenticated visit to /storages redirects to the login screen', async ({ page }) => {
  await page.goto('/storages');

  // The auth guard redirects to /login; the login page shows provider buttons.
  await expect(page.getByRole('button', { name: /microsoft/i })).toBeVisible();
});

test('after dev-login the storages overview is reachable', async ({ page }) => {
  // Use page.request (not the fixture `request`) so the session cookie is stored
  // in the browser context that page.goto uses afterwards (:1659).
  const response = await page.request.post('/auth/dev-login');
  expect(response.status()).toBe(204);

  await page.goto('/storages');

  // The auth guard passes; the overview renders the "+ New storage" button.
  await expect(page.getByRole('button', { name: /new storage/i })).toBeVisible();
});
