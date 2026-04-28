import { api } from "../../shared/api/baseApi";
import type { CategoryDto } from "../../shared/types/api";

type CreateCategoryRequest = {
    name: string;
    type: number;
};

type UpdateCategoryRequest = {
    id: string;
    name: string;
    type: number;
};

export const categoriesApi = api.injectEndpoints({
    endpoints: (builder) => ({
        // 📥 GET
        getCategories: builder.query<CategoryDto[], void>({
            query: () => ({ url: "/categories" }),
            providesTags: ["Category"],
        }),

        // ➕ CREATE
        createCategory: builder.mutation<void, CreateCategoryRequest>({
            query: (body) => ({
                url: "/categories",
                method: "POST",
                body,
            }),
            invalidatesTags: ["Category"],
        }),

        // ✏️ UPDATE
        updateCategory: builder.mutation<void, UpdateCategoryRequest>({
            query: ({ id, ...body }) => ({
                url: `/categories/${id}`,
                method: "PUT",
                body,
            }),
            invalidatesTags: ["Category"],
        }),

        // 🗑 DELETE
        deleteCategory: builder.mutation<void, string>({
            query: (id) => ({
                url: `/categories/${id}`,
                method: "DELETE",
            }),
            invalidatesTags: ["Category"],
        }),
    }),
});
export const {
    useGetCategoriesQuery,
    useCreateCategoryMutation,
    useUpdateCategoryMutation,
    useDeleteCategoryMutation,
} = categoriesApi;