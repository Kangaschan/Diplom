export function formatMoney(value: number, currencyCode = "USD"): string {
  const normalizedCurrencyCode = normalizeCurrencyCode(currencyCode);

  if (!normalizedCurrencyCode) {
    return `${value.toFixed(2)} ${currencyCode || ""}`.trim();
  }

  try {
    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: normalizedCurrencyCode,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value);
  } catch {
    return `${value.toFixed(2)} ${normalizedCurrencyCode}`.trim();
  }
}

function normalizeCurrencyCode(currencyCode?: string | null): string | null {
  if (!currencyCode) {
    return null;
  }

  const normalized = currencyCode.trim().toUpperCase();
  if (/^[A-Z]{3}$/.test(normalized)) {
    return normalized;
  }

  switch (normalized) {
    case "₽":
    case "Р":
    case "РУБ":
    case "RUR":
      return "RUB";
    case "$":
      return "USD";
    case "€":
      return "EUR";
    case "BYR":
      return "BYN";
    default:
      return null;
  }
}
