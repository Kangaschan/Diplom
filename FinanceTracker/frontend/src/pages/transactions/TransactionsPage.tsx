import { DeleteOutlined, PlusOutlined } from "@ant-design/icons";
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Table,
  Typography,
  message
} from "antd";
import dayjs from "dayjs";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";
import {
  useCreateTransactionMutation,
  useDeleteTransactionMutation,
  useGetTransactionsQuery
} from "../../features/transactions/transactionsApi";
import { getExchangeRate } from "../../shared/lib/exchangeRate";
import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";

interface TransactionFormValues {
  accountId: string;
  categoryId?: string | null;
  type: number;
  amount: number;
  currencyCode: string;
  manualRate?: number;
  transactionDate: dayjs.Dayjs;
  description?: string;
}

const currencyOptions = [
  { value: "USD", label: "USD" },
  { value: "EUR", label: "EUR" },
  { value: "RUB", label: "RUB" },
  { value: "BYN", label: "BYN" },
  { value: "JPY", label: "JPY" },
  { value: "CNY", label: "CNY" },
  { value: "GBP", label: "GBP" }
];

export function TransactionsPage() {
  const { t } = useTranslation();
  const [search, setSearch] = useState("");
  const [type, setType] = useState<number | undefined>(undefined);
  const [accountId, setAccountId] = useState<string | undefined>(undefined);
  const [categoryId, setCategoryId] = useState<string | undefined>(undefined);
  const [selectedRowKeys, setSelectedRowKeys] = useState<string[]>([]);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm<TransactionFormValues>();

  const { data: accounts = [] } = useGetAccountsQuery({ includeArchived: false });
  const { data: categories = [] } = useGetCategoriesQuery();
  const { data: transactions = [], isLoading } = useGetTransactionsQuery({
    search: search || undefined,
    type,
    accountId,
    categoryId
  });
  const [createTransaction, { isLoading: isCreating }] = useCreateTransactionMutation();
  const [deleteTransaction, { isLoading: isDeleting }] = useDeleteTransactionMutation();

  const accountMap = useMemo(() => new Map(accounts.map((account) => [account.id, account.name])), [accounts]);
  const categoryMap = useMemo(() => new Map(categories.map((category) => [category.id, category.name])), [categories]);
  const values = Form.useWatch([], form) as TransactionFormValues | undefined;
  const selectedAccount = useMemo(() => accounts.find((account) => account.id === values?.accountId), [accounts, values?.accountId]);

  const [autoRate, setAutoRate] = useState<number | null>(1);
  const [rateLoading, setRateLoading] = useState(false);

  useEffect(() => {
    let active = true;

    async function loadRate() {
      if (!selectedAccount || !values?.currencyCode) {
        setAutoRate(1);
        return;
      }

      const sourceCurrency = values.currencyCode.trim().toUpperCase();
      const targetCurrency = selectedAccount.currencyCode.trim().toUpperCase();

      if (sourceCurrency === targetCurrency) {
        setAutoRate(1);
        return;
      }

      setRateLoading(true);
      try {
        const nextRate = await getExchangeRate(sourceCurrency, targetCurrency);
        if (!active) {
          return;
        }

        setAutoRate(nextRate);
      } finally {
        if (active) {
          setRateLoading(false);
        }
      }
    }

    void loadRate();

    return () => {
      active = false;
    };
  }, [selectedAccount, values?.currencyCode]);

  const appliedRate = useMemo(() => {
    if (!selectedAccount || !values?.currencyCode) {
      return null;
    }

    const sourceCurrency = values.currencyCode.trim().toUpperCase();
    const targetCurrency = selectedAccount.currencyCode.trim().toUpperCase();

    if (sourceCurrency === targetCurrency) {
      return 1;
    }

    if (values?.manualRate && values.manualRate > 0) {
      return values.manualRate;
    }

    return autoRate;
  }, [selectedAccount, values?.currencyCode, values?.manualRate, autoRate]);

  const normalizedAmount = useMemo(() => {
    const amount = values?.amount ?? 0;
    if (!appliedRate) {
      return null;
    }

    return Number((amount * appliedRate).toFixed(2));
  }, [values?.amount, appliedRate]);

  function openModal() {
    form.resetFields();
    form.setFieldsValue({
      transactionDate: dayjs(),
      currencyCode: "USD"
    });
    setIsModalOpen(true);
  }

  async function handleCreate() {
    try {
      const formValues = await form.validateFields();

      await createTransaction({
        accountId: formValues.accountId,
        categoryId: formValues.categoryId || null,
        type: formValues.type,
        amount: formValues.amount,
        currencyCode: formValues.currencyCode.trim().toUpperCase(),
        manualRate: formValues.manualRate && formValues.manualRate > 0 ? formValues.manualRate : null,
        transactionDate: formValues.transactionDate.toISOString(),
        description: formValues.description,
        source: 1
      }).unwrap();

      messageApi.success(t("transactions.created"));
      setIsModalOpen(false);
      form.resetFields();
    } catch (error) {
      if (error instanceof Error && error.message === "Validate Error") {
        return;
      }

      messageApi.error(t("transactions.createFailed"));
    }
  }

  async function handleDelete(transactionId: string) {
    try {
      await deleteTransaction(transactionId).unwrap();
      setSelectedRowKeys((currentKeys) => currentKeys.filter((key) => key !== transactionId));
      messageApi.success(t("transactions.deleted"));
    } catch {
      messageApi.error(t("transactions.deleteFailed"));
    }
  }

  async function handleBatchDelete() {
    if (selectedRowKeys.length === 0) {
      return;
    }

    let deletedCount = 0;

    for (const transactionId of selectedRowKeys) {
      try {
        await deleteTransaction(transactionId).unwrap();
        deletedCount += 1;
      } catch {
        messageApi.error(t("transactions.batchDeleteFailed"));
        return;
      }
    }

    setSelectedRowKeys([]);
    messageApi.success(t("transactions.batchDeleted", { count: deletedCount }));
  }

  return (
    <div className="page-content">
      {contextHolder}

      <Typography.Title level={2} style={{ margin: 0 }}>
        {t("transactions.title")}
      </Typography.Title>

      <Card>
        <Space wrap style={{ marginBottom: 12 }}>
          <Input.Search placeholder={t("transactions.searchPlaceholder")} style={{ width: 260 }} allowClear onSearch={setSearch} />

          <Select
            placeholder={t("transactions.type")}
            allowClear
            style={{ width: 140 }}
            value={type}
            onChange={(value) => setType(value)}
            options={[
              { value: 1, label: t("transactions.income") },
              { value: 2, label: t("transactions.expense") },
              { value: 3, label: t("transactions.transfer") }
            ]}
          />

          <Select
            placeholder={t("transactions.account")}
            allowClear
            style={{ width: 220 }}
            value={accountId}
            onChange={(value) => setAccountId(value)}
            options={accounts.map((account) => ({
              value: account.id,
              label: `${account.name} (${account.currencyCode})`
            }))}
          />

          <Select
            placeholder={t("transactions.category")}
            allowClear
            style={{ width: 220 }}
            value={categoryId}
            onChange={(value) => setCategoryId(value)}
            options={categories.map((category) => ({
              value: category.id,
              label: category.name
            }))}
          />

          <Button type="primary" icon={<PlusOutlined />} onClick={openModal}>
            {t("transactions.newTransaction")}
          </Button>

          <Popconfirm
            title={t("transactions.deleteSelectedTitle")}
            description={t("transactions.deleteSelectedDescription", { count: selectedRowKeys.length })}
            okText={t("common.delete")}
            cancelText={t("common.cancel")}
            disabled={selectedRowKeys.length === 0}
            onConfirm={() => void handleBatchDelete()}
          >
            <Button danger icon={<DeleteOutlined />} disabled={selectedRowKeys.length === 0} loading={isDeleting}>
              {t("transactions.deleteSelected")}
            </Button>
          </Popconfirm>
        </Space>

        <Table
          loading={isLoading}
          rowKey="id"
          dataSource={transactions}
          rowSelection={{
            selectedRowKeys,
            onChange: (nextSelectedRowKeys) => setSelectedRowKeys(nextSelectedRowKeys.map(String))
          }}
          columns={[
            {
              title: t("transactions.date"),
              dataIndex: "transactionDate",
              render: (value: string) => formatDate(value)
            },
            {
              title: t("transactions.account"),
              dataIndex: "accountId",
              render: (value: string) => accountMap.get(value) ?? value
            },
            {
              title: t("transactions.category"),
              dataIndex: "categoryId",
              render: (value: string | null) => (value ? categoryMap.get(value) ?? value : "-")
            },
            {
              title: t("transactions.type"),
              dataIndex: "type",
              render: (value: number) => (value === 1 ? t("transactions.income") : value === 2 ? t("transactions.expense") : t("transactions.transfer"))
            },
            {
              title: t("transactions.amount"),
              render: (_, row) => formatMoney(row.amount, row.currencyCode)
            },
            {
              title: t("transactions.description"),
              dataIndex: "description"
            },
            {
              title: t("transactions.actions"),
              key: "actions",
              width: 96,
              render: (_, row) => (
                <Popconfirm
                  title={t("transactions.deleteTitle")}
                  description={t("transactions.deleteDescription")}
                  okText={t("common.delete")}
                  cancelText={t("common.cancel")}
                  onConfirm={() => void handleDelete(row.id)}
                >
                  <Button danger type="text" icon={<DeleteOutlined />} loading={isDeleting} aria-label={t("transactions.deleteAria")} />
                </Popconfirm>
              )
            }
          ]}
        />
      </Card>

      <Modal
        title={t("transactions.modalTitle")}
        open={isModalOpen}
        onCancel={() => setIsModalOpen(false)}
        onOk={() => void handleCreate()}
        confirmLoading={isCreating}
        width={820}
        okText={t("common.create")}
        cancelText={t("common.cancel")}
      >
        <Space align="start" size={16} style={{ width: "100%" }}>
          <div style={{ flex: 1 }}>
            <Form
              form={form}
              layout="vertical"
              onValuesChange={(changedValues) => {
                if (changedValues.accountId) {
                  const account = accounts.find((item) => item.id === changedValues.accountId);
                  if (account && !form.getFieldValue("currencyCode")) {
                    form.setFieldValue("currencyCode", account.currencyCode);
                  }
                }
              }}
            >
              <Form.Item name="accountId" label={t("transactions.account")} rules={[{ required: true, message: t("transactions.accountRequired") }]}>
                <Select
                  options={accounts.map((account) => ({
                    value: account.id,
                    label: `${account.name} (${account.currencyCode})`
                  }))}
                />
              </Form.Item>

              <Form.Item name="categoryId" label={t("transactions.category")}>
                <Select
                  allowClear
                  options={categories.map((category) => ({
                    value: category.id,
                    label: category.name
                  }))}
                />
              </Form.Item>

              <Form.Item name="type" label={t("transactions.type")} rules={[{ required: true, message: t("transactions.typeRequired") }]}>
                <Select
                  options={[
                    { value: 1, label: t("transactions.income") },
                    { value: 2, label: t("transactions.expense") }
                  ]}
                />
              </Form.Item>

              <Form.Item name="amount" label={t("transactions.enteredAmount")} rules={[{ required: true, message: t("transactions.amountRequired") }]}>
                <InputNumber style={{ width: "100%" }} min={0.01} precision={2} />
              </Form.Item>

              <Form.Item name="currencyCode" label={t("transactions.operationCurrency")} rules={[{ required: true, message: t("transactions.currencyRequired") }]}>
                <Select options={currencyOptions} />
              </Form.Item>

              <Form.Item
                name="manualRate"
                label={t("transactions.manualRate")}
                extra={t("transactions.manualRateExtra")}
                rules={[
                  {
                    validator(_, value: number | undefined) {
                      if (value === undefined || value === null || value > 0) {
                        return Promise.resolve();
                      }

                      return Promise.reject(new Error(t("transactions.manualRateError")));
                    }
                  }
                ]}
              >
                <InputNumber style={{ width: "100%" }} min={0.000001} precision={6} placeholder={t("transactions.manualRatePlaceholder")} />
              </Form.Item>

              <Form.Item name="transactionDate" label={t("transactions.date")} rules={[{ required: true, message: t("transactions.dateRequired") }]}>
                <DatePicker style={{ width: "100%" }} format="DD.MM.YYYY" />
              </Form.Item>

              <Form.Item name="description" label={t("transactions.description")}>
                <Input />
              </Form.Item>
            </Form>
          </div>

          <div style={{ width: 280 }}>
            <Card size="small" title={t("transactions.previewTitle")}>
              <Space direction="vertical" style={{ width: "100%" }}>
                <Typography.Text>{t("transactions.accountCurrency")}: {selectedAccount?.currencyCode ?? "-"}</Typography.Text>
                <Typography.Text>{t("transactions.transactionCurrency")}: {values?.currencyCode ?? "-"}</Typography.Text>
                <Typography.Text>{t("transactions.autoRate")}: {rateLoading ? t("common.loading") : autoRate ?? t("common.notAvailable")}</Typography.Text>
                <Typography.Text>{t("transactions.appliedRate")}: {appliedRate ?? t("common.notAvailable")}</Typography.Text>
                <Typography.Text type="secondary">
                  {values?.manualRate && values.manualRate > 0 ? t("transactions.manualApplied") : t("transactions.autoApplied")}
                </Typography.Text>
                <Typography.Text strong>
                  {t("transactions.accountImpact")}:{" "}
                  {normalizedAmount === null || !selectedAccount ? t("common.notAvailable") : formatMoney(normalizedAmount, selectedAccount.currencyCode)}
                </Typography.Text>

                {!autoRate && selectedAccount && values?.currencyCode && !values?.manualRate && (
                  <Alert
                    type="warning"
                    showIcon
                    message={t("transactions.previewUnavailableTitle")}
                    description={t("transactions.previewUnavailableDescription")}
                  />
                )}
              </Space>
            </Card>
          </div>
        </Space>
      </Modal>
    </div>
  );
}
