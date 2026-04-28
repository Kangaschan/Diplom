import { api } from "../../shared/api/baseApi";

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
  firstName?: string;
  lastName?: string;
}

export interface AuthTokenDto {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  tokenType: string;
}

export interface UserDto {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  hasActivePremium: boolean;
}

export const authApi = api.injectEndpoints({
  endpoints: (builder) => ({
    login: builder.mutation<AuthTokenDto, LoginRequest>({
      query: (body) => ({
        url: "/auth/login",
        method: "POST",
        body
      })
    }),
    register: builder.mutation<AuthTokenDto, RegisterRequest>({
      query: (body) => ({
        url: "/auth/register",
        method: "POST",
        body
      })
    }),
    me: builder.query<UserDto, void>({
      query: () => ({ url: "/auth/me" }),
      providesTags: ["Auth", "Profile"]
    })
  })
});

export const { useLoginMutation, useRegisterMutation, useMeQuery } = authApi;
