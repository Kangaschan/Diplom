import { DeleteOutlined, EyeOutlined, PlusOutlined, ReloadOutlined, UploadOutlined } from "@ant-design/icons";
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Drawer,
  Empty,
  Image,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  Upload,
  message
} from "antd";
import type { UploadFile } from "antd/es/upload/interface";
import dayjs, { type Dayjs } from "dayjs";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";
import { useGetProfileQuery } from "../../features/profile/profileApi";
import {
  type ReceiptListItemDto,
  useApplyReceiptMutation,
  useCreateReceiptItemMutation,
  useDeleteReceiptItemMutation,
  useDeleteReceiptMutation,
  useGetReceiptByIdQuery,
  useGetReceiptsQuery,
  useRetryReceiptProcessingMutation,
  useUpdateReceiptItemMutation,
  useUploadReceiptMutation
} from "../../features/receipts/receiptsApi";
import { fetchAuthorizedBlobUrl } from "../../shared/lib/fetchAuthorizedBlobUrl";
import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";

interface ReceiptItemDraft {
  name: string;
  price: number;
  currencyCode: string;
  mappedCategoryId?: string | null;
  isNew?: boolean;
}

function formatFileSize(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }

  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

export function ReceiptsPage() {
  const { t } = useTranslation();
  const [messageApi, contextHolder] = message.useMessage();
  const [uploadOpen, setUploadOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [localPreviewUrl, setLocalPreviewUrl] = useState<string | null>(null);
  const [remotePreviewUrl, setRemotePreviewUrl] = useState<string | null>(null);
  const [selectedReceiptId, setSelectedReceiptId] = useState<string | null>(null);
  const [selectedAccountId, setSelectedAccountId] = useState<string | undefined>();
  const [receiptTransactionDate, setReceiptTransactionDate] = useState<Dayjs | null>(null);
  const [bulkCurrencyCode, setBulkCurrencyCode] = useState("USD");
  const [itemDrafts, setItemDrafts] = useState<Record<string, ReceiptItemDraft>>({});
  const [deletedItemIds, setDeletedItemIds] = useState<string[]>([]);
  const { data: profile } = useGetProfileQuery();
  const hasPremium = profile?.hasActivePremium ?? false;

  const { data: receipts = [], isLoading } = useGetReceiptsQuery(undefined, {
    pollingInterval: 5000
  });
  const { data: accounts = [] } = useGetAccountsQuery();
  const { data: categories = [] } = useGetCategoriesQuery();
  const [uploadReceipt, { isLoading: isUploading }] = useUploadReceiptMutation();
  const [retryReceiptProcessing, { isLoading: isRetrying }] = useRetryReceiptProcessingMutation();
  const [deleteReceipt, { isLoading: isDeletingReceipt }] = useDeleteReceiptMutation();
  const [createReceiptItem] = useCreateReceiptItemMutation();
  const [deleteReceiptItem] = useDeleteReceiptItemMutation();
  const [updateReceiptItem, { isLoading: isSavingItem }] = useUpdateReceiptItemMutation();
  const [applyReceipt, { isLoading: isApplyingReceipt }] = useApplyReceiptMutation();
  const { data: selectedReceipt, isFetching: isReceiptLoading, refetch: refetchReceipt } = useGetReceiptByIdQuery(selectedReceiptId ?? "", {
    skip: !selectedReceiptId,
    pollingInterval: detailOpen ? 3000 : 0,
    refetchOnMountOrArgChange: true
  });

  function getReceiptStatusTag(status: number) {
    if (status === 2) {
      return <Tag color="success">{t("receipts.statusCompleted")}</Tag>;
    }

    if (status === 3) {
      return <Tag color="error">{t("receipts.statusFailed")}</Tag>;
    }

    return <Tag color="processing">{t("receipts.statusProcessing")}</Tag>;
  }

  const hasPendingItemChanges = useMemo(() => {
    if (!selectedReceipt) {
      return false;
    }

    const hasChangedExistingItems = selectedReceipt.items.some((item) => {
      const draft = itemDrafts[item.id];
      if (!draft) {
        return false;
      }

      return (
        draft.name !== item.name ||
        draft.price !== item.price ||
        draft.currencyCode !== item.currencyCode ||
        (draft.mappedCategoryId ?? null) !== (item.mappedCategoryId ?? null)
      );
    });

    const hasNewItems = Object.values(itemDrafts).some((draft) => draft.isNew);
    return hasChangedExistingItems || hasNewItems || deletedItemIds.length > 0;
  }, [deletedItemIds.length, itemDrafts, selectedReceipt]);

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
    const previewUrl = selectedReceipt.previewUrl;

    async function loadPreview() {
      try {
        objectUrl = await fetchAuthorizedBlobUrl(previewUrl);
        if (disposed) {
          URL.revokeObjectURL(objectUrl);
          return;
        }

        setRemotePreviewUrl(objectUrl);
      } catch {
        messageApi.error(t("receipts.previewLoadFailed"));
      }
    }

    void loadPreview();

    return () => {
      disposed = true;
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [detailOpen, messageApi, selectedReceipt?.previewUrl, t]);

  useEffect(() => {
    if (!selectedReceipt) {
      setItemDrafts({});
      setDeletedItemIds([]);
      return;
    }

    setItemDrafts((current) => {
      const nextDrafts: Record<string, ReceiptItemDraft> = {};
      selectedReceipt.items.forEach((item) => {
        nextDrafts[item.id] = current[item.id] ?? {
          name: item.name,
          price: item.price,
          currencyCode: item.currencyCode,
          mappedCategoryId: item.mappedCategoryId ?? undefined
        };
      });
      return nextDrafts;
    });
  }, [selectedReceipt]);

  useEffect(() => {
    if (!selectedReceipt || selectedReceipt.items.length === 0) {
      return;
    }

    setBulkCurrencyCode(selectedReceipt.recognizedCurrencyCode ?? selectedReceipt.items[0].currencyCode);
  }, [selectedReceipt]);

  useEffect(() => {
    if (!detailOpen) {
      setSelectedAccountId(undefined);
      setReceiptTransactionDate(null);
      return;
    }

    if (!selectedAccountId && accounts.length > 0) {
      setSelectedAccountId(accounts[0].id);
    }
  }, [accounts, detailOpen, selectedAccountId]);

  useEffect(() => {
    if (!selectedReceipt || receiptTransactionDate) {
      return;
    }

    setReceiptTransactionDate(dayjs(selectedReceipt.uploadedAt));
  }, [receiptTransactionDate, selectedReceipt]);

  function closeUploadModal() {
    setUploadOpen(false);
    setSelectedFile(null);
  }

  async function handleUpload() {
    if (!selectedFile) {
      messageApi.warning(t("receipts.uploadWarning"));
      return;
    }

    const formData = new FormData();
    formData.append("file", selectedFile);

    try {
      const createdReceipt = await uploadReceipt(formData).unwrap();
      messageApi.success(t("receipts.uploadSuccess"));
      closeUploadModal();
      setSelectedReceiptId(createdReceipt.id);
      setDetailOpen(true);
    } catch {
      messageApi.error(t("receipts.uploadFailed"));
    }
  }

  function openDetails(receipt: ReceiptListItemDto) {
    setSelectedReceiptId(receipt.id);
    setDetailOpen(true);
    setItemDrafts({});
    setDeletedItemIds([]);
    setReceiptTransactionDate(null);
  }

  async function handleSaveAllItems() {
    if (!selectedReceipt) {
      return;
    }

    try {
      for (const deletedItemId of deletedItemIds) {
        await deleteReceiptItem(deletedItemId).unwrap();
      }

      const changedItems = selectedReceipt.items
        .filter((item) => !deletedItemIds.includes(item.id))
        .filter((item) => {
          const draft = itemDrafts[item.id];
          if (!draft) {
            return false;
          }

          return (
            draft.name !== item.name ||
            draft.price !== item.price ||
            draft.currencyCode !== item.currencyCode ||
            (draft.mappedCategoryId ?? null) !== (item.mappedCategoryId ?? null)
          );
        });

      const newItems = Object.entries(itemDrafts).filter(([, draft]) => draft.isNew);

      if (deletedItemIds.length === 0 && changedItems.length === 0 && newItems.length === 0) {
        messageApi.info(t("receipts.noChanges"));
        return;
      }

      for (const item of changedItems) {
        const draft = itemDrafts[item.id];
        if (!draft) {
          continue;
        }

        await updateReceiptItem({
          itemId: item.id,
          name: draft.name,
          price: draft.price,
          currencyCode: draft.currencyCode,
          mappedCategoryId: draft.mappedCategoryId ?? null
        }).unwrap();
      }

      for (const [, draft] of newItems) {
        await createReceiptItem({
          receiptId: selectedReceipt.id,
          name: draft.name,
          price: draft.price,
          currencyCode: draft.currencyCode,
          mappedCategoryId: draft.mappedCategoryId ?? null
        }).unwrap();
      }

      await refetchReceipt();
      setDeletedItemIds([]);
      messageApi.success(t("receipts.saveSuccess"));
    } catch {
      messageApi.error(t("receipts.saveFailed"));
    }
  }

  async function handleRetry(receiptId: string) {
    try {
      await retryReceiptProcessing(receiptId).unwrap();
      messageApi.success(t("receipts.retrySuccess"));
      if (selectedReceiptId === receiptId) {
        await refetchReceipt();
      }
    } catch {
      messageApi.error(t("receipts.retryFailed"));
    }
  }

  function handleAddItem() {
    const tempId = `new-${crypto.randomUUID()}`;

    setItemDrafts((current) => ({
      ...current,
      [tempId]: {
        name: "",
        price: 0.01,
        currencyCode: bulkCurrencyCode.trim().toUpperCase(),
        mappedCategoryId: undefined,
        isNew: true
      }
    }));
  }

  function handleDeleteItem(itemId: string) {
    setItemDrafts((current) => {
      const nextDrafts = { ...current };
      const draft = nextDrafts[itemId];

      if (draft?.isNew) {
        delete nextDrafts[itemId];
        return nextDrafts;
      }

      delete nextDrafts[itemId];
      return nextDrafts;
    });

    if (!itemId.startsWith("new-")) {
      setDeletedItemIds((current) => (current.includes(itemId) ? current : [...current, itemId]));
    }
  }

  async function handleDeleteReceipt(receiptId: string) {
    try {
      await deleteReceipt(receiptId).unwrap();
      if (selectedReceiptId === receiptId) {
        setDetailOpen(false);
        setSelectedReceiptId(null);
      }

      messageApi.success(t("receipts.deleteSuccess"));
    } catch {
      messageApi.error(t("receipts.deleteFailed"));
    }
  }

  async function handleApplyReceipt() {
    if (!selectedReceipt) {
      return;
    }

    if (!selectedAccountId) {
      messageApi.warning(t("receipts.selectAccountWarning"));
      return;
    }

    try {
      const result = await applyReceipt({
        receiptId: selectedReceipt.id,
        accountId: selectedAccountId,
        transactionDate: receiptTransactionDate?.toISOString() ?? null
      }).unwrap();

      await refetchReceipt();
      messageApi.success(t("receipts.transactionsCreated", { count: result.createdTransactionsCount }));
    } catch {
      messageApi.error(t("receipts.applyFailed"));
    }
  }

  function handleApplyCurrencyToAllItems() {
    if (!selectedReceipt) {
      return;
    }

    setItemDrafts((current) => {
      const nextDrafts = { ...current };

      selectedReceipt.items.forEach((item) => {
        if (deletedItemIds.includes(item.id)) {
          return;
        }

        nextDrafts[item.id] = {
          name: current[item.id]?.name ?? item.name,
          price: current[item.id]?.price ?? item.price,
          currencyCode: bulkCurrencyCode.trim().toUpperCase(),
          mappedCategoryId: current[item.id]?.mappedCategoryId ?? item.mappedCategoryId ?? undefined
        };
      });

      Object.entries(current).forEach(([itemId, draft]) => {
        if (!draft.isNew) {
          return;
        }

        nextDrafts[itemId] = {
          ...draft,
          currencyCode: bulkCurrencyCode.trim().toUpperCase()
        };
      });

      return nextDrafts;
    });
  }

  const receiptTableItems = useMemo(() => {
    if (!selectedReceipt) {
      return [];
    }

    const existingItems = selectedReceipt.items
      .filter((item) => !deletedItemIds.includes(item.id))
      .map((item) => ({
        id: item.id,
        name: item.name,
        price: item.price,
        currencyCode: item.currencyCode,
        categoryName: item.categoryName,
        mappedCategoryId: item.mappedCategoryId,
        isNew: false
      }));

    const newItems = Object.entries(itemDrafts)
      .filter(([, draft]) => draft.isNew)
      .map(([itemId, draft]) => ({
        id: itemId,
        name: draft.name,
        price: draft.price,
        currencyCode: draft.currencyCode,
        categoryName: t("receipts.itemManual"),
        mappedCategoryId: draft.mappedCategoryId ?? null,
        isNew: true
      }));

    return [...existingItems, ...newItems];
  }, [deletedItemIds, itemDrafts, selectedReceipt, t]);

  return (
    <div className="page-content">
      {contextHolder}

      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          {t("receipts.title")}
        </Typography.Title>
        <Button type="primary" icon={<UploadOutlined />} onClick={() => setUploadOpen(true)} disabled={!hasPremium}>
          {t("receipts.upload")}
        </Button>
      </div>

      {!hasPremium && (
        <Alert
          type="info"
          showIcon
          message={t("receipts.premiumTitle")}
          description={t("receipts.premiumDescription")}
        />
      )}

      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        {t("receipts.subtitle")}
      </Typography.Paragraph>

      <Card>
        {receipts.length === 0 && !isLoading ? (
          <Empty description={t("receipts.noReceipts")}>
            <Button type="primary" onClick={() => setUploadOpen(true)} disabled={!hasPremium}>
              {t("receipts.uploadFirst")}
            </Button>
          </Empty>
        ) : (
          <Table
            loading={isLoading}
            rowKey="id"
            dataSource={receipts}
            columns={[
              {
                title: t("receipts.file"),
                dataIndex: "originalFileName"
              },
              {
                title: t("receipts.uploaded"),
                dataIndex: "uploadedAt",
                render: (value: string) => formatDate(value)
              },
              {
                title: t("receipts.status"),
                render: (_, row) => getReceiptStatusTag(row.ocrStatus)
              },
              {
                title: t("receipts.merchant"),
                dataIndex: "recognizedMerchant",
                render: (value?: string | null) => value || "-"
              },
              {
                title: t("receipts.total"),
                render: (_, row) =>
                  row.recognizedTotalAmount == null ? "-" : formatMoney(row.recognizedTotalAmount, row.recognizedCurrencyCode ?? undefined)
              },
              {
                title: t("receipts.size"),
                render: (_, row) => formatFileSize(row.fileSizeBytes)
              },
              {
                title: t("common.actions"),
                render: (_, row) => (
                  <Space>
                    <Button icon={<EyeOutlined />} onClick={() => openDetails(row)} disabled={!hasPremium}>
                      {t("receipts.open")}
                    </Button>
                    {row.ocrStatus === 3 && (
                      <Button icon={<ReloadOutlined />} onClick={() => void handleRetry(row.id)} loading={isRetrying} disabled={!hasPremium}>
                        {t("receipts.retry")}
                      </Button>
                    )}
                    <Popconfirm
                      title={t("receipts.deleteTitle")}
                      description={t("receipts.deleteDescription")}
                      okText={t("common.delete")}
                      cancelText={t("common.cancel")}
                      onConfirm={() => void handleDeleteReceipt(row.id)}
                    >
                      <Button danger icon={<DeleteOutlined />} loading={isDeletingReceipt} disabled={!hasPremium}>
                        {t("receipts.delete")}
                      </Button>
                    </Popconfirm>
                  </Space>
                )
              }
            ]}
          />
        )}
      </Card>

      <Modal
        title={t("receipts.uploadTitle")}
        open={uploadOpen}
        onCancel={closeUploadModal}
        onOk={() => void handleUpload()}
        okText={t("receipts.uploadConfirm")}
        confirmLoading={isUploading}
        okButtonProps={{ disabled: !hasPremium }}
        cancelText={t("common.cancel")}
      >
        <Space direction="vertical" size={16} style={{ width: "100%" }}>
          <Upload
            accept=".jpg,.jpeg,.png"
            maxCount={1}
            disabled={!hasPremium}
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
            <Button icon={<UploadOutlined />}>{t("receipts.chooseImage")}</Button>
          </Upload>

          <Typography.Text type="secondary">{t("receipts.uploadFormats")}</Typography.Text>

          {localPreviewUrl && (
            <Image src={localPreviewUrl} alt={t("receipts.previewAlt")} style={{ width: "100%", maxHeight: 360, objectFit: "contain" }} />
          )}
        </Space>
      </Modal>

      <Drawer
        title={t("receipts.detailTitle")}
        open={detailOpen}
        width={1080}
        onClose={() => {
          setDetailOpen(false);
          setSelectedReceiptId(null);
        }}
      >
        {!selectedReceipt ? (
          <Card loading />
        ) : (
          <Space direction="vertical" size={16} style={{ width: "100%" }}>
            {isReceiptLoading && selectedReceipt.ocrStatus === 1 && (
              <Alert type="info" showIcon message={t("receipts.processingHint")} />
            )}
            {selectedReceipt.processingError && (
              <Alert type="error" showIcon message={t("receipts.processingFailedTitle")} description={selectedReceipt.processingError} />
            )}

            <Card size="small" title={t("receipts.actionsTitle")}>
              <Space direction="vertical" style={{ width: "100%" }} size={12}>
                {selectedReceipt.ocrStatus === 3 && (
                  <Button
                    type="primary"
                    icon={<ReloadOutlined />}
                    onClick={() => void handleRetry(selectedReceipt.id)}
                    loading={isRetrying}
                    disabled={!hasPremium}
                  >
                    {t("receipts.retryRecognition")}
                  </Button>
                )}

                <Space wrap>
                  <Select
                    style={{ minWidth: 260 }}
                    placeholder={t("receipts.selectAccount")}
                    value={selectedAccountId}
                    onChange={setSelectedAccountId}
                    disabled={!hasPremium || selectedReceipt.ocrStatus !== 2 || selectedReceipt.hasCreatedTransactions}
                    options={accounts.map((account) => ({
                      value: account.id,
                      label: `${account.name} (${account.currencyCode})`
                    }))}
                  />
                  <DatePicker
                    value={receiptTransactionDate}
                    format="DD.MM.YYYY"
                    onChange={(value) => setReceiptTransactionDate(value)}
                    disabled={!hasPremium || selectedReceipt.ocrStatus !== 2 || selectedReceipt.hasCreatedTransactions}
                  />
                  <Button
                    type="primary"
                    onClick={() => void handleApplyReceipt()}
                    disabled={!hasPremium || selectedReceipt.ocrStatus !== 2 || selectedReceipt.hasCreatedTransactions}
                    loading={isApplyingReceipt}
                  >
                    {t("receipts.createTransactions")}
                  </Button>
                  <Popconfirm
                    title={t("receipts.deleteTitle")}
                    description={t("receipts.deleteDescription")}
                    okText={t("common.delete")}
                    cancelText={t("common.cancel")}
                    onConfirm={() => void handleDeleteReceipt(selectedReceipt.id)}
                  >
                    <Button danger icon={<DeleteOutlined />} loading={isDeletingReceipt} disabled={!hasPremium}>
                      {t("receipts.delete")}
                    </Button>
                  </Popconfirm>
                </Space>

                {selectedReceipt.hasCreatedTransactions && (
                  <Alert type="success" showIcon message={t("receipts.alreadyApplied")} />
                )}
              </Space>
            </Card>

            {remotePreviewUrl ? (
              <Image src={remotePreviewUrl} alt={selectedReceipt.originalFileName} style={{ width: "100%", maxHeight: 420, objectFit: "contain" }} />
            ) : (
              <Card size="small">
                <Typography.Text type="secondary">{t("receipts.previewUnavailable")}</Typography.Text>
              </Card>
            )}

            <Card size="small" title={t("receipts.metadataTitle")}>
              <Space direction="vertical" style={{ width: "100%" }}>
                <Typography.Text>{t("receipts.file")}: {selectedReceipt.originalFileName}</Typography.Text>
                <Typography.Text>{t("receipts.status")}: {getReceiptStatusTag(selectedReceipt.ocrStatus)}</Typography.Text>
                <Typography.Text>{t("receipts.uploaded")}: {formatDate(selectedReceipt.uploadedAt)}</Typography.Text>
                <Typography.Text>{t("receipts.size")}: {formatFileSize(selectedReceipt.fileSizeBytes)}</Typography.Text>
                <Typography.Text>{t("receipts.merchant")}: {selectedReceipt.recognizedMerchant || "-"}</Typography.Text>
                <Typography.Text>
                  {t("receipts.total")}:{" "}
                  {selectedReceipt.recognizedTotalAmount == null
                    ? "-"
                    : formatMoney(selectedReceipt.recognizedTotalAmount, selectedReceipt.recognizedCurrencyCode ?? undefined)}
                </Typography.Text>
                <Typography.Text>
                  {t("receipts.recognizedDate")}: {selectedReceipt.recognizedDate ? formatDate(selectedReceipt.recognizedDate) : "-"}
                </Typography.Text>
              </Space>
            </Card>

            <Card size="small" title={t("receipts.receiptItemsTitle")}>
              <Space direction="vertical" size={16} style={{ width: "100%" }}>
                <Space wrap>
                  <Button icon={<PlusOutlined />} onClick={handleAddItem} disabled={!hasPremium || selectedReceipt.hasCreatedTransactions}>
                    {t("receipts.addItem")}
                  </Button>
                  <Select
                    style={{ minWidth: 180 }}
                    value={bulkCurrencyCode}
                    onChange={setBulkCurrencyCode}
                    disabled={!hasPremium || selectedReceipt.hasCreatedTransactions}
                    options={[
                      { value: "USD", label: "USD" },
                      { value: "EUR", label: "EUR" },
                      { value: "RUB", label: "RUB" },
                      { value: "BYN", label: "BYN" },
                      { value: "JPY", label: "JPY" },
                      { value: "CNY", label: "CNY" },
                      { value: "GBP", label: "GBP" }
                    ]}
                  />
                  <Button
                    onClick={handleApplyCurrencyToAllItems}
                    disabled={!hasPremium || selectedReceipt.hasCreatedTransactions || receiptTableItems.length === 0}
                  >
                    {t("receipts.applyCurrencyToAll")}
                  </Button>
                </Space>

                <Table
                  rowKey="id"
                  pagination={false}
                  scroll={{ x: 980 }}
                  loading={isSavingItem}
                  dataSource={receiptTableItems}
                  locale={{
                    emptyText: t("receipts.emptyItems")
                  }}
                  columns={[
                    {
                      title: t("receipts.itemName"),
                      width: 360,
                      render: (_, row) => (
                        <Input
                          style={{ minWidth: 320 }}
                          value={itemDrafts[row.id]?.name ?? row.name}
                          onChange={(event) =>
                            setItemDrafts((current) => ({
                              ...current,
                              [row.id]: {
                                ...current[row.id],
                                name: event.target.value
                              }
                            }))
                          }
                          disabled={!hasPremium || selectedReceipt.hasCreatedTransactions}
                        />
                      )
                    },
                    {
                      title: t("receipts.itemPrice"),
                      width: 220,
                      render: (_, row) => (
                        <Space>
                          <InputNumber
                            style={{ width: 120 }}
                            min={0.01}
                            step={0.01}
                            value={itemDrafts[row.id]?.price ?? row.price}
                            onChange={(value) =>
                              setItemDrafts((current) => ({
                                ...current,
                                [row.id]: {
                                  ...current[row.id],
                                  price: typeof value === "number" ? value : row.price
                                }
                              }))
                            }
                            disabled={!hasPremium || selectedReceipt.hasCreatedTransactions}
                          />
                          <Input
                            style={{ width: 90 }}
                            value={itemDrafts[row.id]?.currencyCode ?? row.currencyCode}
                            onChange={(event) =>
                              setItemDrafts((current) => ({
                                ...current,
                                [row.id]: {
                                  ...current[row.id],
                                  currencyCode: event.target.value
                                }
                              }))
                            }
                            disabled={!hasPremium || selectedReceipt.hasCreatedTransactions}
                          />
                        </Space>
                      )
                    },
                    {
                      title: t("receipts.itemDetectedCategory"),
                      dataIndex: "categoryName",
                      width: 180
                    },
                    {
                      title: t("receipts.itemCategory"),
                      width: 240,
                      render: (_, row) => (
                        <Select
                          style={{ minWidth: 200 }}
                          value={itemDrafts[row.id]?.mappedCategoryId ?? row.mappedCategoryId ?? undefined}
                          placeholder={t("receipts.itemCategoryPlaceholder")}
                          allowClear
                          onChange={(value) =>
                            setItemDrafts((current) => ({
                              ...current,
                              [row.id]: {
                                ...current[row.id],
                                mappedCategoryId: value
                              }
                            }))
                          }
                          disabled={!hasPremium || selectedReceipt.hasCreatedTransactions}
                          options={categories
                            .filter((category) => category.type === 2)
                            .map((category) => ({
                              value: category.id,
                              label: category.name
                            }))}
                        />
                      )
                    },
                    {
                      title: t("receipts.itemActions"),
                      width: 100,
                      render: (_, row) => (
                        <Button
                          danger
                          type="text"
                          icon={<DeleteOutlined />}
                          onClick={() => handleDeleteItem(row.id)}
                          disabled={!hasPremium || selectedReceipt.hasCreatedTransactions}
                          aria-label={t("receipts.itemDeleteAria")}
                        />
                      )
                    }
                  ]}
                />

                <div style={{ display: "flex", justifyContent: "flex-end" }}>
                  <Button
                    type="primary"
                    onClick={() => void handleSaveAllItems()}
                    loading={isSavingItem}
                    disabled={!hasPremium || selectedReceipt.hasCreatedTransactions || !hasPendingItemChanges}
                  >
                    {t("receipts.saveAllChanges")}
                  </Button>
                </div>
              </Space>
            </Card>
          </Space>
        )}
      </Drawer>
    </div>
  );
}
