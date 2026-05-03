import { DeleteOutlined, EditOutlined } from "@ant-design/icons";
import {
  Button,
  Card,
  DatePicker,
  Empty,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Progress,
  Select,
  Skeleton,
  Space,
  Tag,
  Typography,
  message
} from "antd";
import dayjs from "dayjs";
import { useMemo, useState } from "react";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import {
  type BudgetDto,
  type BudgetUsageDto,
  useCreateBudgetMutation,
  useDeleteBudgetMutation,
  useGetBudgetsQuery,
  useGetBudgetsUsageQuery,
  useUpdateBudgetMutation
} from "../../features/budgets/budgetsApi";
import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";
import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";
import { BudgetPeriodType } from "../../shared/types/api";

interface BudgetFormValues {
  categoryId: string;
  accountId?: string | null;
  limitAmount: number;
  currencyCode: string;
  periodType: BudgetPeriodType;
  startDate: dayjs.Dayjs;
  endDate: dayjs.Dayjs;
}

function getBudgetStatusColor(status: BudgetUsageDto["status"]) {
  switch (status) {
    case "warning":
      return "warning";
    case "exceeded":
      return "error";
    default:
      return "success";
  }
}

function getBudgetStatusLabel(status: BudgetUsageDto["status"]) {
  switch (status) {
    case "warning":
      return "Near limit";
    case "exceeded":
      return "Exceeded";
    default:
      return "Normal";
  }
}

function getPeriodLabel(periodType: BudgetPeriodType) {
  switch (periodType) {
    case BudgetPeriodType.Weekly:
      return "Weekly";
    case BudgetPeriodType.Custom:
      return "Custom";
    default:
      return "Monthly";
  }
}

function getSuggestedPeriodRange(periodType: BudgetPeriodType) {
  if (periodType === BudgetPeriodType.Weekly) {
    return {
      startDate: dayjs().startOf("week"),
      endDate: dayjs().endOf("week")
    };
  }

  if (periodType === BudgetPeriodType.Custom) {
    return {
      startDate: dayjs(),
      endDate: dayjs().add(30, "day")
    };
  }

  return {
    startDate: dayjs().startOf("month"),
    endDate: dayjs().endOf("month")
  };
}

export function BudgetsPage() {
  const [messageApi, contextHolder] = message.useMessage();
  const [modalOpen, setModalOpen] = useState(false);
  const [editingBudget, setEditingBudget] = useState<BudgetDto | null>(null);
  const [form] = Form.useForm<BudgetFormValues>();

  const { data: budgets = [] } = useGetBudgetsQuery();
  const { data: usage = [], isLoading } = useGetBudgetsUsageQuery();
  const { data: categories = [] } = useGetCategoriesQuery();
  const { data: accounts = [] } = useGetAccountsQuery({ includeArchived: false });
  const [createBudget, { isLoading: isCreating }] = useCreateBudgetMutation();
  const [updateBudget, { isLoading: isUpdating }] = useUpdateBudgetMutation();
  const [deleteBudget, { isLoading: isDeleting }] = useDeleteBudgetMutation();

  const expenseCategories = useMemo(
    () => categories.filter((category) => category.type === 2 && category.isActive !== false),
    [categories]
  );

  function openCreateModal() {
    const initialRange = getSuggestedPeriodRange(BudgetPeriodType.Monthly);

    setEditingBudget(null);
    form.resetFields();
    form.setFieldsValue({
      periodType: BudgetPeriodType.Monthly,
      currencyCode: "USD",
      startDate: initialRange.startDate,
      endDate: initialRange.endDate
    });
    setModalOpen(true);
  }

  function openEditModal(budget: BudgetDto) {
    setEditingBudget(budget);
    form.setFieldsValue({
      categoryId: budget.categoryId,
      accountId: budget.accountId ?? undefined,
      limitAmount: budget.limitAmount,
      currencyCode: budget.currencyCode,
      periodType: budget.periodType,
      startDate: dayjs(budget.startDate),
      endDate: dayjs(budget.endDate)
    });
    setModalOpen(true);
  }

  function closeModal() {
    setModalOpen(false);
    setEditingBudget(null);
    form.resetFields();
  }

  async function handleDelete(id: string) {
    try {
      await deleteBudget(id).unwrap();
      messageApi.success("Budget deleted.");
    } catch {
      messageApi.error("Failed to delete budget.");
    }
  }

  async function handleSubmit() {
    try {
      const values = await form.validateFields();

      const payload = {
        categoryId: values.categoryId,
        accountId: values.accountId ?? null,
        limitAmount: values.limitAmount,
        currencyCode: values.currencyCode.trim().toUpperCase(),
        periodType: values.periodType,
        startDate: values.startDate.startOf("day").toISOString(),
        endDate: values.endDate.endOf("day").toISOString()
      };

      if (editingBudget) {
        await updateBudget({
          id: editingBudget.id,
          limitAmount: payload.limitAmount,
          startDate: payload.startDate,
          endDate: payload.endDate
        }).unwrap();
        messageApi.success("Budget updated.");
      } else {
        await createBudget(payload).unwrap();
        messageApi.success("Budget created.");
      }

      closeModal();
    } catch (error) {
      if (error instanceof Error && error.message === "Validate Error") {
        return;
      }

      messageApi.error("Failed to save budget.");
    }
  }

  return (
    <div className="page-content">
      {contextHolder}

      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          Budgets
        </Typography.Title>
        <Button type="primary" onClick={openCreateModal}>
          New budget
        </Button>
      </div>

      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        Track spending limits by category, monitor progress, and get notified before overspending.
      </Typography.Paragraph>

      <Card>
        {isLoading ? (
          <Skeleton active paragraph={{ rows: 6 }} />
        ) : usage.length === 0 ? (
          <Empty description="No budgets yet">
            <Button type="primary" onClick={openCreateModal}>
              Create first budget
            </Button>
          </Empty>
        ) : (
          <Space direction="vertical" size={16} style={{ width: "100%" }}>
            {usage.map((item) => {
              const fallbackBudget: BudgetDto = {
                id: item.budgetId,
                categoryId: item.categoryId,
                accountId: item.accountId ?? null,
                limitAmount: item.limitAmount,
                currencyCode: item.currencyCode,
                periodType: item.periodType,
                startDate: item.startDate,
                endDate: item.endDate
              };

              const editableBudget = budgets.find((budget) => budget.id === item.budgetId) ?? fallbackBudget;
              const scopeLabel = item.accountName ? `Account: ${item.accountName}` : "All accounts";

              return (
                <Card key={item.budgetId} size="small">
                  <Space direction="vertical" size={12} style={{ width: "100%" }}>
                    <div className="page-header">
                      <Space direction="vertical" size={2}>
                        <Typography.Text strong>{item.categoryName}</Typography.Text>
                        <Typography.Text type="secondary">
                          {scopeLabel} | {getPeriodLabel(item.periodType)}
                        </Typography.Text>
                      </Space>
                      <Space>
                        <Tag color={getBudgetStatusColor(item.status)}>{getBudgetStatusLabel(item.status)}</Tag>
                        <Button type="text" icon={<EditOutlined />} onClick={() => openEditModal(editableBudget)} />
                        <Popconfirm
                          title="Delete budget?"
                          description="This action cannot be undone."
                          okText="Delete"
                          cancelText="Cancel"
                          onConfirm={() => void handleDelete(item.budgetId)}
                        >
                          <Button danger type="text" icon={<DeleteOutlined />} loading={isDeleting} />
                        </Popconfirm>
                      </Space>
                    </div>

                    <Progress
                      percent={Math.max(0, Math.min(100, Number(item.percentUsed)))}
                      status={item.isExceeded ? "exception" : item.isNearLimit ? "active" : "success"}
                    />

                    <Space size={20} wrap>
                      <Typography.Text>Limit: {formatMoney(item.limitAmount, item.currencyCode)}</Typography.Text>
                      <Typography.Text>Used: {formatMoney(item.usedAmount, item.currencyCode)}</Typography.Text>
                      <Typography.Text>Remaining: {formatMoney(item.remainingAmount, item.currencyCode)}</Typography.Text>
                      <Typography.Text>{Number(item.percentUsed).toFixed(2)}%</Typography.Text>
                    </Space>

                    <Typography.Text type="secondary">
                      Period: {formatDate(item.startDate)} - {formatDate(item.endDate)}
                    </Typography.Text>
                  </Space>
                </Card>
              );
            })}
          </Space>
        )}
      </Card>

      <Modal
        title={editingBudget ? "Edit budget" : "Create budget"}
        open={modalOpen}
        onCancel={closeModal}
        onOk={() => void handleSubmit()}
        confirmLoading={isCreating || isUpdating}
        okText={editingBudget ? "Save changes" : "Create budget"}
        cancelText="Cancel"
        destroyOnHidden
      >
        <Form
          form={form}
          layout="vertical"
          onValuesChange={(changedValues) => {
            if (!editingBudget && changedValues.accountId) {
              const selectedAccount = accounts.find((account) => account.id === changedValues.accountId);
              if (selectedAccount) {
                form.setFieldValue("currencyCode", selectedAccount.currencyCode);
              }
            }

            if (!editingBudget && changedValues.periodType) {
              const nextRange = getSuggestedPeriodRange(changedValues.periodType as BudgetPeriodType);
              form.setFieldsValue({
                startDate: nextRange.startDate,
                endDate: nextRange.endDate
              });
            }
          }}
        >
          <Form.Item
            name="categoryId"
            label="Category"
            rules={[{ required: true, message: "Select category" }]}
          >
            <Select
              disabled={Boolean(editingBudget)}
              placeholder="Select expense category"
              options={expenseCategories.map((category) => ({
                value: category.id,
                label: category.name
              }))}
            />
          </Form.Item>

          <Form.Item name="accountId" label="Account">
            <Select
              allowClear
              disabled={Boolean(editingBudget)}
              placeholder="All accounts"
              options={accounts.map((account) => ({
                value: account.id,
                label: `${account.name} (${account.currencyCode})`
              }))}
            />
          </Form.Item>

          <Form.Item
            name="limitAmount"
            label="Limit amount"
            rules={[{ required: true, message: "Enter limit amount" }]}
          >
            <InputNumber style={{ width: "100%" }} precision={2} min={0.01} />
          </Form.Item>

          <Form.Item
            name="currencyCode"
            label="Currency"
            rules={[
              { required: true, message: "Enter currency" },
              { len: 3, message: "Use a 3-letter currency code" }
            ]}
          >
            <Input maxLength={3} disabled={Boolean(editingBudget)} />
          </Form.Item>

          <Form.Item
            name="periodType"
            label="Period type"
            rules={[{ required: true, message: "Select period type" }]}
          >
            <Select
              disabled={Boolean(editingBudget)}
              options={[
                { value: BudgetPeriodType.Monthly, label: "Monthly" },
                { value: BudgetPeriodType.Weekly, label: "Weekly" },
                { value: BudgetPeriodType.Custom, label: "Custom" }
              ]}
            />
          </Form.Item>

          <Form.Item
            name="startDate"
            label="Start date"
            rules={[{ required: true, message: "Select start date" }]}
          >
            <DatePicker style={{ width: "100%" }} format="DD.MM.YYYY" />
          </Form.Item>

          <Form.Item
            name="endDate"
            label="End date"
            dependencies={["startDate"]}
            rules={[
              { required: true, message: "Select end date" },
              ({ getFieldValue }) => ({
                validator(_, value: dayjs.Dayjs | undefined) {
                  const startDate = getFieldValue("startDate") as dayjs.Dayjs | undefined;

                  if (!startDate || !value || !value.isBefore(startDate, "day")) {
                    return Promise.resolve();
                  }

                  return Promise.reject(new Error("End date cannot be earlier than start date"));
                }
              })
            ]}
          >
            <DatePicker style={{ width: "100%" }} format="DD.MM.YYYY" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
