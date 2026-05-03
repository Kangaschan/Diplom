import { api } from "../../shared/api/baseApi";
import type { AccountDto } from "../../shared/types/api";

export interface CreateAccountRequest {
  name: string;
  currencyCode: string;
  initialBalance: number;
}

export interface UpdateAccountRequest {
  name: string;
  isArchived: boolean;
  financialGoalAmount?: number | null;
  financialGoalDeadline?: string | null;
}

export interface SetBalanceRequest {
  newBalance: number;
}

export interface TransferRequest {
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  currencyCode: string;
  manualRate?: number | null;
  description?: string;
}

export interface TransferDto {
  id: string;
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  currencyCode: string;
  transferDate: string;
  description?: string | null;
}

export const accountsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getAccounts: builder.query<AccountDto[], { includeArchived?: boolean } | void>({
      query: (params) => ({
        url: "/accounts",
        params: { includeArchived: params?.includeArchived ?? false }
      }),
      providesTags: ["Account"]
    }),
    createAccount: builder.mutation<AccountDto, CreateAccountRequest>({
      query: (body) => ({
        url: "/accounts",
        method: "POST",
        body
      }),
      invalidatesTags: ["Account"]
    }),
    updateAccount: builder.mutation<AccountDto, { id: string; body: UpdateAccountRequest }>({
      query: ({ id, body }) => ({
        url: `/accounts/${id}`,
        method: "PUT",
        body
      }),
      invalidatesTags: ["Account"]
    }),
    archiveAccount: builder.mutation<void, string>({
      query: (id) => ({
        url: `/accounts/${id}/archive`,
        method: "POST"
      }),
      invalidatesTags: ["Account"]
    }),
    setBalance: builder.mutation<AccountDto, { id: string; body: SetBalanceRequest }>({
      query: ({ id, body }) => ({
        url: `/accounts/${id}/balance`,
        method: "PATCH",
        body
      }),
      invalidatesTags: ["Account", "Transaction"]
    }),
    transfer: builder.mutation<TransferDto, TransferRequest>({
      query: (body) => ({
        url: "/accounts/transfer",
        method: "POST",
        body
      }),
      invalidatesTags: ["Account", "Transfer", "Transaction", "Analytics"]
    })
  })
});

export const {
  useGetAccountsQuery,
  useCreateAccountMutation,
  useUpdateAccountMutation,
  useArchiveAccountMutation,
  useSetBalanceMutation,
  useTransferMutation
} = accountsApi;
