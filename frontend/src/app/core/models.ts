export const UNITS = ['Piece', 'Gram', 'Kilogram', 'Milliliter', 'Liter', 'Pack'] as const;

export type Unit = (typeof UNITS)[number];

export type ExpiryStatus = 'Ok' | 'ExpiringSoon' | 'Expired';

export interface StorageSummary {
  id: string;
  name: string;
  itemCount: number;
  /** Server-computed per AC-01a / ADR-002 — the UI only presents these. */
  expiredCount: number;
  expiringSoonCount: number;
}

export interface StorageItem {
  id: string;
  name: string;
  amount: number;
  unit: Unit;
  expiryDate: string | null;
  productionDate: string | null;
  expiryStatus: ExpiryStatus;
}

export interface ItemRequest {
  name: string;
  amount: number;
  unit: Unit;
  expiryDate: string | null;
  productionDate: string | null;
}
