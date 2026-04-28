import { api } from "../../shared/api/baseApi";
import type { DashboardDto } from "../../shared/types/api";

export const analyticsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getDashboardAnalytics: builder.query<DashboardDto, { from?: string; to?: string } | void>({
      query: (params) => ({
        url: "/analytics/dashboard",
        params
      }),
      providesTags: ["Analytics"]
    })
  })
});

export const { useGetDashboardAnalyticsQuery } = analyticsApi;
