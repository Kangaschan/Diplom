import { api } from "../../shared/api/baseApi";
import type { CategoryDto } from "../../shared/types/api";

export const categoriesApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getCategories: builder.query<CategoryDto[], void>({
      query: () => ({ url: "/categories" }),
      providesTags: ["Category"]
    })
  })
});

export const { useGetCategoriesQuery } = categoriesApi;
