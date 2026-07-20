import { Routes } from '@angular/router';

import { StorageDetailPage } from './storages/storage-detail-page';
import { StorageListPage } from './storages/storage-list-page';

export const routes: Routes = [
  { path: 'storages', component: StorageListPage },
  { path: 'storages/:id', component: StorageDetailPage },
  { path: '', pathMatch: 'full', redirectTo: 'storages' },
  { path: '**', redirectTo: 'storages' },
];
