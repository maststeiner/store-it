import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { StorageMemberResponse } from '../api/models';
import { SharingService } from '../api/services';
import { ErrorMessages } from '../core/error-messages';
import { TranslatePipe } from '../core/translate';

/**
 * SPEC-007 AC-20 (D4): the owner of a storage that has members chooses between deleting it
 * for everyone and handing ownership to a member — and leaving (Marcel, 2026-09-23: "a").
 * Storages without members keep the plain ConfirmDialog.
 */
@Component({
  selector: 'app-storage-delete-dialog',
  imports: [TranslatePipe, FormsModule],
  templateUrl: './storage-delete-dialog.html',
})
export class StorageDeleteDialog implements OnInit, AfterViewInit {
  readonly storageId = input.required<string>();
  readonly storageName = input.required<string>();
  /** The owner chose "delete for everyone". */
  readonly deleteForEveryone = output<void>();
  /** The owner chose "hand over to <userId> and leave". */
  readonly handOver = output<string>();
  readonly cancelled = output<void>();

  private readonly api = inject(SharingService);
  private readonly errors = inject(ErrorMessages);

  protected readonly members = signal<StorageMemberResponse[] | null>(null);
  protected readonly others = computed(() => (this.members() ?? []).filter((m) => !m.isOwner));
  protected readonly mode = signal<'delete' | 'handOver'>('handOver');
  protected newOwnerId = '';
  protected readonly error = signal<string | null>(null);

  protected readonly canConfirm = computed(
    () => this.mode() === 'delete' || (this.others().length > 0 && this.newOwnerId !== ''),
  );

  private readonly dialog = viewChild.required<ElementRef<HTMLElement>>('dialog');
  private readonly firstControl = viewChild.required<ElementRef<HTMLElement>>('firstControl');

  ngOnInit(): void {
    this.api.getMembers({ storageId: this.storageId() }).subscribe({
      next: (members) => {
        this.members.set(members);
        const first = members.find((m) => !m.isOwner);
        this.newOwnerId = first?.userId ?? '';
      },
      error: (error: unknown) => this.error.set(this.errors.messageFor(error)),
    });
  }

  ngAfterViewInit(): void {
    this.firstControl().nativeElement.focus();
  }

  protected confirm(): void {
    if (!this.canConfirm()) {
      return;
    }
    if (this.mode() === 'delete') {
      this.deleteForEveryone.emit();
    } else {
      this.handOver.emit(this.newOwnerId);
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.cancelled.emit();
  }

  @HostListener('document:keydown.tab', ['$event'])
  @HostListener('document:keydown.shift.tab', ['$event'])
  protected onTab(event: Event): void {
    const keyEvent = event as KeyboardEvent;
    const focusables = Array.from(
      this.dialog().nativeElement.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input, select',
      ),
    );
    if (focusables.length === 0) {
      return;
    }
    const first = focusables[0];
    const last = focusables.at(-1)!;
    const active = document.activeElement;
    if (keyEvent.shiftKey && active === first) {
      last.focus();
      event.preventDefault();
    } else if (!keyEvent.shiftKey && active === last) {
      first.focus();
      event.preventDefault();
    }
  }
}
