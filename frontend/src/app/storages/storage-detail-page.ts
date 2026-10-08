import { DatePipe } from '@angular/common';
import {
  Component,
  ElementRef,
  HostListener,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ItemRequest, ItemResponse, StorageResponse, Unit } from '../api/models';
import { UNIT } from '../api/models/unit-array';
import { ItemsService, SharingService, StoragesService } from '../api/services';
import { ErrorMessages } from '../core/error-messages';
import { LanguageService } from '../core/language.service';
import { TranslatePipe } from '../core/translate';
import { ConfirmDialog } from '../shared/confirm-dialog';
import { matchesQuery, queryWords } from './item-search';
import { SharingPanel } from './sharing-panel';
import { StorageDeleteDialog } from './storage-delete-dialog';

interface ItemFormModel {
  name: string;
  amount: number | null;
  unit: Unit;
  expiryDate: string;
  productionDate: string;
}

function emptyForm(): ItemFormModel {
  return { name: '', amount: null, unit: 'Piece', expiryDate: '', productionDate: '' };
}

@Component({
  selector: 'app-storage-detail-page',
  imports: [
    FormsModule,
    TranslatePipe,
    DatePipe,
    RouterLink,
    ConfirmDialog,
    SharingPanel,
    StorageDeleteDialog,
  ],
  templateUrl: './storage-detail-page.html',
})
export class StorageDetailPage implements OnInit {
  private readonly storagesApi = inject(StoragesService);
  private readonly itemsApi = inject(ItemsService);
  private readonly sharingApi = inject(SharingService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly errors = inject(ErrorMessages);
  private readonly language = inject(LanguageService);

  protected readonly units = UNIT;
  /** Active UI locale for DatePipe (dates render per language, SPEC-001 i18n). */
  protected readonly locale = this.language.current;
  protected readonly storageId = this.route.snapshot.paramMap.get('id') ?? '';

  protected readonly storage = signal<StorageResponse | null>(null);
  protected readonly items = signal<ItemResponse[] | null>(null);
  protected readonly loadError = signal<string | null>(null);

  /** SPEC-010 D1: the search field is open (it stays open while it has a value). */
  protected readonly searchOpen = signal(false);
  /** SPEC-010 AC-04: the raw query; blank means "no filter". */
  protected readonly query = signal('');
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>('searchField');
  private readonly searchButton = viewChild<ElementRef<HTMLButtonElement>>('searchButton');

  /** True while the query carries at least one word (EC-01: whitespace is not a query). */
  protected readonly searching = computed(() => queryWords(this.query()).length > 0);

  /** SPEC-010 AC-04/AC-05: the filter runs before grouping, on the loaded list only (AC-06). */
  protected readonly filteredItems = computed(() => {
    const items = this.items() ?? [];
    const query = this.query();
    return this.searching() ? items.filter((item) => matchesQuery(item.name, query)) : items;
  });

  /** Pure presentation: grouping relies solely on the API-computed expiryStatus. */
  protected readonly groups = computed(() => {
    const items = this.filteredItems();
    return [
      {
        key: 'expired',
        labelKey: 'groups.expired',
        items: items.filter((item) => item.expiryStatus === 'Expired'),
      },
      {
        key: 'expiring',
        labelKey: 'groups.expiring',
        items: items.filter((item) => item.expiryStatus === 'ExpiringSoon'),
      },
      {
        key: 'rest',
        labelKey: 'groups.others',
        items: items.filter((item) => item.expiryStatus === 'Ok'),
      },
    ].filter((group) => group.items.length > 0);
  });

  /** #182: the add form is closed by default and opens behind "+ New item". */
  protected readonly addOpen = signal(false);
  protected form: ItemFormModel = emptyForm();
  protected readonly formError = signal<string | null>(null);
  private readonly addNameField = viewChild<ElementRef<HTMLInputElement>>('addNameField');

  protected readonly editItemId = signal<string | null>(null);
  protected editModel: ItemFormModel = emptyForm();
  protected readonly editError = signal<string | null>(null);

  protected readonly renaming = signal(false);
  protected renameValue = '';
  protected readonly renameError = signal<string | null>(null);

  protected readonly deleteOpen = signal(false);
  /** SPEC-007: the owner's share view / the member list (D7), toggled from the header. */
  protected readonly sharingOpen = signal(false);
  /** SPEC-007 D4: members leave; the owner deletes. */
  protected readonly leaveOpen = signal(false);

  ngOnInit(): void {
    this.loadStorage();
    this.loadItems();
  }

  protected openAdd(): void {
    this.form = emptyForm();
    this.formError.set(null);
    this.addOpen.set(true);
    // The field exists only after the next render; focus it then (AC-02).
    setTimeout(() => this.addNameField()?.nativeElement.focus());
  }

  /** AC-05: Cancel / Escape close the form and discard the input. */
  protected cancelAdd(): void {
    this.addOpen.set(false);
    this.form = emptyForm();
    this.formError.set(null);
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.addOpen()) {
      this.cancelAdd();
    }
  }

  /**
   * SPEC-010 AC-01/AC-03: the header button opens the field and focuses it; with an empty
   * field it closes again; with a value it only refocuses — a stray click never drops a query.
   */
  protected toggleSearch(): void {
    if (this.searchOpen() && !this.searching()) {
      this.searchOpen.set(false);
      this.query.set('');
      return;
    }
    this.searchOpen.set(true);
    // The field exists only after the next render; focus it then.
    setTimeout(() => this.searchField()?.nativeElement.focus());
  }

  /** SPEC-010 AC-02: clear button or Escape — remove the filter, close, focus the button. */
  protected clearSearch(): void {
    this.query.set('');
    this.searchOpen.set(false);
    this.searchButton()?.nativeElement.focus();
  }

  /** D6: Escape inside the field is the field's; it must not also close the add form. */
  protected onSearchEscape(event: Event): void {
    event.stopPropagation();
    this.clearSearch();
  }

  protected addItem(): void {
    this.itemsApi
      .addItem({ 'X-XSRF-TOKEN': '', storageId: this.storageId, body: this.toRequest(this.form) })
      .subscribe({
        next: () => {
          // AC-03: a successful add closes the form again.
          this.cancelAdd();
          this.loadItems();
          this.loadStorage();
        },
        // AC-04: a validation error keeps the form open with the message.
        error: (error: unknown) => this.formError.set(this.errors.messageFor(error)),
      });
  }

  protected startItemEdit(item: ItemResponse): void {
    this.editItemId.set(item.id);
    this.editError.set(null);
    this.editModel = {
      name: item.name,
      amount: item.amount,
      unit: item.unit,
      expiryDate: item.expiryDate?.slice(0, 10) ?? '',
      productionDate: item.productionDate?.slice(0, 10) ?? '',
    };
  }

  protected cancelItemEdit(): void {
    this.editItemId.set(null);
    this.editError.set(null);
  }

  protected saveItem(item: ItemResponse): void {
    this.itemsApi
      .updateItem({
        'X-XSRF-TOKEN': '',
        storageId: this.storageId,
        itemId: item.id,
        body: this.toRequest(this.editModel),
      })
      .subscribe({
        next: () => {
          this.editItemId.set(null);
          this.loadItems();
          this.loadStorage();
        },
        error: (error: unknown) => this.editError.set(this.errors.messageFor(error)),
      });
  }

  protected deleteItem(item: ItemResponse): void {
    this.itemsApi
      .deleteItem({ 'X-XSRF-TOKEN': '', storageId: this.storageId, itemId: item.id })
      .subscribe({
        next: () => {
          this.loadItems();
          this.loadStorage();
        },
        error: (error: unknown) => this.loadError.set(this.errors.messageFor(error)),
      });
  }

  protected startRename(): void {
    this.renameValue = this.storage()?.name ?? '';
    this.renameError.set(null);
    this.renaming.set(true);
  }

  protected saveRename(): void {
    this.storagesApi
      .renameStorage({
        'X-XSRF-TOKEN': '',
        storageId: this.storageId,
        body: { name: this.renameValue.trim() },
      })
      .subscribe({
        next: () => {
          this.renaming.set(false);
          this.loadStorage();
        },
        error: (error: unknown) => this.renameError.set(this.errors.messageFor(error)),
      });
  }

  protected confirmDelete(): void {
    this.storagesApi.deleteStorage({ 'X-XSRF-TOKEN': '', storageId: this.storageId }).subscribe({
      next: () => {
        this.deleteOpen.set(false);
        void this.router.navigate(['/storages']);
      },
      error: (error: unknown) => {
        this.deleteOpen.set(false);
        this.loadError.set(this.errors.messageFor(error));
      },
    });
  }

  /**
   * SPEC-007 AC-20, decision "a": hand the storage to a member and leave it — two calls, the
   * second harmless to fail (the user simply stays a member and sees the error).
   */
  protected confirmHandOver(newOwnerId: string): void {
    this.sharingApi
      .transferOwnership({
        'X-XSRF-TOKEN': '',
        storageId: this.storageId,
        body: { userId: newOwnerId },
      })
      .subscribe({
        next: () => {
          this.sharingApi
            .leaveStorage({ 'X-XSRF-TOKEN': '', storageId: this.storageId })
            .subscribe({
              next: () => {
                this.deleteOpen.set(false);
                void this.router.navigate(['/storages']);
              },
              error: (error: unknown) => {
                this.deleteOpen.set(false);
                this.loadStorage();
                this.loadError.set(this.errors.messageFor(error));
              },
            });
        },
        error: (error: unknown) => {
          this.deleteOpen.set(false);
          this.loadError.set(this.errors.messageFor(error));
        },
      });
  }

  /** The panel handed ownership to a member: the header switches to the member view. */
  protected onOwnershipChanged(): void {
    this.loadStorage();
  }

  protected confirmLeave(): void {
    this.sharingApi.leaveStorage({ 'X-XSRF-TOKEN': '', storageId: this.storageId }).subscribe({
      next: () => {
        this.leaveOpen.set(false);
        void this.router.navigate(['/storages']);
      },
      error: (error: unknown) => {
        this.leaveOpen.set(false);
        this.loadError.set(this.errors.messageFor(error));
      },
    });
  }

  private loadStorage(): void {
    this.storagesApi.getStorage({ storageId: this.storageId }).subscribe({
      next: (storage) => this.storage.set(storage),
      error: (error: unknown) => this.loadError.set(this.errors.messageFor(error)),
    });
  }

  private loadItems(): void {
    this.itemsApi.getItems({ storageId: this.storageId }).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loadError.set(null);
      },
      error: (error: unknown) => this.loadError.set(this.errors.messageFor(error)),
    });
  }

  private toRequest(model: ItemFormModel): ItemRequest {
    return {
      name: model.name.trim(),
      amount: model.amount ?? 0,
      unit: model.unit,
      expiryDate: model.expiryDate || null,
      productionDate: model.productionDate || null,
    };
  }
}
