import { api } from "../../shared/api/baseApi";
import type { RecurringPaymentDto } from "../../shared/types/api";

export interface CreateRecurringPaymentRequest {
  name: string;
  description?: string | null;
  accountId: string;
  categoryId?: string | null;
  type: number;
  amount: number;
  currencyCode: string;
  frequency: string;
  firstExecutionDate: string;
  endDate?: string | null;
}

export interface UpdateRecurringPaymentRequest extends CreateRecurringPaymentRequest {
  id: string;
  isActive: boolean;
}

export interface SetRecurringPaymentActiveRequest {
  id: string;
  isActive: boolean;
}

export const recurringPaymentsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getRecurringPayments: builder.query<RecurringPaymentDto[], void>({
      query: () => ({
        url: "/recurring-payments"
      }),
      providesTags: ["RecurringPayment"]
    }),
    createRecurringPayment: builder.mutation<RecurringPaymentDto, CreateRecurringPaymentRequest>({
      query: (body) => ({
        url: "/recurring-payments",
        method: "POST",
        body
      }),
      invalidatesTags: ["RecurringPayment", "Analytics"]
    }),
    updateRecurringPayment: builder.mutation<RecurringPaymentDto, UpdateRecurringPaymentRequest>({
      query: ({ id, ...body }) => ({
        url: `/recurring-payments/${id}`,
        method: "PUT",
        body
      }),
      invalidatesTags: ["RecurringPayment", "Analytics"]
    }),
    setRecurringPaymentActive: builder.mutation<RecurringPaymentDto, SetRecurringPaymentActiveRequest>({
      query: ({ id, isActive }) => ({
        url: `/recurring-payments/${id}/active`,
        method: "PATCH",
        body: { isActive }
      }),
      invalidatesTags: ["RecurringPayment", "Analytics"]
    }),
    deleteRecurringPayment: builder.mutation<void, string>({
      query: (id) => ({
        url: `/recurring-payments/${id}`,
        method: "DELETE"
      }),
      invalidatesTags: ["RecurringPayment", "Analytics"]
    })
  })
});

export const {
  useGetRecurringPaymentsQuery,
  useCreateRecurringPaymentMutation,
  useUpdateRecurringPaymentMutation,
  useSetRecurringPaymentActiveMutation,
  useDeleteRecurringPaymentMutation
} = recurringPaymentsApi;
