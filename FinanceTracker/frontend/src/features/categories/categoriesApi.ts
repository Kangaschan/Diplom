import { api } from "../../shared/api/baseApi";
import type { CategoryDto, CategoryExpenseStatsDto } from "../../shared/types/api";

type CreateCategoryRequest = {
  name: string;
  type: number;
};

type UpdateCategoryRequest = {
  id: string;
  name: string;
  isActive: boolean;
};

export const categoriesApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getCategories: builder.query<CategoryDto[], void>({
      query: () => ({ url: "/categories" }),
      providesTags: ["Category"]
    }),
    getExpenseCategoryStats: builder.query<CategoryExpenseStatsDto[], 1 | 2 | 3>({
      query: (period) => ({
        url: "/categories/expense-stats",
        params: { period }
      }),
      providesTags: ["Category", "Analytics"]
    }),
    createCategory: builder.mutation<void, CreateCategoryRequest>({
      query: (body) => ({
        url: "/categories",
        method: "POST",
        body
      }),
      invalidatesTags: ["Category"]
    }),
    updateCategory: builder.mutation<void, UpdateCategoryRequest>({
      query: ({ id, ...body }) => ({
        url: `/categories/${id}`,
        method: "PUT",
        body
      }),
      invalidatesTags: ["Category"]
    }),
    deleteCategory: builder.mutation<void, string>({
      query: (id) => ({
        url: `/categories/${id}`,
        method: "DELETE"
      }),
      invalidatesTags: ["Category"]
    })
  })
});

export const {
  useGetCategoriesQuery,
  useGetExpenseCategoryStatsQuery,
  useCreateCategoryMutation,
  useUpdateCategoryMutation,
  useDeleteCategoryMutation
} = categoriesApi;
