import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AuthUser } from '../core/auth.service';
import { TranslateService } from '../core/translate';
import { SessionMenu } from './session-menu';

const ALICE: AuthUser = { displayName: 'Alice Example', email: 'alice@example.com' };

describe('SessionMenu', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SessionMenu] }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', {
      auth: {
        session: {
          menu: 'Account menu — signed in as {{name}}',
          logout: 'Sign out',
          deleteAccount: 'Delete account',
        },
      },
    });
  });

  afterEach(() => {
    // Remove hosts appended to document.body so tests stay isolated.
    document.querySelectorAll('app-session-menu').forEach((node) => node.remove());
  });

  // Attach to the document so focus() moves document.activeElement and the
  // document-level click/keydown listeners actually receive the events (jsdom).
  async function render(
    user: AuthUser = ALICE,
  ): Promise<{ fixture: ComponentFixture<SessionMenu>; el: HTMLElement }> {
    const fixture = TestBed.createComponent(SessionMenu);
    fixture.componentRef.setInput('user', user);
    const el = fixture.nativeElement as HTMLElement;
    document.body.appendChild(el);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, el };
  }

  async function openMenu(
    fixture: ComponentFixture<SessionMenu>,
    el: HTMLElement,
  ): Promise<HTMLElement> {
    (el.querySelector('.session-chip') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    return el.querySelector('.session-menu') as HTMLElement;
  }

  it('Header_WhenSignedIn_ShowsOnlyAnInitialsChip', async () => {
    const { el } = await render();

    expect(el.querySelectorAll('button')).toHaveLength(1);
    expect(el.querySelector('.session-chip')?.textContent?.trim()).toBe('AE');
    expect(el.querySelector('.session-menu')).toBeNull();
  });

  it('Initials_WhenOnlyEmailKnown_DerivesFromTheEmail', async () => {
    const { el } = await render({ displayName: null, email: 'bob.builder@example.com' });

    expect(el.querySelector('.session-chip')?.textContent?.trim()).toBe('BB');
  });

  it('Initials_WhenNeitherNameNorEmailKnown_FallsBackToAPlaceholder', async () => {
    const { el } = await render({ displayName: null, email: null });

    expect(el.querySelector('.session-chip')?.textContent?.trim()).toBe('?');
  });

  it('Menu_WhenNoEmailKnown_OmitsTheEmailLine', async () => {
    const { fixture, el } = await render({ displayName: 'Alice Example', email: null });
    const menu = await openMenu(fixture, el);

    expect(menu.querySelector('.session-name')?.textContent).toContain('Alice Example');
    expect(menu.querySelector('.session-email')).toBeNull();
  });

  it('Chip_WhenRendered_CarriesMenuButtonSemantics', async () => {
    const { fixture, el } = await render();
    const chip = el.querySelector('.session-chip') as HTMLButtonElement;

    expect(chip.getAttribute('aria-haspopup')).toBe('menu');
    expect(chip.getAttribute('aria-controls')).toBe('session-menu');
    expect(chip.getAttribute('aria-expanded')).toBe('false');
    expect(chip.getAttribute('aria-label')).toBe('Account menu — signed in as Alice Example');

    await openMenu(fixture, el);
    expect(chip.getAttribute('aria-expanded')).toBe('true');
  });

  it('Menu_WhenChipActivated_ShowsNameEmailAndSignOut', async () => {
    const { fixture, el } = await render();
    const menu = await openMenu(fixture, el);

    expect(menu.getAttribute('role')).toBe('menu');
    expect(menu.getAttribute('aria-labelledby')).toBe('session-chip');
    expect(menu.querySelector('.session-name')?.textContent).toContain('Alice Example');
    expect(menu.querySelector('.session-email')?.textContent).toContain('alice@example.com');
    expect(menu.querySelector('[role="menuitem"]')?.textContent?.trim()).toBe('Sign out');
  });

  it('Menu_WhenOpened_MovesFocusToTheFirstItem', async () => {
    const { fixture, el } = await render();
    const menu = await openMenu(fixture, el);

    expect(document.activeElement).toBe(menu.querySelector('[role="menuitem"]'));
  });

  it('Menu_WhenEscapePressed_ClosesAndReturnsFocusToTheChip', async () => {
    const { fixture, el } = await render();
    await openMenu(fixture, el);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelector('.session-menu')).toBeNull();
    expect(document.activeElement).toBe(el.querySelector('.session-chip'));
  });

  // Two items (SPEC-006 added "Delete account"): from the first item, ArrowDown and End land
  // on the last, ArrowUp wraps to the last, Home stays on the first — and none scrolls the page.
  it.each([
    ['ArrowDown', 1],
    ['ArrowUp', 1],
    ['Home', 0],
    ['End', 1],
  ])('Menu_When%sPressed_MovesFocusToItem%iAndSuppressesScrolling', async (key, expected) => {
    const { fixture, el } = await render();
    const menu = await openMenu(fixture, el);
    const items = Array.from(menu.querySelectorAll<HTMLElement>('[role="menuitem"]'));

    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
    items[0].dispatchEvent(event);

    expect(document.activeElement).toBe(items[expected]);
    expect(event.defaultPrevented).toBe(true);
  });

  it('Menu_WhenOpened_ListsSignOutThenDeleteAccountAsDestructive', async () => {
    const { fixture, el } = await render();
    const menu = await openMenu(fixture, el);
    const items = Array.from(menu.querySelectorAll<HTMLElement>('[role="menuitem"]'));

    expect(items.map((item) => item.textContent?.trim())).toEqual(['Sign out', 'Delete account']);
    expect(items[1].classList.contains('session-menu-item-danger')).toBe(true);
    expect(items[0].classList.contains('session-menu-item-danger')).toBe(false);
  });

  it('DeleteAccount_WhenChosen_EmitsAndClosesTheMenu', async () => {
    const { fixture, el } = await render();
    const deleteAccount = vi.fn();
    const signOut = vi.fn();
    fixture.componentInstance.deleteAccount.subscribe(deleteAccount);
    fixture.componentInstance.signOut.subscribe(signOut);
    const menu = await openMenu(fixture, el);

    (menu.querySelectorAll<HTMLElement>('[role="menuitem"]')[1] as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(deleteAccount).toHaveBeenCalledTimes(1);
    expect(signOut).not.toHaveBeenCalled();
    expect(el.querySelector('.session-menu')).toBeNull();
  });

  it('Menu_WhenAnUnrelatedKeyPressed_LeavesTheEventAlone', async () => {
    const { fixture, el } = await render();
    const menu = await openMenu(fixture, el);

    const event = new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true });
    (menu.querySelector('[role="menuitem"]') as HTMLElement).dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
    expect(el.querySelector('.session-menu')).not.toBeNull();
  });

  it('Menu_WhenTabPressed_ClosesAndReturnsFocusToTheChip', async () => {
    const { fixture, el } = await render();
    const menu = await openMenu(fixture, el);

    (menu.querySelector('[role="menuitem"]') as HTMLElement).dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }),
    );
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelector('.session-menu')).toBeNull();
    expect(document.activeElement).toBe(el.querySelector('.session-chip'));
  });

  it('Menu_WhenClickedOutside_Closes', async () => {
    const { fixture, el } = await render();
    await openMenu(fixture, el);

    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelector('.session-menu')).toBeNull();
  });

  it('Menu_WhenChipActivatedAgain_Closes', async () => {
    const { fixture, el } = await render();
    await openMenu(fixture, el);

    (el.querySelector('.session-chip') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelector('.session-menu')).toBeNull();
  });

  it.each(['ArrowDown', 'ArrowUp'])('Chip_When%sPressed_OpensTheMenu', async (key) => {
    const { fixture, el } = await render();
    const chip = el.querySelector('.session-chip') as HTMLButtonElement;

    chip.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelector('.session-menu')).not.toBeNull();
  });

  it('Escape_WhenTheMenuIsClosed_ChangesNothing', async () => {
    const { fixture, el } = await render();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelector('.session-menu')).toBeNull();
    expect(el.querySelector('.session-chip')?.getAttribute('aria-expanded')).toBe('false');
  });

  it('SignOut_WhenActivated_EmitsSignOutAndClosesTheMenu', async () => {
    const { fixture, el } = await render();
    const menu = await openMenu(fixture, el);
    let signedOut = 0;
    fixture.componentInstance.signOut.subscribe(() => (signedOut += 1));

    (menu.querySelector('[role="menuitem"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(signedOut).toBe(1);
    expect(el.querySelector('.session-menu')).toBeNull();
  });
});
