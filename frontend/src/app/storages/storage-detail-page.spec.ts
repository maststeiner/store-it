import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';

import { ItemResponse, StorageResponse } from '../api/models';
import { apiErrorInterceptor } from '../core/api-error.interceptor';
import { TranslateService } from '../core/translate';
import { StorageDetailPage } from './storage-detail-page';

const TRANSLATIONS = {
  nav: { storages: 'My storages' },
  actions: {
    add: 'Add',
    save: 'Save',
    cancel: 'Cancel',
    rename: 'Rename',
    delete: 'Delete',
    edit: 'Edit',
  },
  items: {
    new: 'New item',
    empty: 'No items yet.',
    tags: {
      label: 'Tags',
      placeholder: 'Add a tag…',
      remove: 'Remove tag {{name}}',
      limit: 'At most 10 tags per item.',
      filterBy: 'Show only items tagged {{name}}',
      clearFilter: 'Clear tag filter',
    },
    search: {
      toggle: 'Search items',
      label: 'Search items by name',
      placeholder: 'Search…',
      clear: 'Clear search',
      result: { one: '1 of {{total}} items', other: '{{shown}} of {{total}} items' },
      noMatches: 'No items match “{{query}}”.',
    },
    producedOn: 'prod. {{date}}',
    form: {
      name: 'Item',
      namePlaceholder: 'e.g. frozen peas',
      amount: 'Amount',
      unit: 'Unit',
      expiry: 'Expiry date',
      production: 'Production date',
      dateHint: 'Provide at least one date.',
    },
  },
  groups: { expired: 'Expired', expiring: 'Expiring soon', others: 'Others' },
  units: { Piece: 'pcs', Gram: 'g', Kilogram: 'kg', Milliliter: 'ml', Liter: 'l', Pack: 'pack' },
  storages: {
    deleteTitle: 'Delete storage',
    deleteConfirm: 'Delete "{{name}}"?',
    namePlaceholder: 'Name',
  },
  errors: {
    generic: 'Something went wrong.',
    item: {
      dates: { missing: 'One date required.' },
      tags: { tooMany: 'At most 10 tags.', tooLong: 'Tag too long.' },
    },
  },
  sharing: {
    share: 'Share',
    leave: 'Leave',
    leaveTitle: 'Leave storage',
    leaveConfirm: 'Leave "{{name}}"?',
    sharedBy: 'Shared by {{name}}',
    members: {
      title: 'Members',
      owner: 'Owner',
      remove: 'Remove',
      none: 'Nobody but you yet.',
      makeOwner: 'Make owner',
      makeOwnerNamed: 'Make {{name}} the owner',
    },
    makeOwner: { title: 'Hand over', message: '{{name}} becomes the owner.' },
    link: { none: 'No active invitation link.', create: 'Create invitation link', warning: 'w' },
    ownerDelete: {
      message: '"{{name}}" is shared. What should happen?',
      choose: 'c',
      handOver: 'Hand over and leave',
      newOwner: 'New owner',
      deleteForEveryone: 'Delete for everyone',
      confirmHandOver: 'Hand over and leave',
    },
  },
};

function item(partial: Partial<ItemResponse>): ItemResponse {
  return {
    id: 'i1',
    name: 'Item',
    amount: 1,
    tags: [],
    unit: 'Piece',
    expiryDate: null,
    productionDate: null,
    expiryStatus: 'Ok',
    ...partial,
  };
}

describe('StorageDetailPage', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StorageDetailPage],
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 's1' }) } },
        },
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
    http = TestBed.inject(HttpTestingController);
  });

  /** #182: the add form is closed by default; open it like a user would. */
  async function openAddForm(fixture: ComponentFixture<StorageDetailPage>): Promise<void> {
    const element = fixture.nativeElement as HTMLElement;
    (
      element.querySelector('.detail-head .icon-btn[aria-label="New item"]') as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    await fixture.whenStable();
  }

  function flushInitialLoad(items: ItemResponse[], sharing: Partial<StorageResponse> = {}) {
    http.expectOne('/api/v1/storages/s1').flush({
      id: 's1',
      name: 'Freezer',
      itemCount: items.length,
      expiredCount: 0,
      expiringSoonCount: 0,
      isOwner: true,
      memberCount: 0,
      ownerName: 'Me',
      ...sharing,
    });
    http.expectOne('/api/v1/storages/s1/items').flush(items);
    http.expectOne('/api/v1/storages/s1/tags').flush(tagsOf(items));
  }

  /** SPEC-011 D10: what GET …/tags answers for a given item list (name + item count, sorted). */
  function tagsOf(items: ItemResponse[]): { name: string; itemCount: number }[] {
    const counts = new Map<string, number>();
    for (const i of items) {
      for (const tag of i.tags) {
        counts.set(tag, (counts.get(tag) ?? 0) + 1);
      }
    }
    return [...counts.entries()]
      .map(([name, itemCount]) => ({ name, itemCount }))
      .sort((a, b) => a.name.localeCompare(b.name, 'en', { sensitivity: 'base' }));
  }

  it('renders the three status groups from the API-computed expiryStatus', async () => {
    const fixture = TestBed.createComponent(StorageDetailPage);
    fixture.detectChanges();
    flushInitialLoad([
      item({ id: 'i1', name: 'Yogurt', expiryDate: '2026-07-12', expiryStatus: 'Expired' }),
      item({ id: 'i2', name: 'Milk', expiryDate: '2026-07-21', expiryStatus: 'ExpiringSoon' }),
      item({ id: 'i3', name: 'Peas', expiryDate: '2027-03-01', expiryStatus: 'Ok' }),
    ]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.group.expired .item-name')?.textContent).toContain('Yogurt');
    expect(element.querySelector('.group.expiring .item-name')?.textContent).toContain('Milk');
    expect(element.querySelector('.group.rest .item-name')?.textContent).toContain('Peas');
  });

  it('shows the production date for items without expiry date (EC-05)', async () => {
    const fixture = TestBed.createComponent(StorageDetailPage);
    fixture.detectChanges();
    flushInitialLoad([
      item({ id: 'i1', name: 'Minced meat', productionDate: '2026-07-01', expiryStatus: 'Ok' }),
    ]);
    await fixture.whenStable();

    const dateCell = (fixture.nativeElement as HTMLElement).querySelector('.item-date');
    // Locale-aware date (test locale falls back to en → mediumDate)
    expect(dateCell?.textContent).toContain('prod. Jul 1, 2026');
  });

  it('shows the translated validation message when adding an item without dates', async () => {
    const fixture = TestBed.createComponent(StorageDetailPage);
    fixture.detectChanges();
    flushInitialLoad([]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    await openAddForm(fixture);
    (element.querySelector('form.add-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    http
      .expectOne('/api/v1/storages/s1/items')
      .flush({ errorCode: 'item.dates.missing' }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    // AC-04: the form stays open and shows the message.
    expect(element.querySelector('.add-form .form-error')?.textContent).toContain(
      'One date required.',
    );
  });

  it('adds an item and reloads items + storage on success', async () => {
    const fixture = TestBed.createComponent(StorageDetailPage);
    fixture.detectChanges();
    flushInitialLoad([]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    await openAddForm(fixture);
    const name = element.querySelector('#item-name') as HTMLInputElement;
    name.value = 'Peas';
    name.dispatchEvent(new Event('input'));
    const amount = element.querySelector('#item-amount') as HTMLInputElement;
    amount.value = '2';
    amount.dispatchEvent(new Event('input'));
    const expiry = element.querySelector('#item-expiry') as HTMLInputElement;
    expiry.value = '2027-03-01';
    expiry.dispatchEvent(new Event('input'));

    (element.querySelector('form.add-form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const post = http.expectOne('/api/v1/storages/s1/items');
    expect(post.request.method).toBe('POST');
    expect(post.request.body.name).toBe('Peas');
    post.flush('i2', { status: 201, statusText: 'Created' });

    // reload: items then storages
    http
      .expectOne('/api/v1/storages/s1/items')
      .flush([
        item({ id: 'i2', name: 'Peas', amount: 2, expiryDate: '2027-03-01', expiryStatus: 'Ok' }),
      ]);
    http
      .expectOne('/api/v1/storages/s1')
      .flush({ id: 's1', name: 'Freezer', itemCount: 1, expiredCount: 0, expiringSoonCount: 0 });
    await fixture.whenStable();

    expect((element.textContent ?? '').includes('Peas')).toBe(true);
    // AC-03: the form closed again, the button is back.
    expect(element.querySelector('form.add-form')).toBeNull();
    expect(element.querySelector('.detail-head .icon-btn[aria-label="New item"]')).not.toBeNull();
  });

  it('deletes an item and reloads', async () => {
    const fixture = TestBed.createComponent(StorageDetailPage);
    fixture.detectChanges();
    flushInitialLoad([
      item({ id: 'i1', name: 'Milk', expiryDate: '2026-07-21', expiryStatus: 'ExpiringSoon' }),
    ]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const actions = element.querySelectorAll('.item-actions .icon-btn');
    (actions[actions.length - 1] as HTMLButtonElement).click(); // trash

    const del = http.expectOne('/api/v1/storages/s1/items/i1');
    expect(del.request.method).toBe('DELETE');
    del.flush(null);

    http.expectOne('/api/v1/storages/s1/items').flush([]);
    http
      .expectOne('/api/v1/storages/s1')
      .flush({ id: 's1', name: 'Freezer', itemCount: 0, expiredCount: 0, expiringSoonCount: 0 });
    await fixture.whenStable();

    expect(element.querySelector('.item-row')).toBeNull();
  });

  it('edits an item via the inline editor (PUT) and reloads', async () => {
    const fixture = TestBed.createComponent(StorageDetailPage);
    fixture.detectChanges();
    flushInitialLoad([
      item({
        id: 'i1',
        name: 'Milk',
        amount: 1,
        expiryDate: '2026-07-21',
        expiryStatus: 'ExpiringSoon',
      }),
    ]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    (element.querySelector('.item-actions .icon-btn') as HTMLButtonElement).click(); // pencil
    await fixture.whenStable();

    const amount = element.querySelector('.item-edit input[name="editAmount"]') as HTMLInputElement;
    amount.value = '2';
    amount.dispatchEvent(new Event('input'));
    (element.querySelector('form.item-edit') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const put = http.expectOne('/api/v1/storages/s1/items/i1');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body.amount).toBe(2);
    put.flush(null);

    http.expectOne('/api/v1/storages/s1/items').flush([
      item({
        id: 'i1',
        name: 'Milk',
        amount: 2,
        expiryDate: '2026-07-21',
        expiryStatus: 'ExpiringSoon',
      }),
    ]);
    http
      .expectOne('/api/v1/storages/s1')
      .flush({ id: 's1', name: 'Freezer', itemCount: 1, expiredCount: 0, expiringSoonCount: 1 });
    await fixture.whenStable();

    expect(element.querySelector('form.item-edit')).toBeNull();
  });

  it('renames the storage and reloads it, reflecting the new name', async () => {
    const fixture = TestBed.createComponent(StorageDetailPage);
    fixture.detectChanges();
    flushInitialLoad([]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    (element.querySelector('.detail-head .icon-btn') as HTMLButtonElement).click(); // pencil
    await fixture.whenStable();

    const input = element.querySelector('.inline-input') as HTMLInputElement;
    input.value = 'Cellar';
    input.dispatchEvent(new Event('input'));
    (element.querySelector('.inline-edit .icon-btn') as HTMLButtonElement).click(); // save ✓

    const put = http.expectOne('/api/v1/storages/s1');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body.name).toBe('Cellar');
    put.flush(null);

    // saveRename() reloads the single storage on success
    http
      .expectOne('/api/v1/storages/s1')
      .flush({ id: 's1', name: 'Cellar', itemCount: 0, expiredCount: 0, expiringSoonCount: 0 });
    await fixture.whenStable();

    expect(element.querySelector('.detail-head h1')?.textContent).toContain('Cellar');
  });

  it('deletes the whole storage after confirmation and navigates away', async () => {
    const fixture = TestBed.createComponent(StorageDetailPage);
    fixture.detectChanges();
    flushInitialLoad([]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const headActions = element.querySelectorAll('.detail-head .icon-btn');
    (headActions[headActions.length - 1] as HTMLButtonElement).click(); // trash
    await fixture.whenStable();

    expect(element.querySelector('app-confirm-dialog')).not.toBeNull();
    (element.querySelector('.dialog .btn-danger') as HTMLButtonElement).click();

    const del = http.expectOne('/api/v1/storages/s1');
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
    await fixture.whenStable();

    expect(element.querySelector('app-confirm-dialog')).toBeNull();
  });

  // SPEC-007 D4 / AC-15: owner vs member controls
  describe('sharing (SPEC-007)', () => {
    it('Owner_SeesShareAndDelete_NoLeaveAndNoOwnerLine', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([], { isOwner: true, memberCount: 2, ownerName: 'Me' });
      await fixture.whenStable();

      const element = fixture.nativeElement as HTMLElement;
      const labels = Array.from(element.querySelectorAll('.detail-head .icon-btn')).map((b) =>
        b.getAttribute('aria-label'),
      );
      expect(labels).toEqual(['Rename', 'New item', 'Search items', 'Share', 'Delete']);
      expect(element.querySelector('.storage-owner')).toBeNull();
    });

    it('Member_SeesOwnerLineMembersAndLeave_NoDelete', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([], { isOwner: false, memberCount: 1, ownerName: 'Olga Owner' });
      await fixture.whenStable();

      const element = fixture.nativeElement as HTMLElement;
      const labels = Array.from(element.querySelectorAll('.detail-head .icon-btn')).map((b) =>
        b.getAttribute('aria-label'),
      );
      expect(labels).toEqual(['Rename', 'New item', 'Search items', 'Members', 'Leave']);
      expect(element.querySelector('.storage-owner')?.textContent).toContain(
        'Shared by Olga Owner',
      );
    });

    it('Member_Leaves_AfterConfirmationAndNavigatesToTheList', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([], { isOwner: false, ownerName: 'Olga Owner' });
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;
      const router = TestBed.inject(Router);
      const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

      (
        element.querySelector('.detail-head .icon-btn[aria-label="Leave"]') as HTMLButtonElement
      ).click();
      await fixture.whenStable();
      expect(element.querySelector('.dialog .btn-danger')?.textContent?.trim()).toBe('Leave');
      (element.querySelector('.dialog .btn-danger') as HTMLButtonElement).click();

      const leave = http.expectOne('/api/v1/storages/s1/membership');
      expect(leave.request.method).toBe('DELETE');
      leave.flush(null);
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith(['/storages']);
    });

    it('Owner_OpensSharing_ShowsThePanelWithMembersAndLink', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([], { isOwner: true });
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;

      (
        element.querySelector('.detail-head .icon-btn[aria-label="Share"]') as HTMLButtonElement
      ).click();
      fixture.detectChanges();
      await fixture.whenStable();
      http
        .expectOne('/api/v1/storages/s1/members')
        .flush([{ userId: 'u1', displayName: 'Me', isOwner: true, joinedAt: null }]);
      http.expectOne('/api/v1/storages/s1/invitation').flush({ active: false, expiresAt: null });
      await fixture.whenStable();

      expect(element.querySelector('app-sharing-panel')).not.toBeNull();
      expect(element.querySelector('.sharing-panel')?.textContent).toContain(
        'No active invitation link.',
      );
      expect(element.querySelector('.sharing-panel')?.textContent).toContain('Nobody but you yet.');
    });
  });

  // SPEC-007 AC-20 (decision "a"): the owner of a shared storage hands over and leaves
  describe('owner delete with members', () => {
    it('Delete_WithMembers_OpensTheOwnerDialogInsteadOfThePlainOne', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([], { isOwner: true, memberCount: 2 });
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;

      (
        element.querySelector('.detail-head .icon-btn[aria-label="Delete"]') as HTMLButtonElement
      ).click();
      fixture.detectChanges();
      await fixture.whenStable();
      http.expectOne('/api/v1/storages/s1/members').flush([
        { userId: 'u1', displayName: 'Me', isOwner: true, joinedAt: null },
        { userId: 'u2', displayName: 'Max', isOwner: false, joinedAt: '2026-09-23T10:00:00Z' },
      ]);
      fixture.detectChanges();
      await fixture.whenStable();

      expect(element.querySelector('app-storage-delete-dialog')).not.toBeNull();
      expect(element.querySelector('app-confirm-dialog')).toBeNull();
    });

    it('HandOver_Confirmed_TransfersThenLeavesAndNavigatesToTheList', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([], { isOwner: true, memberCount: 1 });
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      (
        element.querySelector('.detail-head .icon-btn[aria-label="Delete"]') as HTMLButtonElement
      ).click();
      fixture.detectChanges();
      await fixture.whenStable();
      http.expectOne('/api/v1/storages/s1/members').flush([
        { userId: 'u1', displayName: 'Me', isOwner: true, joinedAt: null },
        { userId: 'u2', displayName: 'Max', isOwner: false, joinedAt: '2026-09-23T10:00:00Z' },
      ]);
      fixture.detectChanges();
      await fixture.whenStable();
      (element.querySelector('app-storage-delete-dialog .btn-danger') as HTMLButtonElement).click();

      const put = http.expectOne('/api/v1/storages/s1/owner');
      expect(put.request.body).toEqual({ userId: 'u2' });
      put.flush(null);
      const leave = http.expectOne('/api/v1/storages/s1/membership');
      expect(leave.request.method).toBe('DELETE');
      leave.flush(null);
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith(['/storages']);
    });
  });

  describe('hand over failures', () => {
    async function openOwnerDialog() {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([], { isOwner: true, memberCount: 1 });
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;
      (
        element.querySelector('.detail-head .icon-btn[aria-label="Delete"]') as HTMLButtonElement
      ).click();
      fixture.detectChanges();
      await fixture.whenStable();
      http.expectOne('/api/v1/storages/s1/members').flush([
        { userId: 'u1', displayName: 'Me', isOwner: true, joinedAt: null },
        { userId: 'u2', displayName: 'Max', isOwner: false, joinedAt: '2026-09-23T10:00:00Z' },
      ]);
      fixture.detectChanges();
      await fixture.whenStable();
      (element.querySelector('app-storage-delete-dialog .btn-danger') as HTMLButtonElement).click();
      return { fixture, element };
    }

    it('Transfer_Fails_ClosesTheDialogAndShowsTheError', async () => {
      const { fixture, element } = await openOwnerDialog();
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      http
        .expectOne('/api/v1/storages/s1/owner')
        .flush('boom', { status: 500, statusText: 'Internal Server Error' });
      fixture.detectChanges();
      await fixture.whenStable();

      expect(element.querySelector('app-storage-delete-dialog')).toBeNull();
      expect(element.querySelector('.form-error')?.textContent).toContain('Something went wrong.');
      expect(navigate).not.toHaveBeenCalled();
    });

    it('Leave_AfterTransfer_Fails_ReloadsTheStorageAndShowsTheError', async () => {
      const { fixture, element } = await openOwnerDialog();
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      http.expectOne('/api/v1/storages/s1/owner').flush(null);
      http
        .expectOne('/api/v1/storages/s1/membership')
        .flush('boom', { status: 500, statusText: 'Internal Server Error' });
      // The page reloads the storage: now a member of Max's storage.
      http.expectOne('/api/v1/storages/s1').flush({
        id: 's1',
        name: 'Freezer',
        itemCount: 0,
        expiredCount: 0,
        expiringSoonCount: 0,
        isOwner: false,
        memberCount: 1,
        ownerName: 'Max',
      });
      fixture.detectChanges();
      await fixture.whenStable();

      expect(element.querySelector('.form-error')?.textContent).toContain('Something went wrong.');
      expect(element.querySelector('.storage-owner')?.textContent).toContain('Shared by Max');
      expect(navigate).not.toHaveBeenCalled();
    });

    it('OwnershipChanged_FromThePanel_ReloadsTheStorage', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([], { isOwner: true, memberCount: 1 });
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;
      (
        element.querySelector('.detail-head .icon-btn[aria-label="Share"]') as HTMLButtonElement
      ).click();
      fixture.detectChanges();
      await fixture.whenStable();
      http.expectOne('/api/v1/storages/s1/members').flush([
        { userId: 'u1', displayName: 'Me', isOwner: true, joinedAt: null },
        { userId: 'u2', displayName: 'Max', isOwner: false, joinedAt: '2026-09-23T10:00:00Z' },
      ]);
      http.expectOne('/api/v1/storages/s1/invitation').flush({ active: false, expiresAt: null });
      fixture.detectChanges();
      await fixture.whenStable();

      const panel = element.querySelector('app-sharing-panel') as HTMLElement;
      (
        panel.querySelector(
          '.member-row button[aria-label="Make Max the owner"]',
        ) as HTMLButtonElement
      ).click();
      fixture.detectChanges();
      await fixture.whenStable();
      (element.querySelector('app-confirm-dialog .btn-danger') as HTMLButtonElement).click();
      http.expectOne('/api/v1/storages/s1/owner').flush(null);
      http.expectOne('/api/v1/storages/s1/members').flush([
        { userId: 'u2', displayName: 'Max', isOwner: true, joinedAt: null },
        { userId: 'u1', displayName: 'Me', isOwner: false, joinedAt: '2026-09-23T12:00:00Z' },
      ]);
      http.expectOne('/api/v1/storages/s1').flush({
        id: 's1',
        name: 'Freezer',
        itemCount: 0,
        expiredCount: 0,
        expiringSoonCount: 0,
        isOwner: false,
        memberCount: 1,
        ownerName: 'Max',
      });
      fixture.detectChanges();
      await fixture.whenStable();

      expect(element.querySelector('.storage-owner')?.textContent).toContain('Shared by Max');
    });
  });

  // #182: the add form opens on demand
  describe('add item on demand (#182)', () => {
    it('AddForm_OnOpen_IsClosedEvenForAnEmptyStorage', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([]);
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;

      expect(element.querySelector('form.add-form')).toBeNull();
      const plus = element.querySelector('.detail-head .icon-btn[aria-label="New item"]');
      expect(plus).not.toBeNull();
      expect(plus?.getAttribute('aria-expanded')).toBe('false');
    });

    it('AddForm_OnButtonClick_OpensEmptyAndFocusesTheName', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      const element = fixture.nativeElement as HTMLElement;
      document.body.appendChild(element);
      fixture.detectChanges();
      flushInitialLoad([]);
      await fixture.whenStable();

      await openAddForm(fixture);
      await new Promise((resolve) => setTimeout(resolve));

      expect(
        element
          .querySelector('.detail-head .icon-btn[aria-label="New item"]')
          ?.getAttribute('aria-expanded'),
      ).toBe('true');
      const name = element.querySelector('#item-name') as HTMLInputElement;
      expect(name.value).toBe('');
      expect(document.activeElement).toBe(name);
      element.remove();
    });

    it('AddForm_OnCancel_ClosesAndDiscardsTheInput', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([]);
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;
      await openAddForm(fixture);
      const name = element.querySelector('#item-name') as HTMLInputElement;
      name.value = 'Half typed';
      name.dispatchEvent(new Event('input'));

      (element.querySelector('.add-form .btn-ghost') as HTMLButtonElement).click();
      fixture.detectChanges();
      await fixture.whenStable();
      expect(element.querySelector('form.add-form')).toBeNull();

      await openAddForm(fixture);
      expect((element.querySelector('#item-name') as HTMLInputElement).value).toBe('');
    });

    it('AddForm_OnPlusClickedAgain_Closes', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([]);
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;
      await openAddForm(fixture);
      expect(element.querySelector('form.add-form')).not.toBeNull();

      await openAddForm(fixture); // same button, now closes

      expect(element.querySelector('form.add-form')).toBeNull();
    });

    it('AddForm_OnEscape_Closes', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      fixture.detectChanges();
      flushInitialLoad([]);
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;
      await openAddForm(fixture);

      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      fixture.detectChanges();
      await fixture.whenStable();

      expect(element.querySelector('form.add-form')).toBeNull();
      expect(
        element
          .querySelector('.detail-head .icon-btn[aria-label="New item"]')
          ?.getAttribute('aria-expanded'),
      ).toBe('false');
    });
  });

  // SPEC-010 — search field: filters the loaded list while typing, groups shrink, Escape clears.
  describe('item search (SPEC-010)', () => {
    const PANTRY = [
      item({ id: 'a', name: 'Bio Vollmilch', expiryStatus: 'Expired' }),
      item({ id: 'b', name: 'Käse', expiryStatus: 'ExpiringSoon' }),
      item({ id: 'c', name: 'Crème fraîche', expiryStatus: 'Ok' }),
      item({ id: 'd', name: 'Buttermilch', expiryStatus: 'Ok' }),
    ];

    async function renderPantry(): Promise<{
      fixture: ComponentFixture<StorageDetailPage>;
      element: HTMLElement;
    }> {
      const fixture = TestBed.createComponent(StorageDetailPage);
      const element = fixture.nativeElement as HTMLElement;
      document.body.appendChild(element);
      fixture.detectChanges();
      flushInitialLoad(PANTRY);
      await fixture.whenStable();
      return { fixture, element };
    }

    const searchButton = (element: HTMLElement) =>
      element.querySelector(
        '.detail-head .icon-btn[aria-label="Search items"]',
      ) as HTMLButtonElement;
    const searchField = (element: HTMLElement) =>
      element.querySelector('#item-search') as HTMLInputElement | null;

    async function openSearch(fixture: ComponentFixture<StorageDetailPage>, element: HTMLElement) {
      searchButton(element).click();
      fixture.detectChanges();
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));
    }

    async function type(
      fixture: ComponentFixture<StorageDetailPage>,
      element: HTMLElement,
      value: string,
    ) {
      const field = searchField(element)!;
      field.value = value;
      field.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      await fixture.whenStable();
    }

    const shownNames = (element: HTMLElement) =>
      [...element.querySelectorAll('.item-row .item-name')].map((n) => n.textContent?.trim());
    const groupHeads = (element: HTMLElement) =>
      [...element.querySelectorAll('.group-head')].map((h) =>
        h.textContent?.replace(/\s+/g, ' ').trim(),
      );

    it('AC-01: the header offers a Search button; activating it opens and focuses the field', async () => {
      const { fixture, element } = await renderPantry();
      expect(searchField(element)).toBeNull();
      expect(searchButton(element).getAttribute('aria-expanded')).toBe('false');

      await openSearch(fixture, element);

      const field = searchField(element)!;
      expect(field.type).toBe('search');
      expect(field.getAttribute('aria-label')).toBe('Search items by name');
      expect(document.activeElement).toBe(field);
      expect(searchButton(element).getAttribute('aria-expanded')).toBe('true');
      element.remove();
    });

    it('AC-04 / AC-05: filters by name while typing, groups shrink and empty groups vanish', async () => {
      const { fixture, element } = await renderPantry();
      await openSearch(fixture, element);
      expect(shownNames(element)).toEqual([
        'Bio Vollmilch',
        'Käse',
        'Crème fraîche',
        'Buttermilch',
      ]);

      await type(fixture, element, 'milch');

      expect(shownNames(element)).toEqual(['Bio Vollmilch', 'Buttermilch']);
      expect(groupHeads(element)).toEqual(['Expired · 1', 'Others · 1']);
      element.remove();
    });

    it('AC-04: accent- and case-insensitive, every word must match', async () => {
      const { fixture, element } = await renderPantry();
      await openSearch(fixture, element);

      await type(fixture, element, 'KASE');
      expect(shownNames(element)).toEqual(['Käse']);

      await type(fixture, element, 'creme fraiche');
      expect(shownNames(element)).toEqual(['Crème fraîche']);

      await type(fixture, element, 'bio butter');
      expect(shownNames(element)).toEqual([]);
      element.remove();
    });

    it('AC-06: typing never calls the API', async () => {
      const { fixture, element } = await renderPantry();
      await openSearch(fixture, element);

      await type(fixture, element, 'milch');
      await type(fixture, element, 'milch b');

      // No request beyond the initial load: verify() throws on an unexpected one.
      expect(() => http.verify()).not.toThrow();
      element.remove();
    });

    it('AC-07: a result line counts shown vs. total; no match says so and hides the empty hint', async () => {
      const { fixture, element } = await renderPantry();
      await openSearch(fixture, element);
      expect(element.querySelector('.search-result')).toBeNull();

      await type(fixture, element, 'milch');
      expect(element.querySelector('.search-result')?.textContent?.trim()).toBe('2 of 4 items');
      expect(element.querySelector('.search-result')?.tagName).toBe('OUTPUT'); // implicit status role

      await type(fixture, element, 'käse');
      expect(element.querySelector('.search-result')?.textContent?.trim()).toBe('1 of 4 items');

      await type(fixture, element, 'zzz');
      expect(element.querySelector('.search-result')?.textContent?.trim()).toBe(
        'No items match “zzz”.',
      );
      expect(element.querySelector('.empty-hint')).toBeNull();
      expect(element.querySelectorAll('.group')).toHaveLength(0);

      // EC-01: whitespace is not a query.
      await type(fixture, element, '   ');
      expect(element.querySelector('.search-result')).toBeNull();
      expect(shownNames(element)).toHaveLength(4);
      element.remove();
    });

    it('AC-02: the clear button removes the filter, closes the field and focuses the button', async () => {
      const { fixture, element } = await renderPantry();
      await openSearch(fixture, element);
      await type(fixture, element, 'milch');
      expect(
        element.querySelector('.search-bar .icon-btn[aria-label="Clear search"]'),
      ).not.toBeNull();

      (
        element.querySelector(
          '.search-bar .icon-btn[aria-label="Clear search"]',
        ) as HTMLButtonElement
      ).click();
      fixture.detectChanges();
      await fixture.whenStable();

      expect(searchField(element)).toBeNull();
      expect(shownNames(element)).toHaveLength(4);
      expect(document.activeElement).toBe(searchButton(element));
      element.remove();
    });

    it('AC-02 / D6: Escape in the field clears and closes without touching the add form', async () => {
      const { fixture, element } = await renderPantry();
      await openAddForm(fixture);
      await openSearch(fixture, element);
      await type(fixture, element, 'milch');

      searchField(element)!.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }),
      );
      fixture.detectChanges();
      await fixture.whenStable();

      expect(searchField(element)).toBeNull();
      expect(shownNames(element)).toHaveLength(4);
      expect(element.querySelector('form.add-form')).not.toBeNull();
      expect(document.activeElement).toBe(searchButton(element));
      element.remove();
    });

    it('AC-03: the button closes an empty field but only refocuses a field with a value', async () => {
      const { fixture, element } = await renderPantry();
      await openSearch(fixture, element);

      searchButton(element).click();
      fixture.detectChanges();
      await fixture.whenStable();
      expect(searchField(element)).toBeNull();

      await openSearch(fixture, element);
      await type(fixture, element, 'milch');
      await openSearch(fixture, element);
      expect(searchField(element)?.value).toBe('milch');
      expect(shownNames(element)).toEqual(['Bio Vollmilch', 'Buttermilch']);
      expect(document.activeElement).toBe(searchField(element));
      element.remove();
    });

    it('AC-08: adding an item keeps the query and re-applies it to the reloaded list', async () => {
      const { fixture, element } = await renderPantry();
      await openSearch(fixture, element);
      await type(fixture, element, 'milch');
      await openAddForm(fixture);

      const name = element.querySelector('#item-name') as HTMLInputElement;
      name.value = 'Joghurt';
      name.dispatchEvent(new Event('input', { bubbles: true }));
      const expiry = element.querySelector('#item-expiry') as HTMLInputElement;
      expiry.value = '2026-12-01';
      expiry.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      (element.querySelector('form.add-form') as HTMLFormElement).dispatchEvent(
        new Event('submit'),
      );
      http
        .expectOne('/api/v1/storages/s1/items')
        .flush('e', { status: 201, statusText: 'Created' });
      http
        .expectOne('/api/v1/storages/s1/items')
        .flush([...PANTRY, item({ id: 'e', name: 'Joghurt', expiryStatus: 'Ok' })]);
      http
        .expectOne('/api/v1/storages/s1')
        .flush({ id: 's1', name: 'Freezer', itemCount: 5, expiredCount: 1, expiringSoonCount: 1 });
      fixture.detectChanges();
      await fixture.whenStable();

      // D5: the filter is the user's — the new, non-matching item is hidden, the total grows.
      expect(searchField(element)?.value).toBe('milch');
      expect(shownNames(element)).toEqual(['Bio Vollmilch', 'Buttermilch']);
      expect(element.querySelector('.search-result')?.textContent?.trim()).toBe('2 of 5 items');
      element.remove();
    });

    it('EC-04: an empty storage still offers the search; typing yields the no-match line', async () => {
      const fixture = TestBed.createComponent(StorageDetailPage);
      const element = fixture.nativeElement as HTMLElement;
      document.body.appendChild(element);
      fixture.detectChanges();
      flushInitialLoad([]);
      await fixture.whenStable();
      expect(element.querySelector('.empty-hint')).not.toBeNull();

      await openSearch(fixture, element);
      await type(fixture, element, 'x');

      expect(element.querySelector('.search-result')?.textContent?.trim()).toBe(
        'No items match “x”.',
      );
      expect(element.querySelector('.empty-hint')).toBeNull();
      element.remove();
    });
  });

  // SPEC-011 — tags: chips on rows, click-to-filter, combined with the text search, the form.
  describe('tags (SPEC-011)', () => {
    const TAGGED = [
      item({ id: 'a', name: 'Bio Vollmilch', expiryStatus: 'Expired', tags: ['Bio'] }),
      item({ id: 'b', name: 'Bohnen', expiryStatus: 'ExpiringSoon', tags: ['Dosen', 'homemade'] }),
      item({ id: 'c', name: 'Mais', expiryStatus: 'Ok', tags: ['Dosen'] }),
      item({ id: 'd', name: 'Butter', expiryStatus: 'Ok' }),
    ];

    async function renderTagged(items = TAGGED) {
      const fixture = TestBed.createComponent(StorageDetailPage);
      const element = fixture.nativeElement as HTMLElement;
      document.body.appendChild(element);
      fixture.detectChanges();
      flushInitialLoad(items);
      await fixture.whenStable();
      fixture.detectChanges();
      return { fixture, element };
    }

    const rowChips = (element: HTMLElement, name: string) => {
      const row = [...element.querySelectorAll('.item-row')].find((r) =>
        r.querySelector('.item-name')?.textContent?.includes(name),
      ) as HTMLElement;
      return [...row.querySelectorAll('.item-tags .tag-chip')].map((c) => c.textContent?.trim());
    };
    const shown = (element: HTMLElement) =>
      [...element.querySelectorAll('.item-row .item-name')].map((n) => n.textContent?.trim());
    const clickChip = async (
      fixture: ComponentFixture<StorageDetailPage>,
      element: HTMLElement,
      name: string,
    ) => {
      const chip = [...element.querySelectorAll('.item-tags .tag-chip')].find(
        (c) => c.textContent?.trim() === name,
      ) as HTMLButtonElement;
      chip.click();
      fixture.detectChanges();
      await fixture.whenStable();
    };

    it('AC-12: shows the tags of an item as chips after its name, none for an untagged item', async () => {
      const { element } = await renderTagged();

      expect(rowChips(element, 'Bohnen')).toEqual(['Dosen', 'homemade']);
      expect(rowChips(element, 'Butter')).toEqual([]);
      element.remove();
    });

    it('AC-13: clicking a chip filters the list to that tag and shows the active-filter chip with its count', async () => {
      const { fixture, element } = await renderTagged();

      await clickChip(fixture, element, 'Dosen');

      expect(shown(element)).toEqual(['Bohnen', 'Mais']);
      const filter = element.querySelector('.tag-filter') as HTMLElement;
      expect(filter.querySelector('.tag-chip-active > span')?.textContent?.trim()).toBe('Dosen');
      expect(filter.querySelector('.tag-chip-count')?.textContent?.trim()).toBe('· 2');
      expect(filter.querySelector('.tag-chip-remove')).not.toBeNull();
      expect(element.querySelector('.search-result')?.textContent?.trim()).toBe('2 of 4 items');
      const active = [...element.querySelectorAll('.item-tags .tag-chip')].filter(
        (c) => c.getAttribute('aria-pressed') === 'true',
      );
      expect(active.map((c) => c.textContent?.trim())).toEqual(['Dosen', 'Dosen']);
      element.remove();
    });

    it('AC-14: the ✕ clears the filter, the same chip toggles it off, another chip replaces it', async () => {
      const { fixture, element } = await renderTagged();

      await clickChip(fixture, element, 'Dosen');
      await clickChip(fixture, element, 'homemade');
      expect(shown(element)).toEqual(['Bohnen']);

      await clickChip(fixture, element, 'homemade');
      expect(shown(element)).toHaveLength(4);
      expect(element.querySelector('.tag-filter')).toBeNull();

      await clickChip(fixture, element, 'Bio');
      (element.querySelector('.tag-filter .tag-chip-remove') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(shown(element)).toHaveLength(4);
      expect(element.querySelector('.search-result')).toBeNull();
      element.remove();
    });

    it('AC-15: tag filter and text search combine with AND and share the result line', async () => {
      const { fixture, element } = await renderTagged();
      await clickChip(fixture, element, 'Dosen');
      (
        element.querySelector(
          '.detail-head .icon-btn[aria-label="Search items"]',
        ) as HTMLButtonElement
      ).click();
      fixture.detectChanges();
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));

      const field = element.querySelector('#item-search') as HTMLInputElement;
      field.value = 'mais';
      field.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();

      expect(shown(element)).toEqual(['Mais']);
      expect(element.querySelector('.search-result')?.textContent?.trim()).toBe('1 of 4 items');

      field.value = 'butter';
      field.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      expect(shown(element)).toEqual([]);
      expect(element.querySelector('.search-result')?.textContent?.trim()).toBe(
        'No items match “butter”.',
      );
      element.remove();
    });

    it('AC-08 / AC-11: the add form carries a tag input and sends the chips with the item', async () => {
      const { fixture, element } = await renderTagged([]);
      await openAddForm(fixture);

      const tagField = element.querySelector('#item-tags') as HTMLInputElement;
      expect(tagField).not.toBeNull();
      tagField.value = 'Dosen, neu';
      tagField.dispatchEvent(new Event('input', { bubbles: true }));
      // Leaving the field commits the text still in it (like clicking Save does in a browser).
      tagField.dispatchEvent(new Event('blur'));
      fixture.detectChanges();
      const name = element.querySelector('#item-name') as HTMLInputElement;
      name.value = 'Erbsen';
      name.dispatchEvent(new Event('input', { bubbles: true }));
      const expiry = element.querySelector('#item-expiry') as HTMLInputElement;
      expiry.value = '2026-12-01';
      expiry.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      (element.querySelector('form.add-form') as HTMLFormElement).dispatchEvent(
        new Event('submit'),
      );

      const post = http.expectOne('/api/v1/storages/s1/items');
      expect(post.request.body).toMatchObject({ name: 'Erbsen', tags: ['Dosen', 'neu'] });
      post.flush('e', { status: 201, statusText: 'Created' });
      // The reload fetches items, storage and tags (D10).
      http
        .expectOne('/api/v1/storages/s1/items')
        .flush([item({ id: 'e', name: 'Erbsen', tags: ['Dosen', 'neu'] })]);
      http.expectOne('/api/v1/storages/s1').flush({ id: 's1', name: 'Freezer', itemCount: 1 });
      http.expectOne('/api/v1/storages/s1/tags').flush([
        { name: 'Dosen', itemCount: 1 },
        { name: 'neu', itemCount: 1 },
      ]);
      fixture.detectChanges();
      await fixture.whenStable();

      expect(rowChips(element, 'Erbsen')).toEqual(['Dosen', 'neu']);
      element.remove();
    });

    it('AC-08 / AC-11: the inline edit starts with the item tags and sends the changed set', async () => {
      const { fixture, element } = await renderTagged();
      const bohnen = [...element.querySelectorAll('.item-row')].find((r) =>
        r.querySelector('.item-name')?.textContent?.includes('Bohnen'),
      ) as HTMLElement;
      (bohnen.querySelector('.icon-btn[aria-label="Edit"]') as HTMLButtonElement).click();
      fixture.detectChanges();
      await fixture.whenStable();

      const chips = [...element.querySelectorAll('.tag-chip-editable > span')].map((c) =>
        c.textContent?.trim(),
      );
      expect(chips).toEqual(['Dosen', 'homemade']);
      (element.querySelector('[aria-label="Remove tag homemade"]') as HTMLButtonElement).click();
      fixture.detectChanges();
      (element.querySelector('form.item-edit') as HTMLFormElement).dispatchEvent(
        new Event('submit'),
      );

      const put = http.expectOne('/api/v1/storages/s1/items/b');
      expect(put.request.body).toMatchObject({ name: 'Bohnen', tags: ['Dosen'] });
      put.flush(null, { status: 204, statusText: 'No Content' });
      element.remove();
    });

    it('AC-10: the API refusal for tags is shown in the form', async () => {
      const { fixture, element } = await renderTagged([]);
      await openAddForm(fixture);
      const name = element.querySelector('#item-name') as HTMLInputElement;
      name.value = 'X';
      name.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      (element.querySelector('form.add-form') as HTMLFormElement).dispatchEvent(
        new Event('submit'),
      );

      http
        .expectOne('/api/v1/storages/s1/items')
        .flush({ errorCode: 'item.tags.tooLong' }, { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();

      expect(element.querySelector('form.add-form [role="alert"]')?.textContent?.trim()).toBe(
        'Tag too long.',
      );
      element.remove();
    });

    it('EC-04: the active filter is cleared when its tag no longer exists after a reload', async () => {
      const { fixture, element } = await renderTagged();
      await clickChip(fixture, element, 'Bio');
      expect(shown(element)).toEqual(['Bio Vollmilch']);

      // Delete the only item with "Bio"; the reload no longer lists the tag.
      const row = element.querySelector('.item-row') as HTMLElement;
      (row.querySelector('.icon-btn[aria-label="Delete"]') as HTMLButtonElement).click();
      http
        .expectOne('/api/v1/storages/s1/items/a')
        .flush(null, { status: 204, statusText: 'No Content' });
      http.expectOne('/api/v1/storages/s1/items').flush(TAGGED.filter((i) => i.id !== 'a'));
      http.expectOne('/api/v1/storages/s1').flush({ id: 's1', name: 'Freezer', itemCount: 3 });
      http.expectOne('/api/v1/storages/s1/tags').flush(tagsOf(TAGGED.filter((i) => i.id !== 'a')));
      fixture.detectChanges();
      await fixture.whenStable();

      expect(element.querySelector('.tag-filter')).toBeNull();
      expect(shown(element)).toHaveLength(3);
      element.remove();
    });
  });
});
