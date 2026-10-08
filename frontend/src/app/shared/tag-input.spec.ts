import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TranslateService } from '../core/translate';
import { TagInput } from './tag-input';

// SPEC-011 AC-08 … AC-10 (D13) — the tag control on its own.
describe('TagInput', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [TagInput] }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', {
      items: {
        tags: {
          label: 'Tags',
          placeholder: 'Add a tag…',
          remove: 'Remove tag {{name}}',
          limit: 'At most 10 tags per item.',
        },
      },
    });
  });

  afterEach(() => document.querySelectorAll('app-tag-input').forEach((n) => n.remove()));

  function render(tags: string[] = [], suggestions: string[] = []) {
    const fixture = TestBed.createComponent(TagInput);
    fixture.componentRef.setInput('tags', tags);
    fixture.componentRef.setInput('suggestions', suggestions);
    const el = fixture.nativeElement as HTMLElement;
    document.body.appendChild(el);
    fixture.detectChanges();
    const input = el.querySelector('input') as HTMLInputElement;
    return { fixture, el, input };
  }

  function typeText(fixture: ComponentFixture<TagInput>, input: HTMLInputElement, value: string) {
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  }

  function press(fixture: ComponentFixture<TagInput>, input: HTMLInputElement, key: string) {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
    input.dispatchEvent(event);
    fixture.detectChanges();
    return event;
  }

  const chips = (el: HTMLElement) =>
    [...el.querySelectorAll('.tag-chip-editable > span')].map((s) => s.textContent?.trim());
  const options = (el: HTMLElement) =>
    [...el.querySelectorAll('[role="option"]')].map((o) => o.textContent?.trim());

  it('AC-08: shows the item tags as chips with a remove button each', () => {
    const { fixture, el } = render(['Dosen', 'homemade']);

    expect(chips(el)).toEqual(['Dosen', 'homemade']);
    (el.querySelector('[aria-label="Remove tag Dosen"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.tags()).toEqual(['homemade']);
  });

  it('AC-09: Enter adds the typed text as a tag and clears the field', () => {
    const { fixture, el, input } = render();

    typeText(fixture, input, '  selbst   gemacht ');
    const enter = press(fixture, input, 'Enter');

    expect(enter.defaultPrevented).toBe(true); // never submits the surrounding form
    expect(fixture.componentInstance.tags()).toEqual(['selbst gemacht']);
    expect(input.value).toBe('');
    expect(chips(el)).toEqual(['selbst gemacht']);
  });

  it('AC-09 / EC-09: a comma separates tags while typing', () => {
    const { fixture, input } = render();

    typeText(fixture, input, 'Dosen, bio,');

    expect(fixture.componentInstance.tags()).toEqual(['Dosen', 'bio']);
    expect(input.value).toBe('');
  });

  it('AC-09: lists existing tags that match the typed text and are not yet assigned', () => {
    const { fixture, el, input } = render(['Dosen'], ['Dosen', 'homemade', 'Bio', 'Vorrat']);

    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    expect(options(el)).toEqual(['homemade', 'Bio', 'Vorrat']);

    typeText(fixture, input, 'o');
    expect(options(el)).toEqual(['homemade', 'Bio', 'Vorrat']);

    typeText(fixture, input, 'vo');
    expect(options(el)).toEqual(['Vorrat']);
  });

  it('AC-09: a suggestion is picked with the arrow keys and Enter, or with the mouse', () => {
    const { fixture, el, input } = render([], ['Dosen', 'homemade']);

    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    press(fixture, input, 'ArrowDown');
    press(fixture, input, 'ArrowDown');
    expect(input.getAttribute('aria-activedescendant')).toBe('item-tags-option-1');
    press(fixture, input, 'Enter');
    expect(fixture.componentInstance.tags()).toEqual(['homemade']);

    const dosen = el.querySelector('[role="option"]') as HTMLElement;
    dosen.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
    expect(fixture.componentInstance.tags()).toEqual(['homemade', 'Dosen']);
  });

  it('D4: typing an existing tag in another case uses the storage spelling, duplicates are ignored', () => {
    const { fixture, input } = render(['Dosen'], ['Dosen', 'homemade']);

    typeText(fixture, input, 'HOMEMADE');
    press(fixture, input, 'Enter');
    typeText(fixture, input, 'dosen');
    press(fixture, input, 'Enter');

    expect(fixture.componentInstance.tags()).toEqual(['Dosen', 'homemade']);
  });

  it('AC-10: at ten tags the input is disabled and the limit hint is shown', () => {
    const ten = Array.from({ length: 10 }, (_, i) => `t${i + 1}`);
    const { el, input } = render(ten);

    expect(input.disabled).toBe(true);
    expect(el.querySelector('.form-hint')?.textContent?.trim()).toBe('At most 10 tags per item.');
  });

  it('D13: Escape closes the suggestions and stays inside the control; otherwise it bubbles', () => {
    const { fixture, el, input } = render([], ['Dosen']);
    const seen: string[] = [];
    document.addEventListener('keydown', () => seen.push('document'), { once: true });

    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    expect(el.querySelector('[role="listbox"]')).not.toBeNull();
    press(fixture, input, 'Escape');
    expect(el.querySelector('[role="listbox"]')).toBeNull();
    expect(seen).toEqual([]);

    press(fixture, input, 'Escape');
    expect(seen).toEqual(['document']);
  });

  it('keeps typed text as a tag when focus leaves the field', () => {
    const { fixture, input } = render();

    typeText(fixture, input, 'Vorrat');
    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(fixture.componentInstance.tags()).toEqual(['Vorrat']);
  });

  it('Backspace in an empty field removes the last chip', () => {
    const { fixture, input } = render(['Dosen', 'bio']);

    press(fixture, input, 'Backspace');

    expect(fixture.componentInstance.tags()).toEqual(['Dosen']);
  });
});
