import { Routes } from '@angular/router';

import { JoinPage } from './auth/join-page';
import { LoginPage } from './auth/login-page';
import { authGuard } from './core/auth.guard';
import { StorageDetailPage } from './storages/storage-detail-page';
import { StorageListPage } from './storages/storage-list-page';

export const routes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'join', component: JoinPage, canActivate: [authGuard] },
  { path: 'storages', component: StorageListPage, canActivate: [authGuard] },
  { path: 'storages/:id', component: StorageDetailPage, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'storages' },
  { path: '**', redirectTo: 'storages' },
];
