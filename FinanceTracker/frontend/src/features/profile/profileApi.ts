import { api } from "../../shared/api/baseApi";
import type { ProfileDto } from "../../shared/types/api";

export interface UpdateProfileRequest {
  firstName?: string;
  lastName?: string;
  avatarUrl?: string;
  username?: string;
  email?: string;
}

export const profileApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getProfile: builder.query<ProfileDto, void>({
      query: () => ({ url: "/profile" }),
      providesTags: ["Profile", "Auth"]
    }),
    updateProfile: builder.mutation<ProfileDto, UpdateProfileRequest>({
      query: (body) => ({
        url: "/profile",
        method: "PUT",
        body
      }),
      invalidatesTags: ["Profile", "Auth"]
    })
  })
});

export const { useGetProfileQuery, useUpdateProfileMutation } = profileApi;
