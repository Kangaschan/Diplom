import { PlusOutlined } from "@ant-design/icons";
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Table,
  Typography,
  message
} from "antd";
import dayjs from "dayjs";
import { useEffect, useMemo, useState } from "react";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";
import { useCreateTransactionMutation, useGetTransactionsQuery } from "../../features/transactions/transactionsApi";
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
  const [search, setSearch] = useState("");
  const [type, setType] = useState<number | undefined>(undefined);
  const [accountId, setAccountId] = useState<string | undefined>(undefined);
  const [categoryId, setCategoryId] = useState<string | undefined>(undefined);
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

  const accountMap = useMemo(() => new Map(accounts.map((account) => [account.id, account.name])), [accounts]);
  const categoryMap = useMemo(() => new Map(categories.map((category) => [category.id, category.name])), [categories]);

  const values = Form.useWatch([], form) as TransactionFormValues | undefined;
  const selectedAccount = useMemo(
    () => accounts.find((account) => account.id === values?.accountId),
    [accounts, values?.accountId]
  );

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

      messageApi.success("Transaction created.");
      setIsModalOpen(false);
      form.resetFields();
    } catch (error) {
      if (error instanceof Error && error.message === "Validate Error") {
        return;
      }

      messageApi.error("Failed to create transaction.");
    }
  }

  return (
    <div className="page-content">
      {contextHolder}

      <Typography.Title level={2} style={{ margin: 0 }}>
        Transactions
      </Typography.Title>

      <Card>
        <Space wrap style={{ marginBottom: 12 }}>
          <Input.Search
            placeholder="Search by description"
            style={{ width: 260 }}
            allowClear
            onSearch={setSearch}
          />

          <Select
            placeholder="Type"
            allowClear
            style={{ width: 140 }}
            value={type}
            onChange={(value) => setType(value)}
            options={[
              { value: 1, label: "Income" },
              { value: 2, label: "Expense" },
              { value: 3, label: "Transfer" }
            ]}
          />

          <Select
            placeholder="Account"
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
            placeholder="Category"
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
            New transaction
          </Button>
        </Space>

        <Table
          loading={isLoading}
          rowKey="id"
          dataSource={transactions}
          columns={[
            {
              title: "Date",
              dataIndex: "transactionDate",
              render: (value: string) => formatDate(value)
            },
            {
              title: "Account",
              dataIndex: "accountId",
              render: (value: string) => accountMap.get(value) ?? value
            },
            {
              title: "Category",
              dataIndex: "categoryId",
              render: (value: string | null) => (value ? categoryMap.get(value) ?? value : "-")
            },
            {
              title: "Type",
              dataIndex: "type",
              render: (value: number) => (value === 1 ? "Income" : value === 2 ? "Expense" : "Transfer")
            },
            {
              title: "Amount",
              render: (_, row) => formatMoney(row.amount, row.currencyCode)
            },
            {
              title: "Description",
              dataIndex: "description"
            }
          ]}
        />
      </Card>

      <Modal
        title="New transaction"
        open={isModalOpen}
        onCancel={() => setIsModalOpen(false)}
        onOk={() => void handleCreate()}
        confirmLoading={isCreating}
        width={820}
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
              <Form.Item name="accountId" label="Account" rules={[{ required: true, message: "Select account" }]}>
                <Select
                  options={accounts.map((account) => ({
                    value: account.id,
                    label: `${account.name} (${account.currencyCode})`
                  }))}
                />
              </Form.Item>

              <Form.Item name="categoryId" label="Category">
                <Select
                  allowClear
                  options={categories.map((category) => ({
                    value: category.id,
                    label: category.name
                  }))}
                />
              </Form.Item>

              <Form.Item name="type" label="Type" rules={[{ required: true, message: "Select type" }]}>
                <Select
                  options={[
                    { value: 1, label: "Income" },
                    { value: 2, label: "Expense" }
                  ]}
                />
              </Form.Item>

              <Form.Item name="amount" label="Entered amount" rules={[{ required: true, message: "Enter amount" }]}>
                <InputNumber style={{ width: "100%" }} min={0.01} precision={2} />
              </Form.Item>

              <Form.Item name="currencyCode" label="Entered currency" rules={[{ required: true, message: "Select currency" }]}>
                <Select options={currencyOptions} />
              </Form.Item>

              <Form.Item
                name="manualRate"
                label="Manual exchange rate"
                extra="Optional. Leave empty to use the current exchange rate automatically."
                rules={[
                  {
                    validator(_, value: number | undefined) {
                      if (value === undefined || value === null || value > 0) {
                        return Promise.resolve();
                      }

                      return Promise.reject(new Error("Manual rate must be greater than zero"));
                    }
                  }
                ]}
              >
                <InputNumber style={{ width: "100%" }} min={0.000001} precision={6} placeholder="For example: 4" />
              </Form.Item>

              <Form.Item name="transactionDate" label="Date" rules={[{ required: true, message: "Select date" }]}>
                <DatePicker style={{ width: "100%" }} format="DD.MM.YYYY" />
              </Form.Item>

              <Form.Item name="description" label="Description">
                <Input />
              </Form.Item>
            </Form>
          </div>

          <div style={{ width: 280 }}>
            <Card size="small" title="Preview">
              <Space direction="vertical" style={{ width: "100%" }}>
                <Typography.Text>Account currency: {selectedAccount?.currencyCode ?? "-"}</Typography.Text>
                <Typography.Text>Entered currency: {values?.currencyCode ?? "-"}</Typography.Text>
                <Typography.Text>Auto rate: {rateLoading ? "Loading..." : autoRate ?? "n/a"}</Typography.Text>
                <Typography.Text>Applied rate: {appliedRate ?? "n/a"}</Typography.Text>
                <Typography.Text type="secondary">
                  {values?.manualRate && values.manualRate > 0
                    ? "Manual rate is enabled for this transaction."
                    : "Current exchange rate will be used automatically."}
                </Typography.Text>
                <Typography.Text strong>
                  Account impact:{" "}
                  {normalizedAmount === null || !selectedAccount
                    ? "n/a"
                    : formatMoney(normalizedAmount, selectedAccount.currencyCode)}
                </Typography.Text>

                {!autoRate && selectedAccount && values?.currencyCode && !values?.manualRate && (
                  <Alert
                    type="warning"
                    showIcon
                    message="Rate preview unavailable"
                    description="The backend will still try to convert the transaction using cached exchange rates."
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
