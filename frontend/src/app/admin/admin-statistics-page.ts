import { Component, OnInit, computed, inject, signal } from '@angular/core';

import { UsageStatisticsResponse } from '../api/models';
import { AdminService } from '../api/services';
import { ErrorMessages } from '../core/error-messages';
import { LanguageService } from '../core/language.service';
import { TranslatePipe } from '../core/translate';

/** One labelled number on the page. */
interface StatTile {
  labelKey: string;
  value: number;
}

/** A labelled list (sign-in providers, units) — the label is shown as given or translated. */
interface StatRow {
  label: string;
  translate: boolean;
  value: number;
}

/**
 * SPEC-008 AC-12: the operator's usage statistics — aggregates only, three groups of
 * tiles, the generation time and a refresh action. Numbers are formatted with the active
 * language; the expiry buckets come from the API (the client never re-implements the rule).
 */
@Component({
  selector: 'app-admin-statistics-page',
  imports: [TranslatePipe],
  templateUrl: './admin-statistics-page.html',
})
export class AdminStatisticsPage implements OnInit {
  private readonly adminApi = inject(AdminService);
  private readonly errors = inject(ErrorMessages);
  private readonly language = inject(LanguageService);

  protected readonly statistics = signal<UsageStatisticsResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  protected readonly userTiles = computed<StatTile[]>(() => {
    const users = this.statistics()?.users;
    return users
      ? [
          { labelKey: 'admin.statistics.users.total', value: users.total },
          { labelKey: 'admin.statistics.users.newLast7Days', value: users.newLast7Days },
          { labelKey: 'admin.statistics.users.newLast30Days', value: users.newLast30Days },
          {
            labelKey: 'admin.statistics.users.withoutAnyStorage',
            value: users.withoutAnyStorage,
          },
        ]
      : [];
  });

  protected readonly storageTiles = computed<StatTile[]>(() => {
    const stats = this.statistics();
    return stats
      ? [
          { labelKey: 'admin.statistics.storages.total', value: stats.storages.total },
          { labelKey: 'admin.statistics.storages.shared', value: stats.storages.shared },
          { labelKey: 'admin.statistics.storages.empty', value: stats.storages.empty },
          {
            labelKey: 'admin.statistics.storages.itemsPerStorageMedian',
            value: stats.storages.itemsPerStorageMedian,
          },
          {
            labelKey: 'admin.statistics.storages.itemsPerStorageMax',
            value: stats.storages.itemsPerStorageMax,
          },
          {
            labelKey: 'admin.statistics.storages.pendingInvitations',
            value: stats.invitations.pending,
          },
        ]
      : [];
  });

  protected readonly itemTiles = computed<StatTile[]>(() => {
    const items = this.statistics()?.items;
    return items
      ? [
          { labelKey: 'admin.statistics.items.total', value: items.total },
          { labelKey: 'admin.statistics.items.withExpiryDate', value: items.withExpiryDate },
          {
            labelKey: 'admin.statistics.items.withProductionDate',
            value: items.withProductionDate,
          },
          { labelKey: 'admin.statistics.items.expired', value: items.expired },
          { labelKey: 'admin.statistics.items.expiringSoon', value: items.expiringSoon },
        ]
      : [];
  });

  protected readonly issuerRows = computed<StatRow[]>(() =>
    Object.entries(this.statistics()?.users.byIssuer ?? {}).map(([issuer, value]) => ({
      label: issuer,
      translate: false,
      value,
    })),
  );

  protected readonly unitRows = computed<StatRow[]>(() =>
    Object.entries(this.statistics()?.items.byUnit ?? {}).map(([unit, value]) => ({
      label: 'units.' + unit,
      translate: true,
      value,
    })),
  );

  protected readonly generatedAt = computed(() => {
    const generatedAt = this.statistics()?.generatedAt;
    return generatedAt
      ? new Intl.DateTimeFormat(this.language.current(), {
          dateStyle: 'medium',
          timeStyle: 'short',
        }).format(new Date(generatedAt))
      : '';
  });

  ngOnInit(): void {
    this.load();
  }

  /** AC-12: one call on load, and again on *Refresh*. */
  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.adminApi.getUsageStatistics().subscribe({
      next: (statistics) => {
        this.statistics.set(statistics);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loadError.set(this.errors.messageFor(error));
        this.loading.set(false);
      },
    });
  }

  /** AC-13: numbers in the active locale (no Angular locale data registered in this app). */
  protected format(value: number): string {
    return new Intl.NumberFormat(this.language.current()).format(value);
  }
}
