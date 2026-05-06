import { api } from "../../shared/api/baseApi";

export interface ReceiptListItemDto {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  ocrStatus: number;
  recognizedTotalAmount?: number | null;
  recognizedCurrencyCode?: string | null;
  recognizedDate?: string | null;
  recognizedMerchant?: string | null;
  processingError?: string | null;
  previewUrl: string;
  hasCreatedTransactions?: boolean;
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

export interface UpdateReceiptItemRequest {
  itemId: string;
  name: string;
  price: number;
  currencyCode: string;
  mappedCategoryId?: string | null;
}

export interface CreateReceiptItemRequest {
  receiptId: string;
  name: string;
  price: number;
  currencyCode: string;
  mappedCategoryId?: string | null;
}

export interface ApplyReceiptRequest {
  receiptId: string;
  accountId: string;
  transactionDate?: string | null;
}

export interface ReceiptApplyResult {
  receiptId: string;
  accountId: string;
  createdTransactionsCount: number;
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
    retryReceiptProcessing: builder.mutation<ReceiptDetailsDto, string>({
      query: (receiptId) => ({
        url: `/receipts/${receiptId}/retry`,
        method: "POST"
      }),
      invalidatesTags: ["Receipt"]
    }),
    deleteReceipt: builder.mutation<void, string>({
      query: (receiptId) => ({
        url: `/receipts/${receiptId}`,
        method: "DELETE"
      }),
      invalidatesTags: ["Receipt"]
    }),
    updateReceiptItem: builder.mutation<ReceiptItemDto, UpdateReceiptItemRequest>({
      query: ({ itemId, ...body }) => ({
        url: `/receipts/items/${itemId}`,
        method: "PUT",
        body
      }),
      invalidatesTags: ["Receipt"]
    }),
    createReceiptItem: builder.mutation<ReceiptItemDto, CreateReceiptItemRequest>({
      query: ({ receiptId, ...body }) => ({
        url: `/receipts/${receiptId}/items`,
        method: "POST",
        body
      }),
      invalidatesTags: ["Receipt"]
    }),
    deleteReceiptItem: builder.mutation<void, string>({
      query: (itemId) => ({
        url: `/receipts/items/${itemId}`,
        method: "DELETE"
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
    }),
    applyReceipt: builder.mutation<ReceiptApplyResult, ApplyReceiptRequest>({
      query: ({ receiptId, accountId, transactionDate }) => ({
        url: `/receipts/${receiptId}/apply`,
        method: "POST",
        body: { accountId, transactionDate: transactionDate ?? null }
      }),
      invalidatesTags: ["Receipt", "Transaction", "Account", "Analytics", "Budget", "Notification"]
    })
  })
});

export const {
  useGetReceiptsQuery,
  useGetReceiptByIdQuery,
  useUploadReceiptMutation,
  useRetryReceiptProcessingMutation,
  useDeleteReceiptMutation,
  useCreateReceiptItemMutation,
  useDeleteReceiptItemMutation,
  useUpdateReceiptItemMutation,
  useUpdateReceiptItemCategoryMutation,
  useApplyReceiptMutation
} = receiptsApi;
