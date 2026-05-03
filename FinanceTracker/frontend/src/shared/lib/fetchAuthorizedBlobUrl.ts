import { loadTokens } from "./authStorage";

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5224/api";

export async function fetchAuthorizedBlobUrl(relativeApiPath: string): Promise<string> {
  const tokens = loadTokens();
  if (!tokens?.accessToken) {
    throw new Error("Missing access token");
  }

  const normalizedPath = relativeApiPath.startsWith("/") ? relativeApiPath : `/${relativeApiPath}`;
  const response = await fetch(`${apiBaseUrl}${normalizedPath}`, {
    headers: {
      Authorization: `Bearer ${tokens.accessToken}`
    }
  });

  if (!response.ok) {
    throw new Error("Failed to load receipt preview");
  }

  const blob = await response.blob();
  return URL.createObjectURL(blob);
}
