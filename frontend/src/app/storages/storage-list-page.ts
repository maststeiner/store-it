import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { StorageResponse } from '../api/models';
import { StoragesService } from '../api/services';
import { ErrorMessages } from '../core/error-messages';
import { TranslatePipe } from '../core/translate';
import { ConfirmDialog } from '../shared/confirm-dialog';

@Component({
  selector: 'app-storage-list-page',
  imports: [FormsModule, TranslatePipe, ConfirmDialog],
  templateUrl: './storage-list-page.html',
})
export class StorageListPage implements OnInit {
  private readonly storagesApi = inject(StoragesService);
  private readonly router = inject(Router);
  private readonly errors = inject(ErrorMessages);

  protected readonly storages = signal<StorageResponse[] | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly totalItems = computed(() =>
    (this.storages() ?? []).reduce((sum, storage) => sum + storage.itemCount, 0),
  );

  protected readonly createOpen = signal(false);
  protected createName = '';
  protected readonly createError = signal<string | null>(null);

  protected readonly editId = signal<string | null>(null);
  protected editName = '';
  protected readonly editError = signal<string | null>(null);

  protected readonly deleteTarget = signal<StorageResponse | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected open(storage: StorageResponse): void {
    this.router.navigate(['/storages', storage.id]);
  }

  protected openCreate(): void {
    this.createName = '';
    this.createError.set(null);
    this.createOpen.set(true);
  }

  protected cancelCreate(): void {
    this.createOpen.set(false);
    this.createError.set(null);
  }

  protected create(): void {
    this.storagesApi
      .createStorage({ 'X-XSRF-TOKEN': '', body: { name: this.createName.trim() } })
      .subscribe({
        next: () => {
          this.createOpen.set(false);
          this.createName = '';
          this.load();
        },
        error: (error: unknown) => this.createError.set(this.errors.messageFor(error)),
      });
  }

  protected startEdit(storage: StorageResponse, event: Event): void {
    event.stopPropagation();
    this.editId.set(storage.id);
    this.editName = storage.name;
    this.editError.set(null);
  }

  protected cancelEdit(): void {
    this.editId.set(null);
    this.editError.set(null);
  }

  protected saveEdit(storage: StorageResponse): void {
    this.storagesApi
      .renameStorage({
        'X-XSRF-TOKEN': '',
        storageId: storage.id,
        body: { name: this.editName.trim() },
      })
      .subscribe({
        next: () => {
          this.editId.set(null);
          this.load();
        },
        error: (error: unknown) => this.editError.set(this.errors.messageFor(error)),
      });
  }

  protected askDelete(storage: StorageResponse, event: Event): void {
    event.stopPropagation();
    this.deleteTarget.set(storage);
  }

  protected confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target) {
      return;
    }
    this.storagesApi.deleteStorage({ 'X-XSRF-TOKEN': '', storageId: target.id }).subscribe({
      next: () => {
        this.deleteTarget.set(null);
        this.load();
      },
      error: (error: unknown) => {
        this.deleteTarget.set(null);
        this.loadError.set(this.errors.messageFor(error));
      },
    });
  }

  private load(): void {
    this.storagesApi.getStorages().subscribe({
      next: (storages) => {
        this.storages.set(storages);
        this.loadError.set(null);
      },
      error: (error: unknown) => this.loadError.set(this.errors.messageFor(error)),
    });
  }
}
