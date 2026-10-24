import AxeBuilder from '@axe-core/playwright';
import { APIRequestContext, Locator, Page, expect, test } from '@playwright/test';

// SPEC-012 — mobile layout. Runs in the `mobile` project only (Chromium phone emulation,
// 390 × 844; one block at 320 × 568). The assertions are layout facts read from the real
// browser: document width vs. viewport (AC-01/02), bounding boxes inside their card
// (AC-04/05), tap-target sizes (AC-07), computed font sizes (AC-03) and the row structure
// with and without tags (AC-09/10). Every visited state is also scanned with axe-core
// against the WCAG 2.x A/AA rule set (AC-13) — zero violations is the gate.

const TAP_TARGET = 44; // SPEC-012 D2: Apple HIG / WCAG 2.5.5
const ROW_CHIP_MIN_HEIGHT = 32; // SPEC-012 D2: the one allowed exception
const MIN_INPUT_FONT_PX = 16; // SPEC-012 D6: iOS Safari zooms below this
const TOLERANCE = 0.5; // sub-pixel rounding of bounding boxes

// ── Helpers ──────────────────────────────────────────────────────────────────

/** AC-01/AC-02 (WCAG 1.4.10 reflow): the document is never wider than the viewport. */
async function expectNoHorizontalOverflow(page: Page, state: string): Promise<void> {
  const widths = await page.evaluate(() => ({
    document: document.documentElement.scrollWidth,
    body: document.body.scrollWidth,
    viewport: document.documentElement.clientWidth,
  }));
  expect(widths.document, `${state}: document wider than viewport`).toBeLessThanOrEqual(
    widths.viewport,
  );
  expect(widths.body, `${state}: body wider than viewport`).toBeLessThanOrEqual(widths.viewport);
}

/** AC-13 (D9): axe-core, WCAG 2.x A/AA rule set, zero violations. */
async function expectAccessible(page: Page, state: string): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  const violations = results.violations.map(
    (violation) =>
      `${violation.id} (${violation.impact}): ${violation.help} → ${violation.nodes
        .map((node) => node.target.join(' '))
        .join(' | ')}`,
  );
  expect(violations, `${state}: axe violations`).toEqual([]);
}

/** Reflow + axe in one go — every state the spec lists goes through this. */
async function expectStateOk(page: Page, state: string): Promise<void> {
  await expectNoHorizontalOverflow(page, state);
  await expectAccessible(page, state);
}

async function box(
  locator: Locator,
): Promise<{ x: number; y: number; width: number; height: number }> {
  const b = await locator.boundingBox();
  expect(b, `no bounding box for ${locator}`).not.toBeNull();
  return b!;
}

/** AC-04/AC-05: the child's box lies inside the container's box (and the container inside the viewport). */
async function expectInside(child: Locator, container: Locator, label: string): Promise<void> {
  const c = await box(child);
  const p = await box(container);
  const viewport = child.page().viewportSize()!;
  expect(p.x, `${label}: container left edge`).toBeGreaterThanOrEqual(-TOLERANCE);
  expect(p.x + p.width, `${label}: container right edge`).toBeLessThanOrEqual(
    viewport.width + TOLERANCE,
  );
  expect(c.x, `${label}: left of its container`).toBeGreaterThanOrEqual(p.x - TOLERANCE);
  expect(c.x + c.width, `${label}: right of its container`).toBeLessThanOrEqual(
    p.x + p.width + TOLERANCE,
  );
  expect(c.y, `${label}: above its container`).toBeGreaterThanOrEqual(p.y - TOLERANCE);
  expect(c.y + c.height, `${label}: below its container`).toBeLessThanOrEqual(
    p.y + p.height + TOLERANCE,
  );
}

/** AC-07 (D2): every visible button/select is at least 44 × 44; row tag chips at least 32 px high. */
async function expectTapTargets(page: Page, state: string): Promise<void> {
  const sizes = await page.evaluate(
    ({ tap, chip }) => {
      const tooSmall: string[] = [];
      const controls = document.querySelectorAll<HTMLElement>(
        'button, select, [role="option"], .session-chip',
      );
      for (const el of controls) {
        const rect = el.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) continue; // not rendered
        const isRowChip = el.matches('.item-tags .tag-chip');
        const minHeight = isRowChip ? chip : tap;
        const minWidth = isRowChip ? 0 : tap;
        if (rect.height + 0.5 < minHeight || rect.width + 0.5 < minWidth) {
          const label = el.getAttribute('aria-label') ?? el.textContent?.trim() ?? el.tagName;
          tooSmall.push(
            `${el.tagName.toLowerCase()}.${[...el.classList].join('.')} "${label}" ${Math.round(rect.width)}×${Math.round(rect.height)}`,
          );
        }
      }
      return tooSmall;
    },
    { tap: TAP_TARGET, chip: ROW_CHIP_MIN_HEIGHT },
  );
  expect(sizes, `${state}: tap targets below ${TAP_TARGET} px`).toEqual([]);
}

/** AC-03 (D6): every rendered input/select is at least 16 px. */
async function expectInputFontSizes(page: Page, state: string): Promise<void> {
  const small = await page.evaluate((min) => {
    const result: string[] = [];
    for (const el of document.querySelectorAll<HTMLElement>('input, select, textarea')) {
      const rect = el.getBoundingClientRect();
      if (rect.width === 0 || rect.height === 0) continue;
      const size = Number.parseFloat(getComputedStyle(el).fontSize);
      if (size < min) {
        result.push(
          `${el.tagName.toLowerCase()}#${el.id || el.getAttribute('name') || el.className} ${size}px`,
        );
      }
    }
    return result;
  }, MIN_INPUT_FONT_PX);
  expect(small, `${state}: controls below ${MIN_INPUT_FONT_PX} px`).toEqual([]);
}

// ── Test data through the API (same cookie session as the page) ──────────────

// SPEC-012 A1 / AC-15: ✎ and 🗑 (and the quantity) sit at the same place in every row,
// whether the row has tags or not — same x and width, on the line of the item name.
async function expectActionsAligned(page: Page, state: string): Promise<void> {
  const rows = page.locator('.item-row:not(.item-edit)');
  const count = await rows.count();
  expect(count, `${state}: needs several rows`).toBeGreaterThan(1);
  const reference = await box(rows.first().locator('.item-actions'));
  const referenceQty = await box(rows.first().locator('.item-qty'));
  for (let i = 0; i < count; i++) {
    const row = rows.nth(i);
    const name = await box(row.locator('.item-name'));
    const actions = await box(row.locator('.item-actions'));
    const qty = await box(row.locator('.item-qty'));
    expect(Math.abs(actions.x - reference.x), `${state}: actions x, row ${i}`).toBeLessThan(
      TOLERANCE + 1,
    );
    expect(
      Math.abs(actions.width - reference.width),
      `${state}: actions width, row ${i}`,
    ).toBeLessThan(TOLERANCE + 1);
    expect(Math.abs(qty.x - referenceQty.x), `${state}: quantity x, row ${i}`).toBeLessThan(
      TOLERANCE + 1,
    );
    // on the name line: the vertical centres overlap within the actions' own height
    expect(
      Math.abs(name.y + name.height / 2 - (actions.y + actions.height / 2)),
      `${state}: actions on the name line, row ${i}`,
    ).toBeLessThan(actions.height);
  }
}

function uniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
}

async function xsrfHeaders(page: Page): Promise<Record<string, string>> {
  await page.request.get('/auth/csrf');
  const cookie = (await page.context().cookies()).find((c) => c.name === 'XSRF-TOKEN');
  expect(cookie, 'XSRF-TOKEN cookie after /auth/csrf').toBeDefined();
  return { 'X-XSRF-TOKEN': decodeURIComponent(cookie!.value) };
}

interface SeededItem {
  name: string;
  amount: number;
  unit: 'Piece' | 'Gram' | 'Milliliter';
  expiryDate: string | null;
  productionDate: string | null;
  tags: string[];
}

async function createStorage(
  request: APIRequestContext,
  headers: Record<string, string>,
  name: string,
): Promise<string> {
  const response = await request.post('/api/v1/storages', { data: { name }, headers });
  expect(response.status(), 'create storage').toBe(201);
  return ((await response.json()) as { id: string }).id;
}

async function addItem(
  request: APIRequestContext,
  headers: Record<string, string>,
  storageId: string,
  item: SeededItem,
): Promise<void> {
  const response = await request.post(`/api/v1/storages/${storageId}/items`, {
    data: item,
    headers,
  });
  expect(response.status(), `add item ${item.name}`).toBe(201);
}

const DAY = 86_400_000;
const isoDate = (offsetDays: number): string =>
  new Date(Date.now() + offsetDays * DAY).toISOString().slice(0, 10);

/** The storage every detail test works on: five rows — all three expiry groups, D5 and the edge cases. */
async function seedStorage(page: Page): Promise<{ id: string; name: string }> {
  const headers = await xsrfHeaders(page);
  const name = uniqueName('Phone');
  const id = await createStorage(page.request, headers, name);
  // one row per expiry group, so the axe scan covers the status colours of the group heads
  // and dates (CodeRabbit on #251)
  await addItem(page.request, headers, id, {
    name: 'Expired yoghurt',
    amount: 1,
    unit: 'Piece',
    expiryDate: isoDate(-1),
    productionDate: null,
    tags: [],
  });
  await addItem(page.request, headers, id, {
    name: 'Expiring milk',
    amount: 1,
    unit: 'Milliliter',
    expiryDate: isoDate(1),
    productionDate: null,
    tags: [],
  });
  await addItem(page.request, headers, id, {
    name: 'Plain pasta',
    amount: 2,
    unit: 'Piece',
    expiryDate: isoDate(400),
    productionDate: null,
    tags: [],
  });
  await addItem(page.request, headers, id, {
    name: 'Canned tomatoes',
    amount: 3,
    unit: 'Piece',
    expiryDate: isoDate(300),
    productionDate: null,
    tags: ['Cans'],
  });
  // EC-01: ten tags of thirty characters; EC-02: a name without spaces longer than the row
  await addItem(page.request, headers, id, {
    name: 'Dosentomaten230gBioQualitätAktionspackungVomGrosshändler',
    amount: 500,
    unit: 'Gram',
    expiryDate: isoDate(200),
    productionDate: null,
    // padded with the widest glyph, so the chip width does not depend on the runner's fonts
    // (CI job 1b failed at 320 px with 'x' padding on Ubuntu fonts while it passed locally)
    tags: Array.from({ length: 10 }, (_, i) => `Tag${i}`.padEnd(30, 'W')),
  });
  return { id, name };
}

test.beforeEach(async ({ page }) => {
  const response = await page.request.post('/auth/dev-login');
  expect(response.status()).toBe(204);
});

// ── AC-01: every page ─────────────────────────────────────────────────────────

test('AC-01: no page is wider than the viewport (storage list, about, join, login)', async ({
  page,
}) => {
  await seedStorage(page);

  await page.goto('/storages');
  await expect(page.locator('.storage-card').first()).toBeVisible();
  await expectStateOk(page, 'storage list');
  await expectTapTargets(page, 'storage list');

  await page.goto('/about');
  await expect(page.locator('.about-page')).toBeVisible();
  await expectStateOk(page, 'about');

  await page.goto('/join');
  await expect(page.locator('main')).toBeVisible();
  await expectStateOk(page, 'join without token');

  await page.context().clearCookies();
  await page.goto('/login');
  await expect(page.locator('.login-card')).toBeVisible();
  await expectStateOk(page, 'login');
  await expectTapTargets(page, 'login');
});

// ── AC-02 … AC-10: the storage detail in every state ─────────────────────────

test.describe('storage detail on a phone', () => {
  let storage: { id: string; name: string };

  test.beforeEach(async ({ page }) => {
    storage = await seedStorage(page);
    await page.goto(`/storages/${storage.id}`);
    await expect(page.locator('.item-row')).toHaveCount(5);
  });

  test('AC-02 / AC-07 / AC-09 / AC-10: rows with and without tags, plain state', async ({
    page,
  }) => {
    await expectStateOk(page, 'detail plain');
    await expectTapTargets(page, 'detail plain');
    await expectActionsAligned(page, 'detail plain');

    // AC-09: a row without tags — name + actions on one line, quantity + date on the next
    const plain = page.locator('.item-row', { hasText: 'Plain pasta' });
    const plainName = await box(plain.locator('.item-name'));
    const plainActions = await box(plain.locator('.item-actions'));
    const plainQty = await box(plain.locator('.item-qty'));
    const plainDate = await box(plain.locator('.item-date'));
    expect(
      Math.abs(plainName.y + plainName.height / 2 - (plainActions.y + plainActions.height / 2)),
    ).toBeLessThan(plainActions.height); // vertically on the same line
    expect(plainQty.y).toBeGreaterThanOrEqual(plainName.y + plainName.height - TOLERANCE);
    expect(Math.abs(plainQty.y - plainDate.y)).toBeLessThan(TOLERANCE + 1); // one line
    expect(plainQty.x).toBeLessThan(plainDate.x); // quantity left, date right

    // AC-10: a row with tags — chips between the name line and the quantity/date line
    const tagged = page.locator('.item-row', { hasText: 'Canned tomatoes' });
    const name = await box(tagged.locator('.item-name'));
    const chips = await box(tagged.locator('.item-tags'));
    const qty = await box(tagged.locator('.item-qty'));
    const date = await box(tagged.locator('.item-date'));
    expect(chips.y).toBeGreaterThanOrEqual(name.y + name.height - TOLERANCE);
    expect(qty.y).toBeGreaterThanOrEqual(chips.y + chips.height - TOLERANCE);
    expect(Math.abs(qty.y - date.y)).toBeLessThan(TOLERANCE + 1);
    expect(qty.x).toBeLessThan(date.x);
    expect(Math.abs(chips.x - name.x)).toBeLessThan(TOLERANCE + 1); // chips start under the name

    // AC-11 / EC-01 / EC-02: long name and ten long tags grow the row in height, not width
    const long = page.locator('.item-row', { hasText: 'Dosentomaten230g' });
    const longBox = await box(long);
    const viewport = page.viewportSize()!;
    expect(longBox.x + longBox.width).toBeLessThanOrEqual(viewport.width + TOLERANCE);
    await expectInside(long.locator('.item-tags'), long, 'ten long tags');
    await expectInside(long.locator('.item-name'), long, 'long name');
    await expect(long.locator('.item-tags .tag-chip')).toHaveCount(10);
  });

  test('AC-02 / AC-03 / AC-05: the add form lies inside its card, 16 px controls', async ({
    page,
  }) => {
    await page.getByRole('button', { name: 'New item' }).click();
    const form = page.locator('.add-form');
    await expect(form).toBeVisible();

    await expectStateOk(page, 'add form open');
    await expectTapTargets(page, 'add form open');
    await expectInputFontSizes(page, 'add form open');

    for (const control of [
      '#item-name',
      '#item-amount',
      '#item-unit',
      '#item-expiry',
      '#item-production',
      '#item-tags',
    ]) {
      await expectInside(form.locator(control), form, control);
    }
    await expectInside(form.getByRole('button', { name: 'Add tag' }), form, 'Add tag');
    await expectInside(form.getByRole('button', { name: 'Add', exact: true }), form, 'Add');
    await expectInside(form.getByRole('button', { name: 'Cancel' }), form, 'Cancel');

    // AC-06 / EC-06: the suggestion list opens within the viewport
    await form.locator('#item-tags').fill('Ca');
    const suggestions = form.locator('.tag-suggestions');
    await expect(suggestions).toBeVisible();
    const s = await box(suggestions);
    expect(s.x).toBeGreaterThanOrEqual(-TOLERANCE);
    expect(s.x + s.width).toBeLessThanOrEqual(page.viewportSize()!.width + TOLERANCE);
    await expectStateOk(page, 'add form with suggestions');
  });

  test('AC-02 / AC-04: the inline edit (with 0 and with 10 tags) lies inside its card', async ({
    page,
  }) => {
    const controls = [
      '.edit-name',
      '.edit-amount',
      '.edit-unit',
      '.edit-expiry',
      '.edit-production',
      '.item-edit-tags input',
    ];

    // with 0 tags
    await page
      .locator('.item-row', { hasText: 'Plain pasta' })
      .getByRole('button', { name: 'Edit' })
      .click();
    let card = page.locator('.item-edit');
    await expect(card).toBeVisible();
    await expectStateOk(page, 'inline edit, no tags');
    await expectTapTargets(page, 'inline edit, no tags');
    await expectInputFontSizes(page, 'inline edit, no tags');
    for (const control of controls) {
      await expectInside(card.locator(control), card, `no tags ${control}`);
    }
    await expectInside(card.getByRole('button', { name: 'Add tag' }), card, 'Add tag');
    await expectInside(card.getByRole('button', { name: 'Save' }), card, 'Save');
    await expectInside(card.getByRole('button', { name: 'Cancel' }), card, 'Cancel');
    await card.getByRole('button', { name: 'Cancel' }).click();

    // with 10 tags (EC-01)
    await page
      .locator('.item-row', { hasText: 'Dosentomaten230g' })
      .getByRole('button', { name: 'Edit' })
      .click();
    card = page.locator('.item-edit');
    await expect(card).toBeVisible();
    await expect(card.locator('.tag-chips .tag-chip')).toHaveCount(10);
    await expectStateOk(page, 'inline edit, ten tags');
    await expectTapTargets(page, 'inline edit, ten tags');
    for (const control of controls) {
      await expectInside(card.locator(control), card, `ten tags ${control}`);
    }
    await expectInside(card.locator('.tag-chips'), card, 'ten chips');
    await expectInside(card.getByRole('button', { name: 'Save' }), card, 'Save');
    await expectInside(card.getByRole('button', { name: 'Cancel' }), card, 'Cancel');
  });

  test('AC-02: search panel with the tag list, sharing panel and the delete dialog', async ({
    page,
  }) => {
    await page.getByRole('button', { name: 'Search items' }).click();
    await expect(page.locator('.tag-list')).toBeVisible();
    await expectStateOk(page, 'search open with tag list');
    await expectTapTargets(page, 'search open with tag list');
    await expectInputFontSizes(page, 'search open with tag list');
    await page.locator('#item-search').fill('zzz');
    await expectStateOk(page, 'search without matches');
    await page.getByRole('button', { name: 'Clear search' }).click();

    await page.getByRole('button', { name: 'Share' }).click();
    await expect(page.locator('.sharing-panel')).toBeVisible();
    await page.getByRole('button', { name: 'Create invitation link' }).click();
    await expect(page.locator('.sharing-link')).toBeVisible();
    await expectStateOk(page, 'sharing panel with link');
    await expectTapTargets(page, 'sharing panel with link');
    await expectInputFontSizes(page, 'sharing panel with link');
    await expectInside(page.locator('.sharing-link'), page.locator('.sharing-panel'), 'link');

    await page.getByRole('button', { name: 'Delete' }).first().click();
    await expect(page.locator('.dialog')).toBeVisible();
    await expectStateOk(page, 'delete dialog');
    await expectTapTargets(page, 'delete dialog');
    await page.locator('.dialog').getByRole('button', { name: 'Cancel' }).click();
  });

  test('AC-11 / EC-03: a long storage title wraps and the icon buttons move below it', async ({
    page,
  }) => {
    await page.getByRole('button', { name: 'Rename' }).click();
    const field = page.locator('.detail-head .inline-input');
    await field.fill('Vorratskammer im Untergeschoss neben der Waschküche');
    await expectInputFontSizes(page, 'rename open');
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.locator('h1')).toContainText('Vorratskammer');

    await expectStateOk(page, 'long title');
    const title = await box(page.locator('h1'));
    const actions = await box(page.locator('.detail-actions'));
    expect(title.x + title.width).toBeLessThanOrEqual(page.viewportSize()!.width + TOLERANCE);
    expect(actions.y).toBeGreaterThanOrEqual(title.y + title.height - TOLERANCE); // D7: below the title
    expect(actions.x + actions.width).toBeLessThanOrEqual(page.viewportSize()!.width + TOLERANCE);
  });
});

// ── D1: the smallest supported width ─────────────────────────────────────────

test.describe('smallest supported width (320 px)', () => {
  test.use({ viewport: { width: 320, height: 568 } });

  test('AC-01 / AC-02 / AC-04: list, detail, inline edit and add form at 320 px', async ({
    page,
  }) => {
    const storage = await seedStorage(page);

    await page.goto('/storages');
    await expect(page.locator('.storage-card').first()).toBeVisible();
    await expectStateOk(page, '320 storage list');
    await expectTapTargets(page, '320 storage list');

    await page.goto(`/storages/${storage.id}`);
    await expect(page.locator('.item-row')).toHaveCount(5);
    await expectStateOk(page, '320 detail');
    await expectTapTargets(page, '320 detail');
    await expectActionsAligned(page, '320 detail');

    await page
      .locator('.item-row', { hasText: 'Dosentomaten230g' })
      .getByRole('button', { name: 'Edit' })
      .click();
    const card = page.locator('.item-edit');
    await expect(card).toBeVisible();
    await expectStateOk(page, '320 inline edit, ten tags');
    for (const control of [
      '.edit-name',
      '.edit-unit',
      '.edit-production',
      '.item-edit-tags input',
    ]) {
      await expectInside(card.locator(control), card, `320 ${control}`);
    }
    await expectInside(card.getByRole('button', { name: 'Add tag' }), card, '320 Add tag');
    await expectInside(card.getByRole('button', { name: 'Save' }), card, '320 Save');
    await card.getByRole('button', { name: 'Cancel' }).click();

    await page.getByRole('button', { name: 'New item' }).click();
    const form = page.locator('.add-form');
    await expect(form).toBeVisible();
    await expectStateOk(page, '320 add form');
    await expectInside(form.locator('#item-unit'), form, '320 #item-unit');
    await expectInside(form.getByRole('button', { name: 'Add', exact: true }), form, '320 Add');
    await expectInside(form.getByRole('button', { name: 'Cancel' }), form, '320 Cancel');
  });
});

// ── AC-15: the same row contract on a wide screen ─────────────────────────────
// Marcel (G3, 2026-10-24): ✎/🗑 moved to a second line as soon as a row had tags — the
// desktop grid auto-placed its cells. Same seed, same assertions, desktop viewport.

test.describe('wide viewport (1280 px)', () => {
  test.use({ viewport: { width: 1280, height: 800 }, isMobile: false, hasTouch: false });

  test('AC-15: quantity, date and actions keep their column with and without tags', async ({
    page,
  }) => {
    const storage = await seedStorage(page);
    await page.goto(`/storages/${storage.id}`);
    await expect(page.locator('.item-row')).toHaveCount(5);
    await expectStateOk(page, '1280 detail');
    await expectActionsAligned(page, '1280 detail');

    // the chip line hangs under the name; quantity and date stay on the name line
    const tagged = page.locator('.item-row', { hasText: 'Canned tomatoes' });
    const name = await box(tagged.locator('.item-name'));
    const chips = await box(tagged.locator('.item-tags'));
    const qty = await box(tagged.locator('.item-qty'));
    const date = await box(tagged.locator('.item-date'));
    expect(chips.y).toBeGreaterThanOrEqual(name.y + name.height - TOLERANCE);
    expect(Math.abs(chips.x - name.x)).toBeLessThan(TOLERANCE + 1);
    expect(Math.abs(name.y + name.height / 2 - (qty.y + qty.height / 2))).toBeLessThan(qty.height);
    expect(Math.abs(qty.y - date.y)).toBeLessThan(TOLERANCE + 1);
    expect(qty.x).toBeLessThan(date.x);
  });
});
