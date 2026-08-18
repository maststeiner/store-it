import {
  Component,
  ElementRef,
  HostListener,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';

import { AuthUser } from '../core/auth.service';
import { TranslatePipe } from '../core/translate';

/**
 * Header control for the signed-in session: an initials chip that opens a menu
 * carrying the full identity and the sign-out action.
 *
 * The identity deliberately lives behind a click rather than a hover tooltip —
 * tooltips do not exist on touch devices, rarely surface on keyboard focus and
 * are announced inconsistently by screen readers, which would leave the account
 * information unreachable for those users.
 */
@Component({
  selector: 'app-session-menu',
  imports: [TranslatePipe],
  template: `
    <button
      #chip
      type="button"
      class="session-chip"
      id="session-chip"
      aria-haspopup="menu"
      aria-controls="session-menu"
      [attr.aria-expanded]="open()"
      [attr.aria-label]="'auth.session.menu' | translate: { name: name() }"
      (click)="toggle()"
      (keydown.arrowdown)="openMenu($event)"
      (keydown.arrowup)="openMenu($event)"
    >
      <span class="session-initials" aria-hidden="true">{{ initials() }}</span>
    </button>

    @if (open()) {
      <div id="session-menu" class="session-menu" role="menu" aria-labelledby="session-chip">
        <div class="session-identity" role="none">
          <span class="session-name">{{ name() }}</span>
          @if (user().email; as email) {
            <span class="session-email">{{ email }}</span>
          }
        </div>
        <button
          #item
          type="button"
          role="menuitem"
          tabindex="-1"
          class="session-menu-item"
          (click)="emitSignOut()"
          (keydown)="onMenuKeydown($event)"
        >
          {{ 'auth.session.logout' | translate }}
        </button>
      </div>
    }
  `,
})
export class SessionMenu {
  readonly user = input.required<AuthUser>();
  readonly signOut = output<void>();

  protected readonly open = signal(false);

  /** Falls back to the e-mail, then to a placeholder, so the chip is never blank. */
  protected readonly name = computed(() => {
    const user = this.user();
    return user.displayName?.trim() || user.email?.trim() || '';
  });

  protected readonly initials = computed(() => {
    const user = this.user();
    const displayName = user.displayName?.trim();
    if (displayName) {
      return initialsOf(displayName.split(/\s+/));
    }
    // Only the local part carries a person's name; the domain would add noise.
    const localPart = user.email?.trim().split('@')[0];
    return localPart ? initialsOf(localPart.split(/[._-]+/)) : '?';
  });

  private readonly chip = viewChild.required<ElementRef<HTMLButtonElement>>('chip');
  private readonly items = viewChildren<ElementRef<HTMLButtonElement>>('item');
  private readonly host = inject(ElementRef<HTMLElement>);

  constructor() {
    // Move focus into the menu once it has rendered, so keyboard users land on
    // an actionable control instead of the (now inert) chip.
    effect(() => {
      if (this.open()) {
        this.items()[0]?.nativeElement.focus();
      }
    });
  }

  protected toggle(): void {
    this.open.update((open) => !open);
  }

  protected openMenu(event: Event): void {
    // Arrow keys open the menu rather than scrolling the page behind it.
    event.preventDefault();
    this.open.set(true);
  }

  protected emitSignOut(): void {
    this.open.set(false);
    this.signOut.emit();
  }

  protected onMenuKeydown(event: KeyboardEvent): void {
    const items = this.items().map((item) => item.nativeElement);
    if (items.length === 0) {
      return;
    }
    const current = items.indexOf(document.activeElement as HTMLButtonElement);

    switch (event.key) {
      case 'ArrowDown':
        items[(current + 1) % items.length].focus();
        break;
      case 'ArrowUp':
        items[(current - 1 + items.length) % items.length].focus();
        break;
      case 'Home':
        items[0].focus();
        break;
      case 'End':
        items.at(-1)!.focus();
        break;
      case 'Tab':
        // Close and hand focus back to the chip *without* preventing the default:
        // the browser then continues its Tab traversal from the chip, which is
        // where focus would have been had the menu never opened.
        this.close();
        return;
      default:
        return;
    }
    event.preventDefault();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.open()) {
      this.close();
    }
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: Event): void {
    const host = this.host.nativeElement as HTMLElement;
    if (this.open() && !host.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  private close(): void {
    this.open.set(false);
    this.chip().nativeElement.focus();
  }
}

/**
 * The leading letters of the first and last word — one letter when a single word
 * is all we have. Derived display data, not translatable copy.
 */
function initialsOf(words: string[]): string {
  const parts = words.filter(Boolean);
  if (parts.length === 0) {
    return '?';
  }
  const first = parts[0][0];
  const last = parts.length > 1 ? parts.at(-1)![0] : '';
  return (first + last).toUpperCase();
}
