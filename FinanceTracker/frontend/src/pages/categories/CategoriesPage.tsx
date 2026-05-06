import { DeleteOutlined, EditOutlined } from "@ant-design/icons";
import {
  Button,
  Card,
  Col,
  Empty,
  Form,
  Input,
  List,
  Modal,
  Popconfirm,
  Progress,
  Row,
  Segmented,
  Select,
  Skeleton,
  Space,
  Tag,
  Typography,
  message
} from "antd";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import {
  useCreateCategoryMutation,
  useDeleteCategoryMutation,
  useGetCategoriesQuery,
  useGetExpenseCategoryStatsQuery,
  useUpdateCategoryMutation
} from "../../features/categories/categoriesApi";
import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";
import type { CategoryDto } from "../../shared/types/api";

export function CategoriesPage() {
  const { t } = useTranslation();
  const { data = [], isLoading } = useGetCategoriesQuery();
  const [statsPeriod, setStatsPeriod] = useState<1 | 2 | 3>(2);
  const { data: expenseStats = [], isLoading: isStatsLoading } = useGetExpenseCategoryStatsQuery(statsPeriod);

  const [createCategory, { isLoading: isCreating }] = useCreateCategoryMutation();
  const [updateCategory, { isLoading: isUpdating }] = useUpdateCategoryMutation();
  const [deleteCategory] = useDeleteCategoryMutation();

  const [modalOpen, setModalOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<CategoryDto | null>(null);

  const [form] = Form.useForm();

  const maxExpenseAmount = useMemo(() => expenseStats.reduce((max, item) => Math.max(max, item.amount), 0), [expenseStats]);
  const visibleExpenseStats = useMemo(
    () => expenseStats.filter((item) => item.transactionsCount > 0 || item.amount > 0).sort((left, right) => right.amount - left.amount),
    [expenseStats]
  );

  function handleCreate() {
    setEditingCategory(null);
    form.resetFields();
    setModalOpen(true);
  }

  function handleEdit(category: CategoryDto) {
    setEditingCategory(category);
    form.setFieldsValue({
      name: category.name,
      type: category.type
    });
    setModalOpen(true);
  }

  async function handleDelete(id: string) {
    try {
      await deleteCategory(id).unwrap();
      message.success(t("categories.deleted"));
    } catch {
      message.error(t("categories.deleteFailed"));
    }
  }

  async function handleSubmit() {
    try {
      const values = await form.validateFields();

      if (editingCategory) {
        await updateCategory({
          id: editingCategory.id,
          name: values.name,
          isActive: editingCategory.isActive ?? true
        }).unwrap();

        message.success(t("categories.updated"));
      } else {
        await createCategory(values).unwrap();
        message.success(t("categories.created"));
      }

      setModalOpen(false);
    } catch {
      message.error(t("categories.saveFailed"));
    }
  }

  return (
    <div className="page-content">
      <Typography.Title level={2} style={{ margin: 0 }}>
        {t("categories.title")}
      </Typography.Title>

      <Card>
        <Space direction="vertical" size={16} style={{ width: "100%" }}>
          <Space style={{ width: "100%", justifyContent: "space-between" }} wrap>
            <Typography.Title level={4} style={{ margin: 0 }}>
              {t("categories.expenseStats")}
            </Typography.Title>
            <Segmented
              value={statsPeriod}
              onChange={(value) => setStatsPeriod(value as 1 | 2 | 3)}
              options={[
                { value: 1, label: t("categories.week") },
                { value: 2, label: t("categories.month") },
                { value: 3, label: t("categories.year") }
              ]}
            />
          </Space>

          {expenseStats.length > 0 && (
            <Typography.Text type="secondary">
              {t("categories.period")}: {formatDate(expenseStats[0].from)} - {formatDate(expenseStats[0].to)}
            </Typography.Text>
          )}

          {isStatsLoading ? (
            <Skeleton active paragraph={{ rows: 4 }} />
          ) : visibleExpenseStats.length === 0 ? (
            <Empty description={t("categories.noExpenseActivity")} />
          ) : (
            <Row gutter={[16, 16]}>
              {visibleExpenseStats.map((item) => (
                <Col xs={24} md={12} key={item.categoryId}>
                  <Card size="small">
                    <Space direction="vertical" size={10} style={{ width: "100%" }}>
                      <Space style={{ width: "100%", justifyContent: "space-between" }}>
                        <Typography.Text strong>{item.categoryName}</Typography.Text>
                        <Typography.Text>{formatMoney(item.amount, item.currencyCode)}</Typography.Text>
                      </Space>
                      <Progress
                        percent={maxExpenseAmount > 0 ? Number(((item.amount / maxExpenseAmount) * 100).toFixed(0)) : 0}
                        showInfo={false}
                        strokeColor="#326586"
                      />
                      <Typography.Text type="secondary">{t("categories.operationsCount", { count: item.transactionsCount })}</Typography.Text>
                    </Space>
                  </Card>
                </Col>
              ))}
            </Row>
          )}
        </Space>
      </Card>

      <Card>
        {isLoading ? (
          <Skeleton active paragraph={{ rows: 6 }} />
        ) : data.length === 0 ? (
          <Empty description={t("categories.noCategories")}>
            <Button type="primary" onClick={handleCreate}>
              {t("categories.createFirst")}
            </Button>
          </Empty>
        ) : (
          <>
            <List
              dataSource={[...data].sort((a, b) => a.type - b.type)}
              renderItem={(item: CategoryDto) => (
                <List.Item
                  actions={[
                    <Button key="edit" type="text" icon={<EditOutlined />} onClick={() => handleEdit(item)} />,
                    <Popconfirm key="delete" title={t("categories.deleteTitle")} onConfirm={() => void handleDelete(item.id)}>
                      <Button danger type="text" icon={<DeleteOutlined />} />
                    </Popconfirm>
                  ]}
                >
                  <List.Item.Meta
                    title={item.name}
                    description={<Tag color={item.type === 1 ? "green" : "red"}>{item.type === 1 ? t("categories.income") : t("categories.expense")}</Tag>}
                  />
                </List.Item>
              )}
            />

            <Button type="dashed" block style={{ marginTop: 16 }} onClick={handleCreate}>
              + {t("categories.create")}
            </Button>
          </>
        )}
      </Card>

      <Modal
        title={editingCategory ? t("categories.edit") : t("categories.create")}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        confirmLoading={isCreating || isUpdating}
        okText={editingCategory ? t("common.save") : t("common.create")}
        cancelText={t("common.cancel")}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="name" label={t("categories.name")} rules={[{ required: true, message: t("categories.nameRequired") }]}>
            <Input placeholder={t("categories.namePlaceholder")} />
          </Form.Item>

          {!editingCategory && (
            <Form.Item name="type" label={t("categories.type")} rules={[{ required: true, message: t("categories.typeRequired") }]}>
              <Select
                options={[
                  { value: 1, label: t("categories.income") },
                  { value: 2, label: t("categories.expense") }
                ]}
              />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </div>
  );
}
