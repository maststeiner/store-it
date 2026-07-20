import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';

import { ItemRequest, StorageItem, StorageSummary } from './models';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errorCode: string | null,
  ) {
    super(errorCode ?? `HTTP ${status}`);
    this.name = 'ApiError';
  }
}

const BASE_URL = '/api/v1';

@Injectable({ providedIn: 'root' })
export class StorageApi {
  private readonly http = inject(HttpClient);

  getStorages(): Observable<StorageSummary[]> {
    return this.http.get<StorageSummary[]>(`${BASE_URL}/storages`).pipe(this.mapError());
  }

  createStorage(name: string): Observable<StorageSummary> {
    return this.http.post<StorageSummary>(`${BASE_URL}/storages`, { name }).pipe(this.mapError());
  }

  renameStorage(id: string, name: string): Observable<void> {
    return this.http.put<void>(`${BASE_URL}/storages/${id}`, { name }).pipe(this.mapError());
  }

  deleteStorage(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE_URL}/storages/${id}`).pipe(this.mapError());
  }

  getItems(storageId: string): Observable<StorageItem[]> {
    return this.http
      .get<StorageItem[]>(`${BASE_URL}/storages/${storageId}/items`)
      .pipe(this.mapError());
  }

  addItem(storageId: string, request: ItemRequest): Observable<StorageItem> {
    return this.http
      .post<StorageItem>(`${BASE_URL}/storages/${storageId}/items`, request)
      .pipe(this.mapError());
  }

  updateItem(storageId: string, itemId: string, request: ItemRequest): Observable<void> {
    return this.http
      .put<void>(`${BASE_URL}/storages/${storageId}/items/${itemId}`, request)
      .pipe(this.mapError());
  }

  deleteItem(storageId: string, itemId: string): Observable<void> {
    return this.http
      .delete<void>(`${BASE_URL}/storages/${storageId}/items/${itemId}`)
      .pipe(this.mapError());
  }

  private mapError<T>(): (source: Observable<T>) => Observable<T> {
    return (source) =>
      source.pipe(
        catchError((error: unknown) => {
          if (error instanceof HttpErrorResponse) {
            return throwError(() => new ApiError(error.status, this.extractErrorCode(error)));
          }
          return throwError(() => error);
        }),
      );
  }

  private extractErrorCode(error: HttpErrorResponse): string | null {
    const body: unknown = error.error;
    if (body && typeof body === 'object' && 'errorCode' in body) {
      const code = (body as Record<string, unknown>)['errorCode'];
      if (typeof code === 'string') {
        return code;
      }
    }
    return null;
  }
}
