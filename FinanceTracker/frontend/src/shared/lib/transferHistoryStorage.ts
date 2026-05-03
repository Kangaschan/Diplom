export interface TransferHistoryEntry {
  id: string;
  createdAt: string;
  fromAccountName: string;
  toAccountName: string;
  amountSent: number;
  amountReceived: number;
  sourceCurrency: string;
  targetCurrency: string;
  estimatedRate: number | null;
  manualRate?: number | null;
  description?: string;
}

const KEY = "ft_transfer_history";

export function getTransferHistory(): TransferHistoryEntry[] {
  const raw = localStorage.getItem(KEY);
  if (!raw) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw) as TransferHistoryEntry[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function pushTransferHistory(entry: TransferHistoryEntry): void {
  const history = getTransferHistory();
  const next = [entry, ...history].slice(0, 300);
  localStorage.setItem(KEY, JSON.stringify(next));
}
