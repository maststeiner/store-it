import { Routes } from '@angular/router';

import { JoinPage } from './auth/join-page';
import { LoginPage } from './auth/login-page';
import { adminGuard } from './core/admin.guard';
import { authGuard } from './core/auth.guard';
import { StorageDetailPage } from './storages/storage-detail-page';
import { StorageListPage } from './storages/storage-list-page';

export const routes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'join', component: JoinPage, canActivate: [authGuard] },
  { path: 'storages', component: StorageListPage, canActivate: [authGuard] },
  { path: 'storages/:id', component: StorageDetailPage, canActivate: [authGuard] },
  {
    // SPEC-008: the operator's usage statistics — lazy, admins only (AC-11).
    path: 'admin',
    loadComponent: () => import('./admin/admin-statistics-page').then((m) => m.AdminStatisticsPage),
    canActivate: [authGuard, adminGuard],
  },
  { path: '', pathMatch: 'full', redirectTo: 'storages' },
  { path: '**', redirectTo: 'storages' },
];
