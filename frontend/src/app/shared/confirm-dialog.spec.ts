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
  async function render(): Promise<{ fixture: ComponentFixture<ConfirmDialog>; el: HTMLElement }> {
    const fixture = TestBed.createComponent(ConfirmDialog);
    fixture.componentRef.setInput('title', 'Delete storage');
    fixture.componentRef.setInput('message', 'Really delete it?');
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
});
