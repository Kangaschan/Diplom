interface ExchangeRateResponse {
  result: string;
  conversion_rates?: Record<string, number>;
}

const apiKey = import.meta.env.VITE_EXCHANGE_RATE_API_KEY;
const baseUrl = import.meta.env.VITE_EXCHANGE_RATE_API_URL ?? "https://v6.exchangerate-api.com/v6";

export async function getExchangeRate(from: string, to: string): Promise<number | null> {
  if (!from || !to) {
    return null;
  }

  if (from.toUpperCase() === to.toUpperCase()) {
    return 1;
  }

  if (!apiKey) {
    return null;
  }

  const response = await fetch(`${baseUrl}/${apiKey}/latest/${from.toUpperCase()}`);
  if (!response.ok) {
    return null;
  }

  const payload = (await response.json()) as ExchangeRateResponse;
  if (payload.result !== "success" || !payload.conversion_rates) {
    return null;
  }

  return payload.conversion_rates[to.toUpperCase()] ?? null;
}
