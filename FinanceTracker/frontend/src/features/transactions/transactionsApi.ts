import { api } from "../../shared/api/baseApi";
import type { TransactionDto } from "../../shared/types/api";

export interface TransactionsQuery {
  from?: string;
  to?: string;
  accountId?: string;
  categoryId?: string;
  type?: number;
  search?: string;
}

export interface CreateTransactionRequest {
  accountId: string;
  categoryId?: string | null;
  type: number;
  amount: number;
  currencyCode: string;
  manualRate?: number | null;
  transactionDate: string;
  description?: string;
  source: number;
}

export interface UpdateTransactionRequest {
  id: string;
  accountId: string;
  categoryId?: string | null;
  amount: number;
  currencyCode: string;
  manualRate?: number | null;
  transactionDate: string;
  description?: string;
}

export const transactionsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getTransactions: builder.query<TransactionDto[], TransactionsQuery | undefined >({
      query: (params) => ({
        url: "/transactions",
        params
      }),
      providesTags: ["Transaction"]
    }),
    createTransaction: builder.mutation<TransactionDto, CreateTransactionRequest>({
      query: (body) => ({
        url: "/transactions",
        method: "POST",
        body
      }),
      invalidatesTags: ["Transaction", "Account", "Budget", "Analytics"]
    }),
    updateTransaction: builder.mutation<TransactionDto, UpdateTransactionRequest>({
      query: ({ id, ...body }) => ({
        url: `/transactions/${id}`,
        method: "PUT",
        body
      }),
      invalidatesTags: ["Transaction", "Account", "Budget", "Analytics"]
    }),
    deleteTransaction: builder.mutation<void, string>({
      query: (id) => ({
        url: `/transactions/${id}`,
        method: "DELETE"
      }),
      invalidatesTags: ["Transaction", "Account", "Budget", "Analytics"]
    })
  })
});

export const {
  useGetTransactionsQuery,
  useCreateTransactionMutation,
  useUpdateTransactionMutation,
  useDeleteTransactionMutation
} = transactionsApi;
