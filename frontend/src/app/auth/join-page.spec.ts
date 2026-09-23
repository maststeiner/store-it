import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';

import { apiErrorInterceptor } from '../core/api-error.interceptor';
import { TranslateService } from '../core/translate';
import { JoinPage } from './join-page';

const TRANSLATIONS = {
  actions: { cancel: 'Cancel' },
  errors: { generic: 'Something went wrong.' },
  sharing: {
    join: {
      title: 'Invitation to a storage',
      text: '{{owner}} shares the storage "{{storage}}" with you.',
      join: 'Join',
      loading: 'Checking …',
      invalidTitle: 'This link is no longer valid',
      invalidText: 'Ask for a new link.',
      backToList: 'To my storages',
    },
  },
};

async function setup(fragment: string | null) {
  await TestBed.configureTestingModule({
    imports: [JoinPage],
    providers: [
      provideHttpClient(withInterceptors([apiErrorInterceptor])),
      provideHttpClientTesting(),
      provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { fragment } } },
    ],
  }).compileComponents();
  TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
  return TestBed.inject(HttpTestingController);
}

describe('JoinPage (SPEC-007 D3 / AC-17)', () => {
  it('Token_FromTheFragment_IsSentInTheBodyAndThePageNamesOwnerAndStorage', async () => {
    const http = await setup('tok123');
    const fixture = TestBed.createComponent(JoinPage);
    fixture.detectChanges();

    const preview = http.expectOne('/api/v1/invitations/preview');
    expect(preview.request.method).toBe('POST');
    expect(preview.request.body).toEqual({ token: 'tok123' });
    preview.flush({
      storageId: 's7',
      storageName: 'Cellar',
      ownerName: 'Olga Owner',
      alreadyMember: false,
    });
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Olga Owner shares the storage "Cellar" with you.');
    expect(element.querySelector('.btn-primary')?.textContent?.trim()).toBe('Join');
    http.verify();
  });

  it('Join_Accepts_AndNavigatesToTheStorage', async () => {
    const http = await setup('tok123');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(JoinPage);
    fixture.detectChanges();
    http
      .expectOne('/api/v1/invitations/preview')
      .flush({ storageId: 's7', storageName: 'Cellar', ownerName: 'Olga', alreadyMember: false });
    await fixture.whenStable();

    (
      (fixture.nativeElement as HTMLElement).querySelector('.btn-primary') as HTMLButtonElement
    ).click();
    const accept = http.expectOne('/api/v1/invitations/accept');
    expect(accept.request.body).toEqual({ token: 'tok123' });
    accept.flush({ storageId: 's7' });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/storages', 's7']);
    http.verify();
  });

  it('AlreadyMember_SkipsThePage_AndOpensTheStorage', async () => {
    const http = await setup('tok123');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(JoinPage);
    fixture.detectChanges();
    http
      .expectOne('/api/v1/invitations/preview')
      .flush({ storageId: 's7', storageName: 'Cellar', ownerName: 'Olga', alreadyMember: true });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/storages', 's7']);
    http.verify();
  });

  it('InvalidToken_ShowsTheMessageAndALinkBack', async () => {
    const http = await setup('expired');
    const fixture = TestBed.createComponent(JoinPage);
    fixture.detectChanges();
    http
      .expectOne('/api/v1/invitations/preview')
      .flush(
        { title: 'invite.invalid', errorCode: 'invite.invalid', status: 404 },
        { status: 404, statusText: 'Not Found' },
      );
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('This link is no longer valid');
    expect(element.querySelector('a[href="/storages"]')).not.toBeNull();
    http.verify();
  });

  it('NoFragment_IsInvalidWithoutCallingTheApi', async () => {
    const http = await setup(null);
    const fixture = TestBed.createComponent(JoinPage);
    fixture.detectChanges();
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'This link is no longer valid',
    );
    http.verify();
  });
});
