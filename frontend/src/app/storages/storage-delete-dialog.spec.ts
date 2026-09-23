import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { apiErrorInterceptor } from '../core/api-error.interceptor';
import { TranslateService } from '../core/translate';
import { StorageDeleteDialog } from './storage-delete-dialog';

const TRANSLATIONS = {
  storages: { deleteTitle: 'Delete storage' },
  actions: { cancel: 'Cancel', delete: 'Delete' },
  errors: { generic: 'Something went wrong.' },
  sharing: {
    ownerDelete: {
      message: '"{{name}}" is shared with others. What should happen?',
      choose: 'What should happen?',
      handOver: 'Hand over ownership and leave',
      newOwner: 'New owner',
      deleteForEveryone: 'Delete for everyone',
      confirmHandOver: 'Hand over and leave',
    },
  },
};

const MEMBERS = [
  { userId: 'u1', displayName: 'Olga Owner', isOwner: true, joinedAt: null },
  { userId: 'u2', displayName: 'Max Member', isOwner: false, joinedAt: '2026-09-23T10:00:00Z' },
  { userId: 'u3', displayName: 'Mia Member', isOwner: false, joinedAt: '2026-09-23T11:00:00Z' },
];

describe('StorageDeleteDialog (SPEC-007 AC-20)', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StorageDeleteDialog],
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    document.querySelectorAll('app-storage-delete-dialog').forEach((node) => node.remove());
  });

  async function render(): Promise<{
    fixture: ComponentFixture<StorageDeleteDialog>;
    el: HTMLElement;
  }> {
    const fixture = TestBed.createComponent(StorageDeleteDialog);
    fixture.componentRef.setInput('storageId', 's1');
    fixture.componentRef.setInput('storageName', 'Cellar');
    const el = fixture.nativeElement as HTMLElement;
    document.body.appendChild(el);
    fixture.detectChanges();
    http.expectOne('/api/v1/storages/s1/members').flush(MEMBERS);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, el };
  }

  it('Opens_WithHandOverPreselected_AndTheFirstMemberAsNewOwner', async () => {
    const { el } = await render();

    expect(el.querySelector('#storage-delete-message')?.textContent).toContain(
      '"Cellar" is shared',
    );
    const select = el.querySelector('#storage-delete-new-owner') as HTMLSelectElement;
    expect(Array.from(select.options).map((o) => o.textContent?.trim())).toEqual([
      'Max Member',
      'Mia Member',
    ]);
    expect(select.value).toBe('u2');
    expect(el.querySelector('.btn-danger')?.textContent?.trim()).toBe('Hand over and leave');
    expect((el.querySelector('.btn-danger') as HTMLButtonElement).disabled).toBe(false);
  });

  it('HandOver_WithChosenMember_EmitsThatUserId', async () => {
    const { fixture, el } = await render();
    const handOver = vi.fn();
    const deleteAll = vi.fn();
    fixture.componentInstance.handOver.subscribe(handOver);
    fixture.componentInstance.deleteForEveryone.subscribe(deleteAll);

    const select = el.querySelector('#storage-delete-new-owner') as HTMLSelectElement;
    select.value = 'u3';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    (el.querySelector('.btn-danger') as HTMLButtonElement).click();

    expect(handOver).toHaveBeenCalledWith('u3');
    expect(deleteAll).not.toHaveBeenCalled();
  });

  it('DeleteForEveryone_WhenChosen_EmitsDeleteAndHidesTheMemberPicker', async () => {
    const { fixture, el } = await render();
    const handOver = vi.fn();
    const deleteAll = vi.fn();
    fixture.componentInstance.handOver.subscribe(handOver);
    fixture.componentInstance.deleteForEveryone.subscribe(deleteAll);

    (el.querySelector('input[value="delete"]') as HTMLInputElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(el.querySelector('#storage-delete-new-owner')).toBeNull();
    expect(el.querySelector('.btn-danger')?.textContent?.trim()).toBe('Delete');
    (el.querySelector('.btn-danger') as HTMLButtonElement).click();

    expect(deleteAll).toHaveBeenCalledTimes(1);
    expect(handOver).not.toHaveBeenCalled();
  });

  it('Escape_Cancels', async () => {
    const { fixture } = await render();
    const cancelled = vi.fn();
    fixture.componentInstance.cancelled.subscribe(cancelled);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));

    expect(cancelled).toHaveBeenCalledTimes(1);
  });
});
