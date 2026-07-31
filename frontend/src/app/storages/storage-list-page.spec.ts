import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { apiErrorInterceptor } from '../core/api-error.interceptor';
import { TranslateService } from '../core/translate';
import { StorageListPage } from './storage-list-page';

const TRANSLATIONS = {
  storages: {
    title: 'My storages',
    new: '+ New storage',
    createGhost: '+ Create storage',
    namePlaceholder: 'Storage name',
    count: { one: '1 storage', other: '{{count}} storages' },
    chips: {
      expired: { one: '1 expired', other: '{{count}} expired' },
      expiring: { one: '1 expiring soon', other: '{{count}} expiring soon' },
      fresh: 'All fresh',
    },
    deleteTitle: 'Delete storage',
    deleteConfirm: 'Delete "{{name}}"?',
  },
  items: { count: { one: '1 item', other: '{{count}} items' } },
  actions: { create: 'Create', save: 'Save', cancel: 'Cancel', rename: 'Rename', delete: 'Delete' },
  errors: { generic: 'Something went wrong.', storage: { name: { empty: 'Name required.' } } },
};

describe('StorageListPage', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StorageListPage],
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
    http = TestBed.inject(HttpTestingController);
  });

  function flushStorages(
    storages: {
      id: string;
      name: string;
      itemCount: number;
      expiredCount: number;
      expiringSoonCount: number;
    }[],
  ) {
    http.expectOne('/api/v1/storages').flush(storages);
  }

  it('renders one card per storage with name and item count', async () => {
    const fixture = TestBed.createComponent(StorageListPage);
    fixture.detectChanges();
    flushStorages([
      { id: 's1', name: 'Pantry', itemCount: 12, expiredCount: 0, expiringSoonCount: 2 },
      { id: 's2', name: 'Freezer', itemCount: 3, expiredCount: 1, expiringSoonCount: 0 },
    ]);
    await fixture.whenStable();

    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('.storage-card');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(cards.length).toBeGreaterThanOrEqual(2);
    expect(text).toContain('Pantry');
    expect(text).toContain('12 items');
  });

  it('renders status chips from the server-computed counts (AC-01a)', async () => {
    const fixture = TestBed.createComponent(StorageListPage);
    fixture.detectChanges();
    flushStorages([
      { id: 's1', name: 'Freezer', itemCount: 5, expiredCount: 1, expiringSoonCount: 2 },
      { id: 's2', name: 'Cellar', itemCount: 4, expiredCount: 0, expiringSoonCount: 0 },
      { id: 's3', name: 'Empty', itemCount: 0, expiredCount: 0, expiringSoonCount: 0 },
    ]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.chip.expired')?.textContent).toContain('1 expired');
    expect(element.querySelector('.chip.expiring')?.textContent).toContain('2 expiring soon');
    expect(element.querySelectorAll('.chip.ok').length).toBe(1); // only Cellar — Empty has no chips
  });

  it('shows the translated validation message when creating with an empty name', async () => {
    const fixture = TestBed.createComponent(StorageListPage);
    fixture.detectChanges();
    flushStorages([]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    (element.querySelector('.storage-card.ghost') as HTMLButtonElement).click();
    await fixture.whenStable();

    (element.querySelector('form.create-form') as HTMLFormElement).dispatchEvent(
      new Event('submit'),
    );
    http
      .expectOne('/api/v1/storages')
      .flush({ errorCode: 'storage.name.empty' }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(element.querySelector('.form-error')?.textContent).toContain('Name required.');
  });

  it('creates a storage and reloads the list on success', async () => {
    const fixture = TestBed.createComponent(StorageListPage);
    fixture.detectChanges();
    flushStorages([]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    (element.querySelector('.storage-card.ghost') as HTMLButtonElement).click();
    await fixture.whenStable();

    const input = element.querySelector('form.create-form input') as HTMLInputElement;
    input.value = 'Pantry';
    input.dispatchEvent(new Event('input'));
    (element.querySelector('form.create-form') as HTMLFormElement).dispatchEvent(
      new Event('submit'),
    );

    const post = http.expectOne('/api/v1/storages');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ name: 'Pantry' });
    post.flush({ id: 's9', name: 'Pantry', itemCount: 0, expiredCount: 0, expiringSoonCount: 0 });

    flushStorages([
      { id: 's9', name: 'Pantry', itemCount: 0, expiredCount: 0, expiringSoonCount: 0 },
    ]);
    await fixture.whenStable();

    expect((element.textContent ?? '').includes('Pantry')).toBe(true);
    expect(element.querySelector('form.create-form')).toBeNull();
  });

  it('renames a storage via the inline editor and reloads', async () => {
    const fixture = TestBed.createComponent(StorageListPage);
    fixture.detectChanges();
    flushStorages([
      { id: 's1', name: 'Freezer', itemCount: 0, expiredCount: 0, expiringSoonCount: 0 },
    ]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    (element.querySelector('.card-actions .icon-btn') as HTMLButtonElement).click(); // pencil
    await fixture.whenStable();

    const input = element.querySelector('.inline-edit .inline-input') as HTMLInputElement;
    input.value = 'Cellar freezer';
    input.dispatchEvent(new Event('input'));
    (element.querySelector('.inline-edit .icon-btn') as HTMLButtonElement).click(); // save ✓

    const put = http.expectOne('/api/v1/storages/s1');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ name: 'Cellar freezer' });
    put.flush(null);

    flushStorages([
      { id: 's1', name: 'Cellar freezer', itemCount: 0, expiredCount: 0, expiringSoonCount: 0 },
    ]);
    await fixture.whenStable();

    expect((element.textContent ?? '').includes('Cellar freezer')).toBe(true);
  });

  it('deletes a storage after confirmation and reloads', async () => {
    const fixture = TestBed.createComponent(StorageListPage);
    fixture.detectChanges();
    flushStorages([
      { id: 's1', name: 'Freezer', itemCount: 3, expiredCount: 0, expiringSoonCount: 0 },
    ]);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const actions = element.querySelectorAll('.card-actions .icon-btn');
    (actions[actions.length - 1] as HTMLButtonElement).click(); // trash
    await fixture.whenStable();

    expect(element.querySelector('app-confirm-dialog')).not.toBeNull();
    (element.querySelector('.dialog .btn-danger') as HTMLButtonElement).click();

    const del = http.expectOne('/api/v1/storages/s1');
    expect(del.request.method).toBe('DELETE');
    del.flush(null);

    flushStorages([]);
    await fixture.whenStable();

    expect(element.querySelector('app-confirm-dialog')).toBeNull();
    expect(element.querySelectorAll('.storage-card:not(.ghost)').length).toBe(0);
  });
});
