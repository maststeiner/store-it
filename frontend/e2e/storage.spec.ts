import { expect, test } from '@playwright/test';

// End-to-end: drives the real UI against the live backend + PostgreSQL.
// Each test creates its own uniquely-named storage so runs are independent
// (no shared-state assumptions, no cleanup coupling).

// Establish a real cookie session before each test using the dev-login shortcut
// (Development-only, mapped by DevAuthEndpoints.cs). Without this the auth guard
// redirects every /storages visit to /login and all assertions time out.
test.beforeEach(async ({ page }) => {
  const response = await page.request.post('/auth/dev-login');
  expect(response.status()).toBe(204);
});

function uniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
}

test('create a storage and see it in the overview', async ({ page }) => {
  const name = uniqueName('Pantry');

  await page.goto('/storages');
  await page.getByRole('button', { name: /create storage/i }).click();
  await page.getByRole('textbox').last().fill(name);
  await page.getByRole('button', { name: /create/i }).click();

  await expect(page.getByText(name)).toBeVisible();
});

test('add an item and see it grouped by expiry status', async ({ page }) => {
  const storageName = uniqueName('Freezer');

  // Arrange: a fresh storage
  await page.goto('/storages');
  await page.getByRole('button', { name: /create storage/i }).click();
  await page.getByRole('textbox').last().fill(storageName);
  await page.getByRole('button', { name: /create/i }).click();
  await page.getByText(storageName).click();

  // Act: add an item that is already expired (yesterday)
  const yesterday = new Date(Date.now() - 86_400_000).toISOString().slice(0, 10);
  // #182: the add form opens on demand
  await page.getByRole('button', { name: /new item/i }).click();
  await page.locator('#item-name').fill('Yogurt');
  await page.locator('#item-amount').fill('2');
  await page.locator('#item-expiry').fill(yesterday);
  // SPEC-011: a tag, added with the tag field's own button (amendment A1)
  await page.locator('#item-tags').fill('Dairy');
  await page.getByRole('button', { name: /add tag/i }).click();
  // The item's own Add — exact, so the tag button above does not match
  await page.getByRole('button', { name: 'Add', exact: true }).click();

  // Assert: item appears in the "expired" group, with its tag chip
  await expect(page.locator('.group.expired')).toContainText('Yogurt');
  await expect(page.locator('.group.expired .tag-chip')).toHaveText('Dairy');

  // SPEC-011 AC-13: the chip filters the storage by that tag
  await page.locator('.group.expired .tag-chip').click();
  await expect(page.locator('.tag-filter')).toContainText('Dairy');
  await expect(page.locator('.item-row')).toHaveCount(1);
});

test('reject an item without any date (server validation surfaces in the UI)', async ({ page }) => {
  const storageName = uniqueName('Cellar');

  await page.goto('/storages');
  await page.getByRole('button', { name: /create storage/i }).click();
  await page.getByRole('textbox').last().fill(storageName);
  await page.getByRole('button', { name: /create/i }).click();
  await page.getByText(storageName).click();

  // #182: the add form opens on demand
  await page.getByRole('button', { name: /new item/i }).click();
  await page.locator('#item-name').fill('Flour');
  await page.locator('#item-amount').fill('1');
  await page.getByRole('button', { name: 'Add', exact: true }).click();

  await expect(page.locator('.add-form .form-error')).toBeVisible();
});
