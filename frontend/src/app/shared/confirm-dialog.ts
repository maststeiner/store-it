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
      <div class="dialog" role="alertdialog" aria-modal="true" [attr.aria-label]="title()">
        <h2 class="dialog-title">{{ title() }}</h2>
        <p class="dialog-message">{{ message() }}</p>
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
}
