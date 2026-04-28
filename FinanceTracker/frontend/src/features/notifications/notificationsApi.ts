import { api } from "../../shared/api/baseApi";
import type { NotificationDto } from "../../shared/types/api";

function toUnreadCount(payload: unknown): number {
  if (typeof payload === "number") {
    return payload;
  }

  if (payload && typeof payload === "object") {
    const candidate = (payload as { count?: unknown; unreadCount?: unknown }).count ??
      (payload as { count?: unknown; unreadCount?: unknown }).unreadCount;

    if (typeof candidate === "number") {
      return candidate;
    }
  }

  return 0;
}

export const notificationsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getNotifications: builder.query<NotificationDto[], { unreadOnly?: boolean } | void>({
      query: (params) => ({
        url: "/notifications",
        params
      }),
      providesTags: ["Notification"]
    }),
    getUnreadCount: builder.query<number, void>({
      query: () => ({ url: "/notifications/unread-count" }),
      transformResponse: toUnreadCount,
      providesTags: ["Notification"]
    }),
    markRead: builder.mutation<void, string>({
      query: (id) => ({
        url: `/notifications/${id}/read`,
        method: "POST"
      }),
      invalidatesTags: ["Notification"]
    }),
    markAllRead: builder.mutation<void, void>({
      query: () => ({
        url: "/notifications/read-all",
        method: "POST"
      }),
      invalidatesTags: ["Notification"]
    })
  })
});

export const {
  useGetNotificationsQuery,
  useGetUnreadCountQuery,
  useMarkReadMutation,
  useMarkAllReadMutation
} = notificationsApi;
