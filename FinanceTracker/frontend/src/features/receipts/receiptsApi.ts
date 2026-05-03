import { api } from "../../shared/api/baseApi";

export interface ReceiptListItemDto {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  ocrStatus: number;
  recognizedTotalAmount?: number | null;
  recognizedDate?: string | null;
  recognizedMerchant?: string | null;
  processingError?: string | null;
  previewUrl: string;
}

export interface ReceiptItemDto {
  id: string;
  name: string;
  price: number;
  currencyCode: string;
  categoryName: string;
  mappedCategoryId?: string | null;
  mappedCategoryName?: string | null;
  sortOrder: number;
}

export interface ReceiptDetailsDto extends ReceiptListItemDto {
  items: ReceiptItemDto[];
}

export interface UpdateReceiptItemCategoryRequest {
  itemId: string;
  mappedCategoryId?: string | null;
}

export const receiptsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getReceipts: builder.query<ReceiptListItemDto[], void>({
      query: () => ({
        url: "/receipts"
      }),
      providesTags: ["Receipt"]
    }),
    getReceiptById: builder.query<ReceiptDetailsDto, string>({
      query: (id) => ({
        url: `/receipts/${id}`
      }),
      providesTags: ["Receipt"]
    }),
    uploadReceipt: builder.mutation<ReceiptDetailsDto, FormData>({
      query: (body) => ({
        url: "/receipts/upload",
        method: "POST",
        body
      }),
      invalidatesTags: ["Receipt"]
    }),
    updateReceiptItemCategory: builder.mutation<ReceiptItemDto, UpdateReceiptItemCategoryRequest>({
      query: ({ itemId, mappedCategoryId }) => ({
        url: `/receipts/items/${itemId}/category`,
        method: "PUT",
        body: { mappedCategoryId: mappedCategoryId ?? null }
      }),
      invalidatesTags: ["Receipt"]
    })
  })
});

export const {
  useGetReceiptsQuery,
  useLazyGetReceiptByIdQuery,
  useUploadReceiptMutation,
  useUpdateReceiptItemCategoryMutation
} = receiptsApi;
