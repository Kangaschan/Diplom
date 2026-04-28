import { useState } from "react";
import {
    Card,
    Empty,
    List,
    Skeleton,
    Tag,
    Typography,
    Button,
    Popconfirm,
    Modal,
    Form,
    Input,
    Select,
    message,
} from "antd";
import { EditOutlined, DeleteOutlined } from "@ant-design/icons";

import {
    useGetCategoriesQuery,
    useCreateCategoryMutation,
    useUpdateCategoryMutation,
    useDeleteCategoryMutation,
} from "../../features/categories/categoriesApi";
import type { CategoryDto } from "../../shared/types/api";

export function CategoriesPage() {
    const { data = [], isLoading } = useGetCategoriesQuery();

    const [createCategory, { isLoading: isCreating }] =
        useCreateCategoryMutation();
    const [updateCategory, { isLoading: isUpdating }] =
        useUpdateCategoryMutation();
    const [deleteCategory] = useDeleteCategoryMutation();

    const [modalOpen, setModalOpen] = useState(false);
    const [editingCategory, setEditingCategory] = useState<CategoryDto | null>(null);

    const [form] = Form.useForm();

    // --- handlers ---

    const handleCreate = () => {
        setEditingCategory(null);
        form.resetFields();
        setModalOpen(true);
    };

    const handleEdit = (category: CategoryDto) => {
        setEditingCategory(category);
        form.setFieldsValue(category);
        setModalOpen(true);
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteCategory(id).unwrap();
            message.success("Category deleted");
        } catch {
            message.error("Delete failed");
        }
    };

    const handleSubmit = async () => {
        try {
            const values = await form.validateFields();

            if (editingCategory) {
                await updateCategory({
                    id: editingCategory.id,
                    ...values,
                }).unwrap();

                message.success("Category updated");
            } else {
                await createCategory(values).unwrap();
                message.success("Category created");
            }

            setModalOpen(false);
        } catch {
            message.error("Save failed");
        }
    };

    // --- UI ---

    return (
        <div className="page-content">
            <Typography.Title level={2} style={{ margin: 0 }}>
                Categories
            </Typography.Title>

            <Card>
                {isLoading ? (
                    <Skeleton active paragraph={{ rows: 6 }} />
                ) : data.length === 0 ? (
                    <Empty description="No categories">
                        <Button type="primary" onClick={handleCreate}>
                            Create first category
                        </Button>
                    </Empty>
                ) : (
                    <>
                        <List
                            dataSource={[...data].sort((a, b) => a.type - b.type)}
                            renderItem={(item: CategoryDto) => (
                                <List.Item
                                    actions={[
                                        <Button
                                            type="text"
                                            icon={<EditOutlined />}
                                            onClick={() => handleEdit(item)}
                                        />,
                                        <Popconfirm
                                            title="Delete category?"
                                            onConfirm={() => handleDelete(item.id)}
                                        >
                                            <Button
                                                danger
                                                type="text"
                                                icon={<DeleteOutlined />}
                                            />
                                        </Popconfirm>,
                                    ]}
                                >
                                    <List.Item.Meta
                                        title={item.name}
                                        description={
                                            <Tag color={item.type === 1 ? "green" : "red"}>
                                                {item.type === 1 ? "Income" : "Expense"}
                                            </Tag>
                                        }
                                    />
                                </List.Item>
                            )}
                        />

                        <Button
                            type="dashed"
                            block
                            style={{ marginTop: 16 }}
                            onClick={handleCreate}
                        >
                            + Create category
                        </Button>
                    </>
                )}
            </Card>

            {/* MODAL */}

            <Modal
                title={editingCategory ? "Edit Category" : "Create Category"}
                open={modalOpen}
                onCancel={() => setModalOpen(false)}
                onOk={handleSubmit}
                confirmLoading={isCreating || isUpdating}
            >
                <Form form={form} layout="vertical">
                    <Form.Item
                        name="name"
                        label="Name"
                        rules={[{ required: true, message: "Enter name" }]}
                    >
                        <Input placeholder="e.g. Salary, Food..." />
                    </Form.Item>

                    <Form.Item
                        name="type"
                        label="Type"
                        rules={[{ required: true, message: "Select type" }]}
                    >
                        <Select
                            options={[
                                { value: 1, label: "Income" },
                                { value: 2, label: "Expense" },
                            ]}
                        />
                    </Form.Item>
                </Form>
            </Modal>
        </div>
    );
}