import { expect, test } from '@playwright/test';

// End-to-end: drives the real UI against the live backend + PostgreSQL.
// Each test creates its own uniquely-named storage so runs are independent
// (no shared-state assumptions, no cleanup coupling).

function uniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
}

test('create a storage and see it in the overview', async ({ page }) => {
  const name = uniqueName('Pantry');

  await page.goto('/storages');
  await page.getByRole('button', { name: /new storage/i }).click();
  await page.getByRole('textbox').last().fill(name);
  await page.getByRole('button', { name: /create/i }).click();

  await expect(page.getByText(name)).toBeVisible();
});

test('add an item and see it grouped by expiry status', async ({ page }) => {
  const storageName = uniqueName('Freezer');

  // Arrange: a fresh storage
  await page.goto('/storages');
  await page.getByRole('button', { name: /new storage/i }).click();
  await page.getByRole('textbox').last().fill(storageName);
  await page.getByRole('button', { name: /create/i }).click();
  await page.getByText(storageName).click();

  // Act: add an item that is already expired (yesterday)
  const yesterday = new Date(Date.now() - 86_400_000).toISOString().slice(0, 10);
  await page.locator('#item-name').fill('Yogurt');
  await page.locator('#item-amount').fill('2');
  await page.locator('#item-expiry').fill(yesterday);
  await page.getByRole('button', { name: /add/i }).click();

  // Assert: item appears in the "expired" group
  await expect(page.locator('.group.expired')).toContainText('Yogurt');
});

test('reject an item without any date (server validation surfaces in the UI)', async ({ page }) => {
  const storageName = uniqueName('Cellar');

  await page.goto('/storages');
  await page.getByRole('button', { name: /new storage/i }).click();
  await page.getByRole('textbox').last().fill(storageName);
  await page.getByRole('button', { name: /create/i }).click();
  await page.getByText(storageName).click();

  await page.locator('#item-name').fill('Flour');
  await page.locator('#item-amount').fill('1');
  await page.getByRole('button', { name: /add/i }).click();

  await expect(page.locator('.add-form .form-error')).toBeVisible();
});
