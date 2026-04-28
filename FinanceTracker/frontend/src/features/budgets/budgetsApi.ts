import { api } from "../../shared/api/baseApi";

export interface BudgetDto {
  id: string;
  categoryId: string;
  accountId?: string | null;
  limitAmount: number;
  currencyCode: string;
  startDate: string;
  endDate: string;
}

export interface BudgetUsageDto {
  budgetId?: string;
  used?: number;
  usagePercent?: number;
  title?: string;
}

export const budgetsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getBudgets: builder.query<BudgetDto[], void>({
      query: () => ({ url: "/budgets" }),
      providesTags: ["Budget"]
    }),
    getBudgetsUsage: builder.query<BudgetUsageDto[], void>({
      query: () => ({ url: "/budgets/usage" }),
      providesTags: ["Budget"]
    })
  })
});

export const { useGetBudgetsQuery, useGetBudgetsUsageQuery } = budgetsApi;
