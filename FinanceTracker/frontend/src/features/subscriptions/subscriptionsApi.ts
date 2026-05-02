import { api } from "../../shared/api/baseApi";

export interface CurrentSubscriptionDto {
  id: string;
  userId: string;
  type: number;
  status: number;
  startDate: string;
  endDate: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface SubscriptionPriceDto {
  id: string;
  name: string;
  durationDays: number;
}

export interface SubscriptionPlanDto {
  name: string;
  prices: SubscriptionPriceDto[];
}

export const subscriptionsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getCurrentSubscription: builder.query<CurrentSubscriptionDto | null, void>({
      query: () => ({ url: "/subscriptions/current" }),
      providesTags: ["Subscription"]
    }),
    getSubscriptionHistory: builder.query<CurrentSubscriptionDto[], void>({
      query: () => ({ url: "/subscriptions/history" }),
      providesTags: ["Subscription"]
    }),
    getSubscriptionPlans: builder.query<SubscriptionPlanDto[], void>({
      query: () => ({ url: "/subscriptions/plans" }),
      providesTags: ["Subscription"]
    }),
    createCheckoutSession: builder.mutation<string, { priceId: string }>({
      query: (body) => ({
        url: "/subscriptions/checkout",
        method: "POST",
        body
      })
    }),
    createPortalSession: builder.mutation<string, void>({
      query: () => ({
        url: "/subscriptions/portal",
        method: "POST"
      })
    })
  })
});

export const {
  useGetCurrentSubscriptionQuery,
  useGetSubscriptionHistoryQuery,
  useGetSubscriptionPlansQuery,
  useCreateCheckoutSessionMutation,
  useCreatePortalSessionMutation
} = subscriptionsApi;
