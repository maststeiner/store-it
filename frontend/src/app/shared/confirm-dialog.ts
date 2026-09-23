import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  computed,
  input,
  output,
  signal,
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
        @if (challenge(); as expected) {
          <div class="field dialog-challenge">
            <label for="confirm-dialog-challenge">{{ challengeLabel() }}</label>
            <input
              #challengeInput
              id="confirm-dialog-challenge"
              type="text"
              autocomplete="off"
              spellcheck="false"
              [attr.placeholder]="expected"
              [value]="typed()"
              (input)="typed.set($any($event.target).value)"
            />
          </div>
        }
        <div class="dialog-actions">
          <button type="button" class="btn-ghost" (click)="cancelled.emit()">
            {{ 'actions.cancel' | translate }}
          </button>
          <button
            #confirmButton
            type="button"
            class="btn-danger"
            [disabled]="!canConfirm()"
            (click)="confirm()"
          >
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
  /**
   * SPEC-006 D4: when set, the user has to type this value (their e-mail address) before the
   * confirm button becomes active. Compared case-insensitively and trimmed. Existing callers
   * (storage / item deletion) pass nothing and get the plain two-button dialog as before.
   */
  readonly challenge = input<string | null>(null);
  /** Label of the challenge field, e.g. "Type your e-mail address to confirm". */
  readonly challengeLabel = input<string>('');
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected readonly typed = signal('');
  protected readonly canConfirm = computed(() => {
    const expected = this.challenge();
    return expected === null || normalise(this.typed()) === normalise(expected);
  });

  private readonly dialog = viewChild.required<ElementRef<HTMLElement>>('dialog');
  private readonly confirmButton =
    viewChild.required<ElementRef<HTMLButtonElement>>('confirmButton');
  private readonly challengeInput = viewChild<ElementRef<HTMLInputElement>>('challengeInput');

  ngAfterViewInit(): void {
    // Move focus into the modal so keyboard users land on an actionable control: the
    // challenge field when there is one (the button is inert until it matches), else the button.
    (this.challengeInput()?.nativeElement ?? this.confirmButton().nativeElement).focus();
  }

  protected confirm(): void {
    // The button is disabled while the challenge is unmet; this guard covers programmatic clicks.
    if (this.canConfirm()) {
      this.confirmed.emit();
    }
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
      this.dialog().nativeElement.querySelectorAll<HTMLElement>('button:not([disabled]), input'),
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

function normalise(value: string): string {
  return value.trim().toLowerCase();
}
