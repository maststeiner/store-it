import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { apiErrorInterceptor } from '../core/api-error.interceptor';
import { TranslateService } from '../core/translate';
import { AdminStatisticsPage } from './admin-statistics-page';

const TRANSLATIONS = {
  admin: {
    statistics: {
      title: 'Usage statistics',
      generatedAt: 'Generated at {{time}}',
      refresh: 'Refresh',
      loading: 'Loading…',
      groups: { users: 'Users', storages: 'Storages', items: 'Items' },
      users: {
        total: 'Total',
        newLast7Days: 'New (7 days)',
        newLast30Days: 'New (30 days)',
        withoutAnyStorage: 'Without storage',
        byIssuer: 'By provider',
      },
      storages: {
        total: 'Total',
        shared: 'Shared',
        empty: 'Empty',
        itemsPerStorageMedian: 'Median items',
        itemsPerStorageMax: 'Max items',
        pendingInvitations: 'Open links',
      },
      items: {
        total: 'Total',
        withExpiryDate: 'With expiry',
        withProductionDate: 'With production date',
        expired: 'Expired',
        expiringSoon: 'Expiring soon',
        byUnit: 'By unit',
      },
    },
  },
  units: { Piece: 'pcs', Liter: 'l' },
  errors: { generic: 'Something went wrong.' },
};

const STATISTICS = {
  generatedAt: '2026-10-05T18:00:00+00:00',
  users: {
    total: 1234,
    newLast7Days: 3,
    newLast30Days: 12,
    byIssuer: { 'https://accounts.google.com': 1000, 'https://login.microsoftonline.com': 234 },
    withoutAnyStorage: 7,
  },
  storages: { total: 40, shared: 5, empty: 2, itemsPerStorageMedian: 4, itemsPerStorageMax: 31 },
  invitations: { pending: 1 },
  items: {
    total: 300,
    withExpiryDate: 250,
    withProductionDate: 20,
    expired: 9,
    expiringSoon: 4,
    byUnit: { Piece: 280, Liter: 20 },
  },
};

describe('AdminStatisticsPage', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminStatisticsPage],
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function render() {
    const fixture = TestBed.createComponent(AdminStatisticsPage);
    fixture.detectChanges();
    return fixture;
  }

  it('calls the statistics endpoint once and renders the three groups', () => {
    const fixture = render();

    http.expectOne('/api/v1/admin/statistics').flush(STATISTICS);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const headings = [...el.querySelectorAll('h2')].map((h) => h.textContent?.trim());
    expect(headings).toEqual(['Users', 'Storages', 'Items']);
    const tiles = [...el.querySelectorAll('.stat-tile')].map((tile) => ({
      value: tile.querySelector('.stat-value')?.textContent?.trim(),
      label: tile.querySelector('.stat-label')?.textContent?.trim(),
    }));
    expect(tiles).toContainEqual({ value: '1,234', label: 'Total' });
    expect(tiles).toContainEqual({ value: '1', label: 'Open links' });
    expect(tiles).toContainEqual({ value: '4', label: 'Expiring soon' });
    expect(tiles).toHaveLength(15);
  });

  it('lists providers as given and units translated', () => {
    const fixture = render();
    http.expectOne('/api/v1/admin/statistics').flush(STATISTICS);
    fixture.detectChanges();

    const terms = [...(fixture.nativeElement as HTMLElement).querySelectorAll('dt')].map((dt) =>
      dt.textContent?.trim(),
    );
    expect(terms).toEqual([
      'https://accounts.google.com',
      'https://login.microsoftonline.com',
      'pcs',
      'l',
    ]);
  });

  it('shows the generation time and re-queries on refresh', () => {
    const fixture = render();
    http.expectOne('/api/v1/admin/statistics').flush(STATISTICS);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.count-hint')?.textContent).toContain('Generated at');

    (el.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();

    http.expectOne('/api/v1/admin/statistics').flush(STATISTICS);
  });

  it('shows the error state when the API refuses (AC-11)', () => {
    const fixture = render();

    http
      .expectOne('/api/v1/admin/statistics')
      .flush({ errorCode: 'forbidden' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Something went wrong.');
    expect(el.querySelectorAll('.stat-tile')).toHaveLength(0);
  });
});
