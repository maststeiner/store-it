import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { apiErrorInterceptor } from '../core/api-error.interceptor';
import { TranslateService } from '../core/translate';
import { SharingPanel } from './sharing-panel';

const TRANSLATIONS = {
  sharing: {
    title: 'Sharing',
    link: {
      create: 'Create invitation link',
      renew: 'Create a new link',
      deactivate: 'Deactivate link',
      copy: 'Copy',
      copied: 'Copied',
      label: 'Invitation link',
      created: 'Link created.',
      validUntil: 'Valid until {{date}}.',
      active: 'Active until {{date}}.',
      none: 'No active invitation link.',
      warning: 'Anyone with the link can join.',
    },
    members: {
      title: 'Members',
      owner: 'Owner',
      remove: 'Remove',
      removeNamed: 'Remove {{name}}',
      none: 'Nobody but you yet.',
      makeOwner: 'Make owner',
      makeOwnerNamed: 'Make {{name}} the owner',
    },
    makeOwner: { title: 'Hand over ownership', message: '{{name}} becomes the owner.' },
  },
  actions: { cancel: 'Cancel', delete: 'Delete' },
  errors: { generic: 'Something went wrong.' },
};

const MEMBERS = [
  { userId: 'u1', displayName: 'Olga Owner', isOwner: true, joinedAt: null },
  { userId: 'u2', displayName: 'Max Member', isOwner: false, joinedAt: '2026-09-23T10:00:00Z' },
];

describe('SharingPanel (SPEC-007 AC-16)', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SharingPanel],
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(
    isOwner: boolean,
  ): Promise<{ fixture: ComponentFixture<SharingPanel>; el: HTMLElement }> {
    const fixture = TestBed.createComponent(SharingPanel);
    fixture.componentRef.setInput('storageId', 's1');
    fixture.componentRef.setInput('isOwner', isOwner);
    fixture.detectChanges();
    http.expectOne('/api/v1/storages/s1/members').flush(MEMBERS);
    if (isOwner) {
      http.expectOne('/api/v1/storages/s1/invitation').flush({ active: false, expiresAt: null });
    }
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('Owner_Opens_SeesMembersLinkStateAndRemoveButtons', async () => {
    const { el } = await render(true);

    const rows = Array.from(el.querySelectorAll('.member-row'));
    expect(rows.map((r) => r.querySelector('.member-name')?.textContent?.trim())).toEqual([
      'Olga Owner',
      'Max Member',
    ]);
    expect(rows[0].querySelector('.member-role')?.textContent).toContain('Owner');
    const labels = Array.from(rows[1].querySelectorAll('button')).map((b) =>
      b.getAttribute('aria-label'),
    );
    expect(labels).toEqual(['Make Max Member the owner', 'Remove Max Member']);
    expect(el.textContent).toContain('No active invitation link.');
    expect(el.textContent).not.toContain('@');
  });

  it('Member_Opens_SeesMembersOnly_NoLinkControlsNoRemove', async () => {
    const { el } = await render(false);

    expect(el.querySelector('.sharing-invite')).toBeNull();
    expect(el.querySelectorAll('.member-row button')).toHaveLength(0);
    expect(el.querySelectorAll('.member-row')).toHaveLength(2);
  });

  it('Owner_CreatesLink_ShowsFragmentUrlOnceAndRefreshesTheStatus', async () => {
    const { fixture, el } = await render(true);

    (el.querySelector('.sharing-invite .btn-primary') as HTMLButtonElement).click();
    const post = http.expectOne('/api/v1/storages/s1/invitation');
    expect(post.request.method).toBe('POST');
    post.flush({ token: 'tok123', expiresAt: '2026-09-30T12:00:00Z' });
    http
      .expectOne('/api/v1/storages/s1/invitation')
      .flush({ active: true, expiresAt: '2026-09-30T12:00:00Z' });
    fixture.detectChanges();
    await fixture.whenStable();

    const link = el.querySelector('.sharing-link') as HTMLInputElement;
    expect(link.value).toBe(`${window.location.origin}/join#tok123`);
    expect(el.textContent).toContain('Deactivate link');
  });

  it('Owner_DeactivatesLink_SendsDeleteAndHidesTheUrl', async () => {
    const { fixture, el } = await render(true);
    (el.querySelector('.sharing-invite .btn-primary') as HTMLButtonElement).click();
    http
      .expectOne('/api/v1/storages/s1/invitation')
      .flush({ token: 't', expiresAt: '2026-09-30T12:00:00Z' });
    http
      .expectOne('/api/v1/storages/s1/invitation')
      .flush({ active: true, expiresAt: '2026-09-30T12:00:00Z' });
    fixture.detectChanges();
    await fixture.whenStable();

    (el.querySelector('.sharing-invite .btn-ghost') as HTMLButtonElement).click();
    const del = http.expectOne('/api/v1/storages/s1/invitation');
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
    http.expectOne('/api/v1/storages/s1/invitation').flush({ active: false, expiresAt: null });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelector('.sharing-link')).toBeNull();
    expect(el.textContent).toContain('No active invitation link.');
  });

  it('Owner_RemovesMember_SendsDeleteAndReloadsTheList', async () => {
    const { fixture, el } = await render(true);

    (
      el.querySelector('.member-row button[aria-label="Remove Max Member"]') as HTMLButtonElement
    ).click();
    const del = http.expectOne('/api/v1/storages/s1/members/u2');
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
    http.expectOne('/api/v1/storages/s1/members').flush([MEMBERS[0]]);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(el.querySelectorAll('.member-row')).toHaveLength(1);
    expect(el.textContent).toContain('Nobody but you yet.');
  });

  it('Owner_WhileCreateIsPending_HasBothMutationButtonsDisabled', async () => {
    const { fixture, el } = await render(true);

    const create = el.querySelector('.sharing-invite .btn-primary') as HTMLButtonElement;
    create.click();
    fixture.detectChanges();

    expect(create.disabled).toBe(true);
    // Only one request in flight — a second click must not have fired another one.
    create.click();
    const post = http.expectOne('/api/v1/storages/s1/invitation');
    post.flush({ token: 't', expiresAt: '2026-09-30T12:00:00Z' });
    http
      .expectOne('/api/v1/storages/s1/invitation')
      .flush({ active: true, expiresAt: '2026-09-30T12:00:00Z' });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(create.disabled).toBe(false);
  });

  // SPEC-007 AC-18 / AC-23: hand over from the member list
  it('Owner_MakesMemberOwner_ConfirmsThenTransfersAndNotifiesTheHost', async () => {
    const { fixture, el } = await render(true);
    const changed = vi.fn();
    fixture.componentInstance.ownershipChanged.subscribe(changed);

    (
      el.querySelector(
        '.member-row button[aria-label="Make Max Member the owner"]',
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(el.querySelector('app-confirm-dialog')?.textContent).toContain(
      'Max Member becomes the owner.',
    );
    expect(el.querySelector('.dialog .btn-danger')?.textContent?.trim()).toBe('Make owner');

    (el.querySelector('.dialog .btn-danger') as HTMLButtonElement).click();
    const put = http.expectOne('/api/v1/storages/s1/owner');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ userId: 'u2' });
    put.flush(null);
    http.expectOne('/api/v1/storages/s1/members').flush([
      { userId: 'u2', displayName: 'Max Member', isOwner: true, joinedAt: null },
      { userId: 'u1', displayName: 'Olga Owner', isOwner: false, joinedAt: '2026-09-23T12:00:00Z' },
    ]);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(changed).toHaveBeenCalledTimes(1);
    expect(el.querySelector('app-confirm-dialog')).toBeNull();
  });
});
