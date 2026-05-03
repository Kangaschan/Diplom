import { api } from "../../shared/api/baseApi";
import { BudgetPeriodType } from "../../shared/types/api";

export interface BudgetDto {
  id: string;
  categoryId: string;
  accountId?: string | null;
  limitAmount: number;
  currencyCode: string;
  periodType: BudgetPeriodType;
  startDate: string;
  endDate: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface BudgetUsageDto {
  budgetId: string;
  categoryId: string;
  categoryName: string;
  accountId?: string | null;
  accountName?: string | null;
  limitAmount: number;
  usedAmount: number;
  remainingAmount: number;
  percentUsed: number;
  isNearLimit: boolean;
  isExceeded: boolean;
  status: "normal" | "warning" | "exceeded";
  currencyCode: string;
  periodType: BudgetPeriodType;
  startDate: string;
  endDate: string;
}

export interface CreateBudgetRequest {
  categoryId: string;
  accountId?: string | null;
  limitAmount: number;
  currencyCode: string;
  periodType: BudgetPeriodType;
  startDate: string;
  endDate: string;
}

export interface UpdateBudgetRequest {
  id: string;
  limitAmount: number;
  startDate: string;
  endDate: string;
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
    }),
    createBudget: builder.mutation<BudgetDto, CreateBudgetRequest>({
      query: (body) => ({
        url: "/budgets",
        method: "POST",
        body
      }),
      invalidatesTags: ["Budget", "Notification"]
    }),
    updateBudget: builder.mutation<BudgetDto, UpdateBudgetRequest>({
      query: ({ id, ...body }) => ({
        url: `/budgets/${id}`,
        method: "PUT",
        body
      }),
      invalidatesTags: ["Budget", "Notification"]
    }),
    deleteBudget: builder.mutation<void, string>({
      query: (id) => ({
        url: `/budgets/${id}`,
        method: "DELETE"
      }),
      invalidatesTags: ["Budget", "Notification"]
    })
  })
});

export const {
  useGetBudgetsQuery,
  useGetBudgetsUsageQuery,
  useCreateBudgetMutation,
  useUpdateBudgetMutation,
  useDeleteBudgetMutation
} = budgetsApi;
