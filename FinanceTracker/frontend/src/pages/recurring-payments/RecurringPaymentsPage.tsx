import { DeleteOutlined, EditOutlined, PlusOutlined } from "@ant-design/icons";
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
  Switch,
  Table,
  Tag,
  Typography,
  message
} from "antd";
import type { Dayjs } from "dayjs";
import dayjs from "dayjs";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";
import {
  useCreateRecurringPaymentMutation,
  useDeleteRecurringPaymentMutation,
  useGetRecurringPaymentsQuery,
  useSetRecurringPaymentActiveMutation,
  useUpdateRecurringPaymentMutation
} from "../../features/recurring-payments/recurringPaymentsApi";
import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";
import type { RecurringPaymentDto } from "../../shared/types/api";
import { TransactionType } from "../../shared/types/api";

interface RecurringPaymentFormValues {
  name: string;
  description?: string;
  accountId: string;
  categoryId?: string;
  type: TransactionType;
  amount: number;
  currencyCode: string;
  frequency: string;
  firstExecutionDate: Dayjs;
  endDate?: Dayjs | null;
  isActive: boolean;
}

export function RecurringPaymentsPage() {
  const { t } = useTranslation();
  const [messageApi, contextHolder] = message.useMessage();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<RecurringPaymentDto | null>(null);
  const [form] = Form.useForm<RecurringPaymentFormValues>();

  const { data: recurringPayments = [], isLoading, error } = useGetRecurringPaymentsQuery();
  const { data: accounts = [] } = useGetAccountsQuery({ includeArchived: false });
  const { data: categories = [] } = useGetCategoriesQuery();

  const [createRecurringPayment, { isLoading: isCreating }] = useCreateRecurringPaymentMutation();
  const [updateRecurringPayment, { isLoading: isUpdating }] = useUpdateRecurringPaymentMutation();
  const [setRecurringPaymentActive, { isLoading: isToggling }] = useSetRecurringPaymentActiveMutation();
  const [deleteRecurringPayment, { isLoading: isDeleting }] = useDeleteRecurringPaymentMutation();

  const selectedType = Form.useWatch("type", form) as TransactionType | undefined;
  const filteredCategories = useMemo(
    () =>
      categories.filter((category) =>
        selectedType === TransactionType.Income
          ? category.type === 1
          : selectedType === TransactionType.Expense
            ? category.type === 2
            : true),
    [categories, selectedType]
  );

  const frequencyOptions = [
    { value: "daily", label: t("recurring.daily") },
    { value: "weekly", label: t("recurring.weekly") },
    { value: "monthly", label: t("recurring.monthly") },
    { value: "yearly", label: t("recurring.yearly") }
  ];

  const currencyOptions = [
    { value: "USD", label: "USD" },
    { value: "EUR", label: "EUR" },
    { value: "RUB", label: "RUB" },
    { value: "BYN", label: "BYN" },
    { value: "JPY", label: "JPY" },
    { value: "CNY", label: "CNY" },
    { value: "GBP", label: "GBP" }
  ];

  function getTypeTag(type: TransactionType) {
    return type === TransactionType.Income
      ? <Tag color="success">{t("recurring.income")}</Tag>
      : <Tag color="error">{t("recurring.expense")}</Tag>;
  }

  function openCreateModal() {
    setEditingItem(null);
    form.resetFields();
    form.setFieldsValue({
      type: TransactionType.Expense,
      currencyCode: "USD",
      frequency: "monthly",
      firstExecutionDate: dayjs().startOf("day"),
      isActive: true
    });
    setIsModalOpen(true);
  }

  function openEditModal(item: RecurringPaymentDto) {
    setEditingItem(item);
    form.setFieldsValue({
      name: item.name,
      description: item.description ?? undefined,
      accountId: item.accountId ?? undefined,
      categoryId: item.categoryId ?? undefined,
      type: item.type,
      amount: item.amount,
      currencyCode: item.currencyCode,
      frequency: item.frequency,
      firstExecutionDate: item.startDate ? dayjs(item.startDate) : dayjs(),
      endDate: item.endDate ? dayjs(item.endDate) : null,
      isActive: item.isActive
    });
    setIsModalOpen(true);
  }

  async function handleSubmit() {
    try {
      const values = await form.validateFields();
      const payload = {
        name: values.name.trim(),
        description: values.description?.trim() || null,
        accountId: values.accountId,
        categoryId: values.categoryId || null,
        type: values.type,
        amount: values.amount,
        currencyCode: values.currencyCode.trim().toUpperCase(),
        frequency: values.frequency,
        firstExecutionDate: values.firstExecutionDate.toISOString(),
        endDate: values.endDate ? values.endDate.toISOString() : null
      };

      if (editingItem) {
        await updateRecurringPayment({
          id: editingItem.id,
          ...payload,
          isActive: values.isActive
        }).unwrap();

        messageApi.success(t("recurring.updated"));
      } else {
        await createRecurringPayment(payload).unwrap();
        messageApi.success(t("recurring.created"));
      }

      setIsModalOpen(false);
      setEditingItem(null);
      form.resetFields();
    } catch (error) {
      if (error instanceof Error && error.message === "Validate Error") {
        return;
      }

      messageApi.error(t("recurring.saveFailed"));
    }
  }

  async function handleToggleActive(item: RecurringPaymentDto, checked: boolean) {
    try {
      await setRecurringPaymentActive({
        id: item.id,
        isActive: checked
      }).unwrap();

      messageApi.success(checked ? t("recurring.enabled") : t("recurring.disabled"));
    } catch {
      messageApi.error(t("recurring.toggleFailed"));
    }
  }

  async function handleDelete(id: string) {
    try {
      await deleteRecurringPayment(id).unwrap();
      messageApi.success(t("recurring.deleted"));
    } catch {
      messageApi.error(t("recurring.deleteFailed"));
    }
  }

  return (
    <div className="page-content">
      {contextHolder}

      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          {t("recurring.title")}
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreateModal}>
          {t("recurring.newPayment")}
        </Button>
      </div>

      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        {t("recurring.subtitle")}
      </Typography.Paragraph>

      {error && (
        <Alert
          type="info"
          showIcon
          message={t("recurring.unavailableTitle")}
          description={t("recurring.unavailableDescription")}
        />
      )}

      <Card>
        <Table
          loading={isLoading}
          rowKey="id"
          dataSource={recurringPayments}
          columns={[
            {
              title: t("recurring.name"),
              render: (_, row) => (
                <div>
                  <Typography.Text strong>{row.name}</Typography.Text>
                  {row.description && (
                    <div>
                      <Typography.Text type="secondary">{row.description}</Typography.Text>
                    </div>
                  )}
                </div>
              )
            },
            {
              title: t("recurring.type"),
              dataIndex: "type",
              render: (value: TransactionType) => getTypeTag(value)
            },
            {
              title: t("recurring.amount"),
              render: (_, row) => formatMoney(row.amount, row.currencyCode)
            },
            {
              title: t("recurring.account"),
              render: (_, row) => row.accountName ?? "-"
            },
            {
              title: t("recurring.category"),
              render: (_, row) => row.categoryName ?? "-"
            },
            {
              title: t("recurring.frequency"),
              dataIndex: "frequency",
              render: (value: string) => frequencyOptions.find((item) => item.value === value)?.label ?? value
            },
            {
              title: t("recurring.nextExecution"),
              render: (_, row) => row.nextExecutionAt ? formatDate(row.nextExecutionAt) : "-"
            },
            {
              title: t("recurring.lastExecution"),
              render: (_, row) => row.lastExecutedAt ? formatDate(row.lastExecutedAt) : "-"
            },
            {
              title: t("recurring.active"),
              render: (_, row) => (
                <Switch
                  checked={row.isActive}
                  onChange={(checked) => void handleToggleActive(row, checked)}
                  loading={isToggling}
                />
              )
            },
            {
              title: t("recurring.actions"),
              width: 140,
              render: (_, row) => (
                <Space>
                  <Button icon={<EditOutlined />} onClick={() => openEditModal(row)} />
                  <Popconfirm
                    title={t("recurring.deleteTitle")}
                    description={t("recurring.deleteDescription")}
                    okText={t("common.delete")}
                    cancelText={t("common.cancel")}
                    onConfirm={() => void handleDelete(row.id)}
                  >
                    <Button danger icon={<DeleteOutlined />} loading={isDeleting} />
                  </Popconfirm>
                </Space>
              )
            }
          ]}
        />
      </Card>

      <Modal
        title={editingItem ? t("recurring.editTitle") : t("recurring.createTitle")}
        open={isModalOpen}
        onCancel={() => {
          setIsModalOpen(false);
          setEditingItem(null);
        }}
        onOk={() => void handleSubmit()}
        confirmLoading={isCreating || isUpdating}
        width={720}
        okText={editingItem ? t("common.save") : t("common.create")}
        cancelText={t("common.cancel")}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="name" label={t("recurring.name")} rules={[{ required: true, message: t("recurring.nameRequired") }]}>
            <Input placeholder={t("recurring.namePlaceholder")} />
          </Form.Item>

          <Form.Item name="description" label={t("recurring.description")}>
            <Input placeholder={t("recurring.descriptionPlaceholder")} />
          </Form.Item>

          <Space size={16} style={{ width: "100%" }} align="start">
            <Form.Item name="type" label={t("recurring.type")} rules={[{ required: true, message: t("recurring.typeRequired") }]} style={{ flex: 1 }}>
              <Select
                options={[
                  { value: TransactionType.Income, label: t("recurring.income") },
                  { value: TransactionType.Expense, label: t("recurring.expense") }
                ]}
              />
            </Form.Item>

            <Form.Item name="frequency" label={t("recurring.frequency")} rules={[{ required: true, message: t("recurring.frequencyRequired") }]} style={{ flex: 1 }}>
              <Select options={frequencyOptions} />
            </Form.Item>
          </Space>

          <Space size={16} style={{ width: "100%" }} align="start">
            <Form.Item name="accountId" label={t("recurring.account")} rules={[{ required: true, message: t("recurring.accountRequired") }]} style={{ flex: 1 }}>
              <Select
                options={accounts.map((account) => ({
                  value: account.id,
                  label: `${account.name} (${account.currencyCode})`
                }))}
              />
            </Form.Item>

            <Form.Item name="categoryId" label={t("recurring.category")} style={{ flex: 1 }}>
              <Select
                allowClear
                options={filteredCategories.map((category) => ({
                  value: category.id,
                  label: category.name
                }))}
              />
            </Form.Item>
          </Space>

          <Space size={16} style={{ width: "100%" }} align="start">
            <Form.Item name="amount" label={t("recurring.amount")} rules={[{ required: true, message: t("recurring.amountRequired") }]} style={{ flex: 1 }}>
              <InputNumber style={{ width: "100%" }} min={0.01} precision={2} />
            </Form.Item>

            <Form.Item name="currencyCode" label={t("recurring.currency")} rules={[{ required: true, message: t("recurring.currencyRequired") }]} style={{ flex: 1 }}>
              <Select options={currencyOptions} />
            </Form.Item>
          </Space>

          <Space size={16} style={{ width: "100%" }} align="start">
            <Form.Item
              name="firstExecutionDate"
              label={t("recurring.firstExecutionDate")}
              rules={[{ required: true, message: t("recurring.firstExecutionDateRequired") }]}
              style={{ flex: 1 }}
            >
              <DatePicker style={{ width: "100%" }} format="DD.MM.YYYY" />
            </Form.Item>

            <Form.Item name="endDate" label={t("recurring.endDate")} style={{ flex: 1 }}>
              <DatePicker style={{ width: "100%" }} format="DD.MM.YYYY" />
            </Form.Item>
          </Space>

          {editingItem && (
            <Form.Item name="isActive" label={t("recurring.active")} valuePropName="checked">
              <Switch />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </div>
  );
}
