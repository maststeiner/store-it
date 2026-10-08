import { Component, ElementRef, computed, input, model, signal, viewChild } from '@angular/core';

import { TranslatePipe } from '../core/translate';

/** SPEC-011 D4: at most this many tags per item — the input locks at the limit (AC-10). */
export const MAX_TAGS_PER_ITEM = 10;

/**
 * SPEC-011 D13 / AC-08…AC-10: the tag control of the item forms. Chips with ✕ above a
 * combobox; typing lists the storage's existing tags that are not on the item yet; the *Add* button, Enter or
 * `,` adds the typed text (a new or an existing tag), selecting a suggestion adds it. Identity
 * of a tag is case-insensitive (the API decides the spelling, D4/D8); the control only avoids
 * obvious duplicates while typing.
 */
@Component({
  selector: 'app-tag-input',
  imports: [TranslatePipe],
  template: `
    @if (tags().length > 0) {
      <ul class="tag-chips" aria-live="polite">
        @for (tag of tags(); track tag) {
          <li class="tag-chip tag-chip-editable">
            <span>{{ tag }}</span>
            <button
              type="button"
              class="tag-chip-remove"
              (click)="remove(tag)"
              [attr.aria-label]="'items.tags.remove' | translate: { name: tag }"
            >
              ✕
            </button>
          </li>
        }
      </ul>
    }
    <div class="tag-input">
      <input
        #field
        [id]="inputId()"
        class="inline-input"
        type="text"
        autocomplete="off"
        role="combobox"
        aria-autocomplete="list"
        [attr.aria-expanded]="listOpen()"
        [attr.aria-controls]="inputId() + '-list'"
        [attr.aria-activedescendant]="activeId()"
        [disabled]="full()"
        [placeholder]="(full() ? 'items.tags.limit' : 'items.tags.placeholder') | translate"
        [attr.aria-label]="'items.tags.label' | translate"
        [value]="draft()"
        (input)="onInput($event)"
        (focus)="listOpen.set(true)"
        (blur)="onBlur()"
        (keydown)="onKeydown($event)"
      />
      <button
        type="button"
        class="btn-ghost btn-small tag-add"
        [disabled]="full() || draft().trim() === ''"
        (mousedown)="$event.preventDefault()"
        (click)="addDraft()"
      >
        {{ 'items.tags.add' | translate }}
      </button>
      @if (listOpen() && suggestionsToShow().length > 0) {
        <ul class="tag-suggestions" role="listbox" [id]="inputId() + '-list'">
          @for (suggestion of suggestionsToShow(); track suggestion; let i = $index) {
            <li
              role="option"
              [id]="inputId() + '-option-' + i"
              [attr.aria-selected]="i === activeIndex()"
              [class.active]="i === activeIndex()"
              (mousedown)="pick($event, suggestion)"
            >
              {{ suggestion }}
            </li>
          }
        </ul>
      }
    </div>
    @if (full()) {
      <p class="form-hint">{{ 'items.tags.limit' | translate }}</p>
    }
  `,
})
export class TagInput {
  /** The item's tags, two-way bound to the form model. */
  readonly tags = model<string[]>([]);
  /** The storage's existing tags (canonical spelling), for the suggestion list (AC-09). */
  readonly suggestions = input<string[]>([]);
  readonly inputId = input('item-tags');

  protected readonly draft = signal('');
  protected readonly listOpen = signal(false);
  protected readonly activeIndex = signal(-1);
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  protected readonly full = computed(() => this.tags().length >= MAX_TAGS_PER_ITEM);

  /** Existing tags not yet on the item that contain the typed text (case-insensitive). */
  protected readonly suggestionsToShow = computed(() => {
    const typed = this.draft().trim().toLowerCase();
    const assigned = new Set(this.tags().map((tag) => tag.toLowerCase()));
    return this.suggestions().filter(
      (tag) => !assigned.has(tag.toLowerCase()) && tag.toLowerCase().includes(typed),
    );
  });

  protected readonly activeId = computed(() =>
    this.activeIndex() >= 0 ? `${this.inputId()}-option-${this.activeIndex()}` : null,
  );

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    // EC-09: a comma is the separator — everything before it becomes a tag right away.
    if (value.includes(',')) {
      const parts = value.split(',');
      const rest = parts.pop() ?? '';
      parts.forEach((part) => this.add(part));
      this.draft.set(rest);
      this.field().nativeElement.value = rest;
    } else {
      this.draft.set(value);
    }
    this.listOpen.set(true);
    this.activeIndex.set(-1);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const options = this.suggestionsToShow();
    switch (event.key) {
      case 'Enter':
        // Never submit the surrounding form from the tag field.
        event.preventDefault();
        if (this.activeIndex() >= 0 && options[this.activeIndex()]) {
          this.add(options[this.activeIndex()]);
        } else {
          this.add(this.draft());
        }
        this.clearDraft();
        break;
      case 'ArrowDown':
        if (options.length > 0) {
          event.preventDefault();
          this.listOpen.set(true);
          this.activeIndex.set((this.activeIndex() + 1) % options.length);
        }
        break;
      case 'ArrowUp':
        if (options.length > 0) {
          event.preventDefault();
          this.activeIndex.set((this.activeIndex() - 1 + options.length) % options.length);
        }
        break;
      case 'Escape':
        // D13: Escape closes the suggestions only; with the list closed it belongs to the page
        // (the add form closes on Escape, SPEC-010 D6).
        if (this.listOpen()) {
          event.stopPropagation();
          this.listOpen.set(false);
          this.activeIndex.set(-1);
        }
        break;
      case 'Backspace':
        if (this.draft() === '' && this.tags().length > 0) {
          this.remove(this.tags().at(-1)!);
        }
        break;
      default:
        return;
    }
  }

  protected pick(event: MouseEvent, suggestion: string): void {
    // mousedown, not click: the input's blur must not close the list before the pick lands.
    event.preventDefault();
    this.add(suggestion);
    this.clearDraft();
    this.field().nativeElement.focus();
  }

  /** D13 (amendment A1): the explicit add — same as Enter, visible as a button. */
  protected addDraft(): void {
    this.add(this.draft());
    this.clearDraft();
    this.field().nativeElement.focus();
  }

  protected onBlur(): void {
    // Leaving the field only closes the list; text stays in the field until it is added
    // explicitly (button, Enter or comma) — nothing is committed behind the user's back.
    this.listOpen.set(false);
  }

  protected remove(tag: string): void {
    this.tags.set(this.tags().filter((existing) => existing !== tag));
  }

  private add(raw: string): void {
    const value = raw.trim().replace(/\s+/g, ' ');
    if (value === '' || this.full()) {
      return;
    }
    const lower = value.toLowerCase();
    if (this.tags().some((tag) => tag.toLowerCase() === lower)) {
      return;
    }
    // Prefer the storage's spelling when the typed text matches an existing tag (D4).
    const existing = this.suggestions().find((tag) => tag.toLowerCase() === lower);
    this.tags.set([...this.tags(), existing ?? value]);
  }

  private clearDraft(): void {
    this.draft.set('');
    this.field().nativeElement.value = '';
    this.activeIndex.set(-1);
  }
}
