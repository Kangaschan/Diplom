import { api } from "../../shared/api/baseApi";
import type {
  AccountDistributionDto,
  AnalyticsCategoryDto,
  BalanceHistoryPointDto,
  CashFlowPointDto,
  DashboardDto,
  PremiumComparisonDto,
  RecurringPaymentsAnalyticsDto
} from "../../shared/types/api";

export interface AnalyticsPeriodParams {
  from: string;
  to: string;
}

export interface CashFlowParams extends AnalyticsPeriodParams {
  grouping: number;
}

export interface PremiumCompareParams {
  previousFrom: string;
  previousTo: string;
  currentFrom: string;
  currentTo: string;
}

export const analyticsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getDashboardAnalytics: builder.query<DashboardDto, AnalyticsPeriodParams>({
      query: (params) => ({
        url: "/analytics/dashboard",
        params
      }),
      providesTags: ["Analytics"]
    }),
    getExpensesByCategoryAnalytics: builder.query<AnalyticsCategoryDto[], AnalyticsPeriodParams>({
      query: (params) => ({
        url: "/analytics/expenses-by-category",
        params
      }),
      providesTags: ["Analytics"]
    }),
    getCashFlowAnalytics: builder.query<CashFlowPointDto[], CashFlowParams>({
      query: (params) => ({
        url: "/analytics/cash-flow",
        params
      }),
      providesTags: ["Analytics"]
    }),
    getBalanceHistoryAnalytics: builder.query<BalanceHistoryPointDto[], CashFlowParams>({
      query: (params) => ({
        url: "/analytics/balance-history",
        params
      }),
      providesTags: ["Analytics"]
    }),
    getAccountsDistributionAnalytics: builder.query<AccountDistributionDto[], void>({
      query: () => ({
        url: "/analytics/accounts-distribution"
      }),
      providesTags: ["Analytics"]
    }),
    getRecurringPaymentsAnalytics: builder.query<RecurringPaymentsAnalyticsDto, AnalyticsPeriodParams>({
      query: (params) => ({
        url: "/analytics/recurring-payments",
        params
      }),
      providesTags: ["Analytics"]
    }),
    getPremiumComparisonAnalytics: builder.query<PremiumComparisonDto, PremiumCompareParams>({
      query: (params) => ({
        url: "/analytics/premium/compare",
        params
      }),
      providesTags: ["Analytics"]
    })
  })
});

export const {
  useGetDashboardAnalyticsQuery,
  useGetExpensesByCategoryAnalyticsQuery,
  useGetCashFlowAnalyticsQuery,
  useGetBalanceHistoryAnalyticsQuery,
  useGetAccountsDistributionAnalyticsQuery,
  useGetRecurringPaymentsAnalyticsQuery,
  useGetPremiumComparisonAnalyticsQuery
} = analyticsApi;
