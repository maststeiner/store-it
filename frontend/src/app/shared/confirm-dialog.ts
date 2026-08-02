import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  input,
  output,
  viewChild,
} from '@angular/core';

import { TranslatePipe } from '../core/translate';

@Component({
  selector: 'app-confirm-dialog',
  imports: [TranslatePipe],
  template: `
    <div class="overlay">
      <div
        #dialog
        class="dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        aria-describedby="confirm-dialog-message"
      >
        <h2 id="confirm-dialog-title" class="dialog-title">{{ title() }}</h2>
        <p id="confirm-dialog-message" class="dialog-message">{{ message() }}</p>
        <div class="dialog-actions">
          <button type="button" class="btn-ghost" (click)="cancelled.emit()">
            {{ 'actions.cancel' | translate }}
          </button>
          <button #confirmButton type="button" class="btn-danger" (click)="confirmed.emit()">
            {{ 'actions.delete' | translate }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ConfirmDialog implements AfterViewInit {
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  private readonly dialog = viewChild.required<ElementRef<HTMLElement>>('dialog');
  private readonly confirmButton =
    viewChild.required<ElementRef<HTMLButtonElement>>('confirmButton');

  ngAfterViewInit(): void {
    // Move focus into the modal so keyboard users land on an actionable control
    this.confirmButton().nativeElement.focus();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.cancelled.emit();
  }

  // Focus trap: keep Tab / Shift+Tab cycling between the dialog's controls
  // so focus can't escape to the (inert) background while the modal is open.
  @HostListener('document:keydown.tab', ['$event'])
  @HostListener('document:keydown.shift.tab', ['$event'])
  protected onTab(event: Event): void {
    const keyEvent = event as KeyboardEvent;
    const focusables = Array.from(
      this.dialog().nativeElement.querySelectorAll<HTMLElement>('button'),
    );
    if (focusables.length === 0) {
      return;
    }
    const first = focusables[0];
    const last = focusables.at(-1)!;
    const active = document.activeElement;

    if (keyEvent.shiftKey && active === first) {
      last.focus();
      event.preventDefault();
    } else if (!keyEvent.shiftKey && active === last) {
      first.focus();
      event.preventDefault();
    }
  }
}
