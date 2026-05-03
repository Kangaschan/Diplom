import {
  BaseQueryFn,
  createApi,
  fetchBaseQuery,
  FetchArgs,
  FetchBaseQueryError
} from "@reduxjs/toolkit/query/react";

import type { RootState } from "../../app/providers/store";
import { clearAuth, setTokens } from "../../features/auth/authSlice";

const baseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5224/api";

const rawBaseQuery = fetchBaseQuery({
  baseUrl,
  prepareHeaders: (headers, { getState }) => {
    const state = getState() as RootState;
    const token = state.auth.accessToken;
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    return headers;
  }
});

const baseQueryWithReauth: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (
  args,
  api,
  extraOptions
) => {
  let result = await rawBaseQuery(args, api, extraOptions);

  if (result.error?.status !== 401) {
    return result;
  }

  const state = api.getState() as RootState;
  const refreshToken = state.auth.refreshToken;
  if (!refreshToken) {
    api.dispatch(clearAuth());
    return result;
  }

  const refreshResult = await rawBaseQuery(
    {
      url: "/auth/refresh",
      method: "POST",
      body: { refreshToken }
    },
    api,
    extraOptions
  );

  if (refreshResult.data && typeof refreshResult.data === "object") {
    const tokens = refreshResult.data as { accessToken?: string; refreshToken?: string };
    if (tokens.accessToken && tokens.refreshToken) {
      api.dispatch(setTokens({ accessToken: tokens.accessToken, refreshToken: tokens.refreshToken }));
      result = await rawBaseQuery(args, api, extraOptions);
      return result;
    }
  }

  api.dispatch(clearAuth());
  return result;
};

export const api = createApi({
  reducerPath: "api",
  baseQuery: baseQueryWithReauth,
  tagTypes: [
    "Auth",
    "Account",
    "Transfer",
    "Transaction",
    "Profile",
    "Subscription",
    "Notification",
    "Category",
    "Budget",
    "Analytics",
    "Receipt"
  ],
  endpoints: () => ({})
});
