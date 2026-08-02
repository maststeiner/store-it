import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { ItemResponse } from '../api/models';
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
    empty: 'No items yet.',
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
  errors: { generic: 'Something went wrong.', item: { dates: { missing: 'One date required.' } } },
};

function item(partial: Partial<ItemResponse>): ItemResponse {
  return {
    id: 'i1',
    name: 'Item',
    amount: 1,
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

  function flushInitialLoad(items: ItemResponse[]) {
    http.expectOne('/api/v1/storages/s1').flush({
      id: 's1',
      name: 'Freezer',
      itemCount: items.length,
      expiredCount: 0,
      expiringSoonCount: 0,
    });
    http.expectOne('/api/v1/storages/s1/items').flush(items);
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
    (element.querySelector('form.add-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    http
      .expectOne('/api/v1/storages/s1/items')
      .flush({ errorCode: 'item.dates.missing' }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

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
});
