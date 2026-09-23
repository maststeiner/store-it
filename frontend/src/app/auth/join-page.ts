import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { InvitationPreviewResponse } from '../api/models';
import { SharingService } from '../api/services';
import { ErrorMessages } from '../core/error-messages';
import { TranslatePipe } from '../core/translate';

/**
 * SPEC-007 D3 / AC-17: `/join#<token>`. The token lives in the URL fragment, so it never reaches
 * a server log; the page reads it and sends it to the API in a request body. Existing members
 * are sent straight to the storage; everyone else sees who shares what and clicks *Join*.
 */
@Component({
  selector: 'app-join-page',
  imports: [TranslatePipe, RouterLink],
  templateUrl: './join-page.html',
})
export class JoinPage implements OnInit {
  private readonly api = inject(SharingService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly errors = inject(ErrorMessages);

  protected readonly preview = signal<InvitationPreviewResponse | null>(null);
  protected readonly invalid = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly joining = signal(false);

  private readonly token = (this.route.snapshot.fragment ?? '').trim();

  ngOnInit(): void {
    if (!this.token) {
      this.invalid.set(true);
      return;
    }
    this.api.previewInvitation({ 'X-XSRF-TOKEN': '', body: { token: this.token } }).subscribe({
      next: (preview) => {
        if (preview.alreadyMember) {
          void this.router.navigate(['/storages', preview.storageId]);
          return;
        }
        this.preview.set(preview);
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  protected join(): void {
    this.joining.set(true);
    this.api.acceptInvitation({ 'X-XSRF-TOKEN': '', body: { token: this.token } }).subscribe({
      next: (accepted) => void this.router.navigate(['/storages', accepted.storageId]),
      error: (error: unknown) => {
        this.joining.set(false);
        this.fail(error);
      },
    });
  }

  private fail(error: unknown): void {
    const status = (error as { status?: number })?.status;
    if (status === 404) {
      this.invalid.set(true);
    } else {
      this.error.set(this.errors.messageFor(error));
    }
  }
}
