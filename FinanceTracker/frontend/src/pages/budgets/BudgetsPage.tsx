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
import { useTranslation } from "react-i18next";

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
  const { t } = useTranslation();
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
        return t("budgets.statusWarning");
      case "exceeded":
        return t("budgets.statusExceeded");
      default:
        return t("budgets.statusNormal");
    }
  }

  function getPeriodLabel(periodType: BudgetPeriodType) {
    switch (periodType) {
      case BudgetPeriodType.Weekly:
        return t("budgets.periodWeek");
      case BudgetPeriodType.Custom:
        return t("budgets.periodCustom");
      default:
        return t("budgets.periodMonth");
    }
  }

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
      messageApi.success(t("budgets.deleted"));
    } catch {
      messageApi.error(t("budgets.deleteFailed"));
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
        messageApi.success(t("budgets.updated"));
      } else {
        await createBudget(payload).unwrap();
        messageApi.success(t("budgets.created"));
      }

      closeModal();
    } catch (error) {
      if (error instanceof Error && error.message === "Validate Error") {
        return;
      }

      messageApi.error(t("budgets.saveFailed"));
    }
  }

  return (
    <div className="page-content">
      {contextHolder}

      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          {t("budgets.title")}
        </Typography.Title>
        <Button type="primary" onClick={openCreateModal}>
          {t("budgets.newBudget")}
        </Button>
      </div>

      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        {t("budgets.subtitle")}
      </Typography.Paragraph>

      <Card>
        {isLoading ? (
          <Skeleton active paragraph={{ rows: 6 }} />
        ) : usage.length === 0 ? (
          <Empty description={t("budgets.noBudgets")}>
            <Button type="primary" onClick={openCreateModal}>
              {t("budgets.createFirst")}
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
              const scopeLabel = item.accountName ? t("budgets.scopeAccount", { name: item.accountName }) : t("budgets.scopeAllAccounts");

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
                          title={t("budgets.deleteTitle")}
                          description={t("budgets.deleteDescription")}
                          okText={t("common.delete")}
                          cancelText={t("common.cancel")}
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
                      <Typography.Text>{t("budgets.limit")}: {formatMoney(item.limitAmount, item.currencyCode)}</Typography.Text>
                      <Typography.Text>{t("budgets.used")}: {formatMoney(item.usedAmount, item.currencyCode)}</Typography.Text>
                      <Typography.Text>{t("budgets.remaining")}: {formatMoney(item.remainingAmount, item.currencyCode)}</Typography.Text>
                      <Typography.Text>{Number(item.percentUsed).toFixed(2)}%</Typography.Text>
                    </Space>

                    <Typography.Text type="secondary">
                      {t("budgets.periodLabel")}: {formatDate(item.startDate)} - {formatDate(item.endDate)}
                    </Typography.Text>
                  </Space>
                </Card>
              );
            })}
          </Space>
        )}
      </Card>

      <Modal
        title={editingBudget ? t("budgets.editTitle") : t("budgets.createTitle")}
        open={modalOpen}
        onCancel={closeModal}
        onOk={() => void handleSubmit()}
        confirmLoading={isCreating || isUpdating}
        okText={editingBudget ? t("budgets.saveChanges") : t("budgets.createTitle")}
        cancelText={t("common.cancel")}
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
          <Form.Item name="categoryId" label={t("budgets.category")} rules={[{ required: true, message: t("budgets.categoryRequired") }]}>
            <Select
              disabled={Boolean(editingBudget)}
              placeholder={t("budgets.categoryPlaceholder")}
              options={expenseCategories.map((category) => ({
                value: category.id,
                label: category.name
              }))}
            />
          </Form.Item>

          <Form.Item name="accountId" label={t("budgets.account")}>
            <Select
              allowClear
              disabled={Boolean(editingBudget)}
              placeholder={t("budgets.allAccounts")}
              options={accounts.map((account) => ({
                value: account.id,
                label: `${account.name} (${account.currencyCode})`
              }))}
            />
          </Form.Item>

          <Form.Item name="limitAmount" label={t("budgets.limitAmount")} rules={[{ required: true, message: t("budgets.limitAmountRequired") }]}>
            <InputNumber style={{ width: "100%" }} precision={2} min={0.01} />
          </Form.Item>

          <Form.Item
            name="currencyCode"
            label={t("budgets.currency")}
            rules={[
              { required: true, message: t("budgets.currencyRequired") },
              { len: 3, message: t("budgets.currencyLength") }
            ]}
          >
            <Input maxLength={3} disabled={Boolean(editingBudget)} />
          </Form.Item>

          <Form.Item name="periodType" label={t("budgets.periodType")} rules={[{ required: true, message: t("budgets.periodTypeRequired") }]}>
            <Select
              disabled={Boolean(editingBudget)}
              options={[
                { value: BudgetPeriodType.Monthly, label: t("budgets.periodMonth") },
                { value: BudgetPeriodType.Weekly, label: t("budgets.periodWeek") },
                { value: BudgetPeriodType.Custom, label: t("budgets.periodCustom") }
              ]}
            />
          </Form.Item>

          <Form.Item name="startDate" label={t("budgets.startDate")} rules={[{ required: true, message: t("budgets.startDateRequired") }]}>
            <DatePicker style={{ width: "100%" }} format="DD.MM.YYYY" />
          </Form.Item>

          <Form.Item
            name="endDate"
            label={t("budgets.endDate")}
            dependencies={["startDate"]}
            rules={[
              { required: true, message: t("budgets.endDateRequired") },
              ({ getFieldValue }) => ({
                validator(_, value: dayjs.Dayjs | undefined) {
                  const startDate = getFieldValue("startDate") as dayjs.Dayjs | undefined;

                  if (!startDate || !value || !value.isBefore(startDate, "day")) {
                    return Promise.resolve();
                  }

                  return Promise.reject(new Error(t("budgets.endDateError")));
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
