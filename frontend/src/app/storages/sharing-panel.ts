import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';

import { InvitationStatusResponse, StorageMemberResponse } from '../api/models';
import { SharingService } from '../api/services';
import { ErrorMessages } from '../core/error-messages';
import { LanguageService } from '../core/language.service';
import { TranslatePipe } from '../core/translate';

/**
 * SPEC-007 AC-16: the owner's share view (invitation link + member list) and, read-only, the
 * member list for members (D7). Display names only (D6). The link carries the token in the
 * URL fragment (`/join#<token>`, D3) so no server ever logs it.
 */
@Component({
  selector: 'app-sharing-panel',
  imports: [TranslatePipe, DatePipe],
  templateUrl: './sharing-panel.html',
})
export class SharingPanel implements OnInit {
  readonly storageId = input.required<string>();
  readonly isOwner = input.required<boolean>();

  private readonly api = inject(SharingService);
  private readonly errors = inject(ErrorMessages);
  private readonly language = inject(LanguageService);

  protected readonly locale = this.language.current;
  protected readonly members = signal<StorageMemberResponse[] | null>(null);
  protected readonly invitation = signal<InvitationStatusResponse | null>(null);
  /** The freshly created link — shown once, until the panel is left (AC-05). */
  protected readonly link = signal<string | null>(null);
  protected readonly linkExpiresAt = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly error = signal<string | null>(null);
  /** A create/deactivate request is in flight: both buttons are disabled meanwhile. */
  protected readonly busy = signal(false);
  /** Ignore invitation-status responses older than the latest request. */
  private statusRequest = 0;

  protected readonly others = computed(() => (this.members() ?? []).filter((m) => !m.isOwner));

  ngOnInit(): void {
    this.loadMembers();
    if (this.isOwner()) {
      this.loadInvitation();
    }
  }

  protected createLink(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.api.createInvitation({ 'X-XSRF-TOKEN': '', storageId: this.storageId() }).subscribe({
      next: (created) => {
        this.link.set(`${window.location.origin}/join#${created.token}`);
        this.linkExpiresAt.set(created.expiresAt);
        this.copied.set(false);
        this.error.set(null);
        this.busy.set(false);
        this.loadInvitation();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(this.errors.messageFor(error));
      },
    });
  }

  protected async copyLink(): Promise<void> {
    const link = this.link();
    if (!link) {
      return;
    }
    try {
      await navigator.clipboard.writeText(link);
      this.copied.set(true);
    } catch {
      // Clipboard access can be denied; the link stays visible for manual copying.
      this.copied.set(false);
    }
  }

  protected deactivateLink(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.api.deactivateInvitation({ 'X-XSRF-TOKEN': '', storageId: this.storageId() }).subscribe({
      next: () => {
        this.link.set(null);
        this.linkExpiresAt.set(null);
        this.error.set(null);
        this.busy.set(false);
        this.loadInvitation();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(this.errors.messageFor(error));
      },
    });
  }

  protected removeMember(member: StorageMemberResponse): void {
    this.api
      .removeMember({ 'X-XSRF-TOKEN': '', storageId: this.storageId(), userId: member.userId })
      .subscribe({
        next: () => this.loadMembers(),
        error: (error: unknown) => this.error.set(this.errors.messageFor(error)),
      });
  }

  private loadMembers(): void {
    this.api.getMembers({ storageId: this.storageId() }).subscribe({
      next: (members) => this.members.set(members),
      error: (error: unknown) => this.error.set(this.errors.messageFor(error)),
    });
  }

  private loadInvitation(): void {
    const request = ++this.statusRequest;
    this.api.getInvitation({ storageId: this.storageId() }).subscribe({
      next: (status) => {
        if (request === this.statusRequest) {
          this.invitation.set(status);
        }
      },
      error: (error: unknown) => this.error.set(this.errors.messageFor(error)),
    });
  }
}
