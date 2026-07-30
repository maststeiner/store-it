import { TestBed } from '@angular/core/testing';

import { TranslateService } from '../core/translate';
import { ConfirmDialog } from './confirm-dialog';

describe('ConfirmDialog', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ConfirmDialog] }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', {
      actions: { cancel: 'Cancel', delete: 'Delete' },
    });
  });

  it('wires the title and message to the dialog via aria-labelledby/describedby', async () => {
    const fixture = TestBed.createComponent(ConfirmDialog);
    fixture.componentRef.setInput('title', 'Delete storage');
    fixture.componentRef.setInput('message', 'Really delete it?');
    fixture.detectChanges();
    await fixture.whenStable();

    const el = fixture.nativeElement as HTMLElement;
    const dialog = el.querySelector('[role="alertdialog"]')!;

    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-labelledby')).toBe('confirm-dialog-title');
    expect(dialog.getAttribute('aria-describedby')).toBe('confirm-dialog-message');
    expect(el.querySelector('#confirm-dialog-title')?.textContent).toContain('Delete storage');
    expect(el.querySelector('#confirm-dialog-message')?.textContent).toContain('Really delete it?');
  });
});
