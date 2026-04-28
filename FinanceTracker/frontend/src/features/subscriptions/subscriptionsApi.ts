import { api } from "../../shared/api/baseApi";

export interface CurrentSubscriptionDto {
  type?: string;
  status?: string;
  endDate?: string | null;
}

export const subscriptionsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getCurrentSubscription: builder.query<CurrentSubscriptionDto, void>({
      query: () => ({ url: "/subscriptions/current" }),
      providesTags: ["Subscription"]
    })
  })
});

export const { useGetCurrentSubscriptionQuery } = subscriptionsApi;
