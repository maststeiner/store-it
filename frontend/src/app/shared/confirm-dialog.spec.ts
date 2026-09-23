import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TranslateService } from '../core/translate';
import { ConfirmDialog } from './confirm-dialog';

describe('ConfirmDialog', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ConfirmDialog] }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', {
      actions: { cancel: 'Cancel', delete: 'Delete' },
    });
  });

  afterEach(() => {
    // Remove dialog hosts appended to document.body so tests stay isolated.
    document.querySelectorAll('app-confirm-dialog').forEach((node) => node.remove());
  });

  // Attach to the document so focus() actually moves document.activeElement (jsdom).
  async function render(
    challenge?: string,
  ): Promise<{ fixture: ComponentFixture<ConfirmDialog>; el: HTMLElement }> {
    const fixture = TestBed.createComponent(ConfirmDialog);
    fixture.componentRef.setInput('title', 'Delete storage');
    fixture.componentRef.setInput('message', 'Really delete it?');
    if (challenge !== undefined) {
      fixture.componentRef.setInput('challenge', challenge);
      fixture.componentRef.setInput('challengeLabel', 'Type your e-mail address to confirm');
    }
    const el = fixture.nativeElement as HTMLElement;
    document.body.appendChild(el);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, el };
  }

  function keydown(key: string, opts: Partial<KeyboardEventInit> = {}): void {
    document.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, ...opts }));
  }

  it('wires the title and message to the dialog via aria-labelledby/describedby', async () => {
    const { el } = await render();
    const dialog = el.querySelector('[role="alertdialog"]') as HTMLElement;

    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-labelledby')).toBe('confirm-dialog-title');
    expect(dialog.getAttribute('aria-describedby')).toBe('confirm-dialog-message');
    expect(el.querySelector('#confirm-dialog-title')?.textContent).toContain('Delete storage');
    expect(el.querySelector('#confirm-dialog-message')?.textContent).toContain('Really delete it?');
  });

  it('moves focus to the confirm (delete) button on open', async () => {
    const { el } = await render();
    expect(document.activeElement).toBe(el.querySelector('.btn-danger'));
  });

  it('emits cancelled on Escape', async () => {
    const { fixture } = await render();
    let cancelled = false;
    fixture.componentInstance.cancelled.subscribe(() => (cancelled = true));
    keydown('Escape');
    expect(cancelled).toBe(true);
  });

  it('traps Tab from the last control back to the first', async () => {
    const { el } = await render();
    const cancel = el.querySelector('.btn-ghost') as HTMLElement;
    const confirm = el.querySelector('.btn-danger') as HTMLElement;

    confirm.focus();
    keydown('Tab');
    expect(document.activeElement).toBe(cancel);
  });

  it('traps Shift+Tab from the first control back to the last', async () => {
    const { el } = await render();
    const cancel = el.querySelector('.btn-ghost') as HTMLElement;
    const confirm = el.querySelector('.btn-danger') as HTMLElement;

    cancel.focus();
    keydown('Tab', { shiftKey: true });
    expect(document.activeElement).toBe(confirm);
  });

  // SPEC-006 D4 / AC-09: an optional typed challenge gates the confirm button.
  describe('with a typed challenge', () => {
    async function type(fixture: ComponentFixture<ConfirmDialog>, el: HTMLElement, value: string) {
      const input = el.querySelector('#confirm-dialog-challenge') as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      await fixture.whenStable();
    }

    it('Challenge_WhenSet_ShowsALabelledFieldAndDisablesConfirm', async () => {
      const { el } = await render('Alice@Example.com');
      const input = el.querySelector('#confirm-dialog-challenge') as HTMLInputElement;

      expect(input).not.toBeNull();
      expect(el.querySelector('label[for="confirm-dialog-challenge"]')?.textContent).toContain(
        'Type your e-mail address to confirm',
      );
      expect((el.querySelector('.btn-danger') as HTMLButtonElement).disabled).toBe(true);
      expect(document.activeElement).toBe(input);
    });

    it('Challenge_WhenTypedWrong_KeepsConfirmDisabledAndEmitsNothing', async () => {
      const { fixture, el } = await render('alice@example.com');
      const confirmed = vi.fn();
      fixture.componentInstance.confirmed.subscribe(confirmed);

      await type(fixture, el, 'alice@example.co');
      const button = el.querySelector('.btn-danger') as HTMLButtonElement;
      button.click();

      expect(button.disabled).toBe(true);
      expect(confirmed).not.toHaveBeenCalled();
    });

    it('Challenge_WhenTypedIgnoringCaseAndWhitespace_EnablesConfirmAndEmits', async () => {
      const { fixture, el } = await render('alice@example.com');
      const confirmed = vi.fn();
      fixture.componentInstance.confirmed.subscribe(confirmed);

      await type(fixture, el, '  Alice@Example.COM ');
      const button = el.querySelector('.btn-danger') as HTMLButtonElement;
      expect(button.disabled).toBe(false);
      button.click();

      expect(confirmed).toHaveBeenCalledTimes(1);
    });

    it('Challenge_WhenAbsent_LeavesTheDialogAsBefore', async () => {
      const { el } = await render();

      expect(el.querySelector('#confirm-dialog-challenge')).toBeNull();
      expect((el.querySelector('.btn-danger') as HTMLButtonElement).disabled).toBe(false);
    });
  });
});
