import { loadTokens } from "./authStorage";

const baseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5224/api";

export async function downloadAnalyticsPdf(params: {
  from: string;
  to: string;
  grouping: number;
}): Promise<void> {
  const tokens = loadTokens();
  if (!tokens?.accessToken) {
    throw new Error("Authentication required.");
  }

  const query = new URLSearchParams({
    from: params.from,
    to: params.to,
    grouping: String(params.grouping)
  });

  const response = await fetch(`${baseUrl}/exports/analytics-pdf?${query.toString()}`, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${tokens.accessToken}`
    }
  });

  if (!response.ok) {
    throw new Error("Failed to export analytics report.");
  }

  const blob = await response.blob();
  const objectUrl = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = objectUrl;
  link.download = `analytics-report-${new Date().toISOString().slice(0, 10)}.pdf`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(objectUrl);
}
