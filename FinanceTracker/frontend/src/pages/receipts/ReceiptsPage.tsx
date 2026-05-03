import { EyeOutlined, UploadOutlined } from "@ant-design/icons";
import {
  Alert,
  Button,
  Card,
  Drawer,
  Empty,
  Image,
  Modal,
  Space,
  Table,
  Tag,
  Typography,
  Upload,
  message
} from "antd";
import type { UploadFile } from "antd/es/upload/interface";
import { useEffect, useMemo, useState } from "react";

import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";
import {
  type ReceiptDetailsDto,
  type ReceiptListItemDto,
  useGetReceiptsQuery,
  useLazyGetReceiptByIdQuery,
  useUpdateReceiptItemCategoryMutation,
  useUploadReceiptMutation
} from "../../features/receipts/receiptsApi";
import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";
import { fetchAuthorizedBlobUrl } from "../../shared/lib/fetchAuthorizedBlobUrl";

function formatFileSize(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }

  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

function getReceiptStatusTag(status: number) {
  if (status === 2) {
    return <Tag color="success">Completed</Tag>;
  }

  if (status === 3) {
    return <Tag color="error">Failed</Tag>;
  }

  return <Tag color="processing">Pending</Tag>;
}

export function ReceiptsPage() {
  const [messageApi, contextHolder] = message.useMessage();
  const [uploadOpen, setUploadOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [localPreviewUrl, setLocalPreviewUrl] = useState<string | null>(null);
  const [remotePreviewUrl, setRemotePreviewUrl] = useState<string | null>(null);
  const [selectedReceiptId, setSelectedReceiptId] = useState<string | null>(null);

  const { data: receipts = [], isLoading } = useGetReceiptsQuery();
  const { data: categories = [] } = useGetCategoriesQuery();
  const [uploadReceipt, { isLoading: isUploading }] = useUploadReceiptMutation();
  const [updateReceiptItemCategory, { isLoading: isUpdatingCategory }] = useUpdateReceiptItemCategoryMutation();
  const [loadReceipt, { data: receiptDetails, isFetching: isReceiptLoading }] = useLazyGetReceiptByIdQuery();

  const selectedReceipt = useMemo<ReceiptDetailsDto | null>(() => {
    if (!selectedReceiptId) {
      return null;
    }

    if (receiptDetails?.id === selectedReceiptId) {
      return receiptDetails;
    }

    return null;
  }, [receiptDetails, selectedReceiptId]);

  useEffect(() => {
    if (!selectedFile) {
      setLocalPreviewUrl((current) => {
        if (current) {
          URL.revokeObjectURL(current);
        }

        return null;
      });
      return;
    }

    const objectUrl = URL.createObjectURL(selectedFile);
    setLocalPreviewUrl(objectUrl);

    return () => {
      URL.revokeObjectURL(objectUrl);
    };
  }, [selectedFile]);

  useEffect(() => {
    if (!selectedReceipt?.previewUrl || !detailOpen) {
      setRemotePreviewUrl((current) => {
        if (current) {
          URL.revokeObjectURL(current);
        }

        return null;
      });
      return;
    }

    let disposed = false;
    let objectUrl = "";

    async function loadPreview() {
      try {
        objectUrl = await fetchAuthorizedBlobUrl(selectedReceipt.previewUrl);
        if (disposed) {
          URL.revokeObjectURL(objectUrl);
          return;
        }

        setRemotePreviewUrl(objectUrl);
      } catch {
        messageApi.error("Failed to load receipt preview.");
      }
    }

    void loadPreview();

    return () => {
      disposed = true;
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [detailOpen, messageApi, selectedReceipt?.previewUrl]);

  function closeUploadModal() {
    setUploadOpen(false);
    setSelectedFile(null);
  }

  async function handleUpload() {
    if (!selectedFile) {
      messageApi.warning("Select a receipt image first.");
      return;
    }

    const formData = new FormData();
    formData.append("file", selectedFile);

    try {
      const createdReceipt = await uploadReceipt(formData).unwrap();
      messageApi.success("Receipt uploaded.");
      closeUploadModal();
      setSelectedReceiptId(createdReceipt.id);
      setDetailOpen(true);
      void loadReceipt(createdReceipt.id);
    } catch {
      messageApi.error("Failed to upload receipt.");
    }
  }

  async function openDetails(receipt: ReceiptListItemDto) {
    setSelectedReceiptId(receipt.id);
    setDetailOpen(true);
    await loadReceipt(receipt.id);
  }

  async function handleUpdateItemCategory(itemId: string, mappedCategoryId?: string | null) {
    try {
      await updateReceiptItemCategory({
        itemId,
        mappedCategoryId: mappedCategoryId ?? null
      }).unwrap();

      if (selectedReceiptId) {
        await loadReceipt(selectedReceiptId);
      }

      messageApi.success("Receipt item category updated.");
    } catch {
      messageApi.error("Failed to update receipt item category.");
    }
  }

  return (
    <div className="page-content">
      {contextHolder}

      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          Receipts
        </Typography.Title>
        <Button type="primary" icon={<UploadOutlined />} onClick={() => setUploadOpen(true)}>
          Upload receipt
        </Button>
      </div>

      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        Upload receipt images, keep them in storage, and prepare them for OCR parsing.
      </Typography.Paragraph>

      <Card>
        {receipts.length === 0 && !isLoading ? (
          <Empty description="No receipts uploaded yet">
            <Button type="primary" onClick={() => setUploadOpen(true)}>
              Upload first receipt
            </Button>
          </Empty>
        ) : (
          <Table
            loading={isLoading}
            rowKey="id"
            dataSource={receipts}
            columns={[
              {
                title: "File",
                dataIndex: "originalFileName"
              },
              {
                title: "Uploaded",
                dataIndex: "uploadedAt",
                render: (value: string) => formatDate(value)
              },
              {
                title: "Status",
                render: (_, row) => getReceiptStatusTag(row.ocrStatus)
              },
              {
                title: "Merchant",
                dataIndex: "recognizedMerchant",
                render: (value?: string | null) => value || "-"
              },
              {
                title: "Total",
                render: (_, row) =>
                  row.recognizedTotalAmount == null ? "-" : formatMoney(row.recognizedTotalAmount)
              },
              {
                title: "Size",
                render: (_, row) => formatFileSize(row.fileSizeBytes)
              },
              {
                title: "Actions",
                render: (_, row) => (
                  <Button icon={<EyeOutlined />} onClick={() => void openDetails(row)}>
                    View
                  </Button>
                )
              }
            ]}
          />
        )}
      </Card>

      <Modal
        title="Upload receipt"
        open={uploadOpen}
        onCancel={closeUploadModal}
        onOk={() => void handleUpload()}
        okText="Upload"
        confirmLoading={isUploading}
      >
        <Space direction="vertical" size={16} style={{ width: "100%" }}>
          <Upload
            accept=".jpg,.jpeg,.png"
            maxCount={1}
            beforeUpload={(file) => {
              setSelectedFile(file);
              return false;
            }}
            onRemove={() => {
              setSelectedFile(null);
            }}
            fileList={
              selectedFile
                ? [
                    {
                      uid: selectedFile.name,
                      name: selectedFile.name,
                      status: "done"
                    } satisfies UploadFile
                  ]
                : []
            }
          >
            <Button icon={<UploadOutlined />}>Choose image</Button>
          </Upload>

          <Typography.Text type="secondary">
            Allowed formats: JPG, JPEG, PNG. Maximum size: 10 MB.
          </Typography.Text>

          {localPreviewUrl && (
            <Image
              src={localPreviewUrl}
              alt="Receipt preview"
              style={{ width: "100%", maxHeight: 360, objectFit: "contain" }}
            />
          )}
        </Space>
      </Modal>

      <Drawer
        title="Receipt details"
        open={detailOpen}
        width={760}
        onClose={() => {
          setDetailOpen(false);
          setSelectedReceiptId(null);
        }}
      >
        {!selectedReceipt || isReceiptLoading ? (
          <Card loading />
        ) : (
          <Space direction="vertical" size={16} style={{ width: "100%" }}>
            {selectedReceipt.processingError && (
              <Alert type="error" showIcon message="Processing error" description={selectedReceipt.processingError} />
            )}

            {remotePreviewUrl ? (
              <Image
                src={remotePreviewUrl}
                alt={selectedReceipt.originalFileName}
                style={{ width: "100%", maxHeight: 420, objectFit: "contain" }}
              />
            ) : (
              <Card size="small">
                <Typography.Text type="secondary">Preview is loading or unavailable.</Typography.Text>
              </Card>
            )}

            <Card size="small" title="Metadata">
              <Space direction="vertical" style={{ width: "100%" }}>
                <Typography.Text>File: {selectedReceipt.originalFileName}</Typography.Text>
                <Typography.Text>Status: {getReceiptStatusTag(selectedReceipt.ocrStatus)}</Typography.Text>
                <Typography.Text>Uploaded: {formatDate(selectedReceipt.uploadedAt)}</Typography.Text>
                <Typography.Text>Size: {formatFileSize(selectedReceipt.fileSizeBytes)}</Typography.Text>
                <Typography.Text>Merchant: {selectedReceipt.recognizedMerchant || "-"}</Typography.Text>
                <Typography.Text>
                  Total: {selectedReceipt.recognizedTotalAmount == null ? "-" : formatMoney(selectedReceipt.recognizedTotalAmount)}
                </Typography.Text>
              </Space>
            </Card>

            <Card size="small" title="Receipt items">
              {selectedReceipt.items.length === 0 ? (
                <Empty
                  image={Empty.PRESENTED_IMAGE_SIMPLE}
                  description="Receipt items will appear here after OCR integration is connected."
                />
              ) : (
                <Table
                  rowKey="id"
                  pagination={false}
                  loading={isUpdatingCategory}
                  dataSource={selectedReceipt.items}
                  columns={[
                    {
                      title: "Product",
                      dataIndex: "name"
                    },
                    {
                      title: "Price",
                      render: (_, row) => formatMoney(row.price, row.currencyCode)
                    },
                    {
                      title: "Detected category",
                      dataIndex: "categoryName"
                    },
                    {
                      title: "Mapped category",
                      render: (_, row) => (
                        <Select
                          style={{ minWidth: 200 }}
                          value={row.mappedCategoryId ?? undefined}
                          placeholder="Others"
                          allowClear
                          onChange={(value) => void handleUpdateItemCategory(row.id, value)}
                          options={categories
                            .filter((category) => category.type === 2)
                            .map((category) => ({
                              value: category.id,
                              label: category.name
                            }))}
                        />
                      )
                    }
                  ]}
                />
              )}
            </Card>
          </Space>
        )}
      </Drawer>
    </div>
  );
}
