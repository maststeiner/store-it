import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ItemRequest, ItemResponse, StorageResponse, Unit } from '../api/models';
import { UNIT } from '../api/models/unit-array';
import { ItemsService, StoragesService } from '../api/services';
import { ErrorMessages } from '../core/error-messages';
import { LanguageService } from '../core/language.service';
import { TranslatePipe } from '../core/translate';
import { ConfirmDialog } from '../shared/confirm-dialog';

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
  imports: [FormsModule, TranslatePipe, DatePipe, RouterLink, ConfirmDialog],
  templateUrl: './storage-detail-page.html',
})
export class StorageDetailPage implements OnInit {
  private readonly storagesApi = inject(StoragesService);
  private readonly itemsApi = inject(ItemsService);
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

  /** Pure presentation: grouping relies solely on the API-computed expiryStatus. */
  protected readonly groups = computed(() => {
    const items = this.items() ?? [];
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

  protected form: ItemFormModel = emptyForm();
  protected readonly formError = signal<string | null>(null);

  protected readonly editItemId = signal<string | null>(null);
  protected editModel: ItemFormModel = emptyForm();
  protected readonly editError = signal<string | null>(null);

  protected readonly renaming = signal(false);
  protected renameValue = '';
  protected readonly renameError = signal<string | null>(null);

  protected readonly deleteOpen = signal(false);

  ngOnInit(): void {
    this.loadStorage();
    this.loadItems();
  }

  protected addItem(): void {
    this.itemsApi
      .addItem({ 'X-XSRF-TOKEN': '', storageId: this.storageId, body: this.toRequest(this.form) })
      .subscribe({
        next: () => {
          this.form = emptyForm();
          this.formError.set(null);
          this.loadItems();
          this.loadStorage();
        },
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
        this.router.navigate(['/storages']);
      },
      error: (error: unknown) => {
        this.deleteOpen.set(false);
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
