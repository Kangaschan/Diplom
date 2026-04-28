import {
    Card,
    Input,
    Select,
    Space,
    Table,
    Typography,
    Button,
    Modal,
    Form,
    InputNumber,
    DatePicker,
    message,
} from "antd";
import { PlusOutlined } from "@ant-design/icons";
import { useMemo, useState } from "react";
//import dayjs from "dayjs";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";
import {
    useGetTransactionsQuery,
    useCreateTransactionMutation,
} from "../../features/transactions/transactionsApi";

import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";

export function TransactionsPage() {
    const [search, setSearch] = useState("");
    const [type, setType] = useState<number | undefined>(undefined);
    const [accountId, setAccountId] = useState<string | undefined>(undefined);
    const [categoryId, setCategoryId] = useState<string | undefined>(undefined);

    const [isModalOpen, setIsModalOpen] = useState(false);

    const { data: accounts = [] } = useGetAccountsQuery({
        includeArchived: false,
    });

    const { data: categories = [] } = useGetCategoriesQuery();

    const { data: transactions = [], isLoading } = useGetTransactionsQuery({
        search: search || undefined,
        type,
        accountId,
        categoryId,
    });

    const [createTransaction, { isLoading: isCreating }] =
        useCreateTransactionMutation();

    const [form] = Form.useForm();

    const accountMap = useMemo(
        () => new Map(accounts.map((x) => [x.id, x.name])),
        [accounts]
    );

    const categoryMap = useMemo(
        () => new Map(categories.map((x) => [x.id, x.name])),
        [categories]
    );

    const openModal = () => {
        form.resetFields();
        setIsModalOpen(true);
    };

    const handleCreate = async () => {
        try {
            const values = await form.validateFields();

            await createTransaction({
                accountId: values.accountId,
                categoryId: values.categoryId || null,
                type: values.type,
                amount: values.amount,
                currencyCode: values.currencyCode,
                transactionDate: values.transactionDate.toISOString(),
                description: values.description,
                source: 1,
            }).unwrap();

            message.success("Transaction created");
            setIsModalOpen(false);
        } catch {
            message.error("Failed to create transaction");
        }
    };

    return (
        <div className="page-content">
            <Typography.Title level={2} style={{ margin: 0 }}>
                Transactions
            </Typography.Title>

            <Card>
                {/* FILTERS */}
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
                            { value: 3, label: "Transfer" },
                        ]}
                    />

                    <Select
                        placeholder="Account"
                        allowClear
                        style={{ width: 220 }}
                        value={accountId}
                        onChange={(value) => setAccountId(value)}
                        options={accounts.map((x) => ({
                            value: x.id,
                            label: `${x.name} (${x.currencyCode})`,
                        }))}
                    />

                    <Select
                        placeholder="Category"
                        allowClear
                        style={{ width: 220 }}
                        value={categoryId}
                        onChange={(value) => setCategoryId(value)}
                        options={categories.map((x) => ({
                            value: x.id,
                            label: x.name,
                        }))}
                    />

                    <Button type="primary" icon={<PlusOutlined />} onClick={openModal}>
                        New transaction
                    </Button>
                </Space>

                {/* TABLE */}
                <Table
                    loading={isLoading}
                    rowKey="id"
                    dataSource={transactions}
                    columns={[
                        {
                            title: "Date",
                            dataIndex: "transactionDate",
                            render: (value: string) => formatDate(value),
                        },
                        {
                            title: "Account",
                            dataIndex: "accountId",
                            render: (value: string) => accountMap.get(value) ?? value,
                        },
                        {
                            title: "Category",
                            dataIndex: "categoryId",
                            render: (value: string | null) =>
                                value ? categoryMap.get(value) ?? value : "-",
                        },
                        {
                            title: "Type",
                            dataIndex: "type",
                            render: (value: number) =>
                                value === 1
                                    ? "Income"
                                    : value === 2
                                        ? "Expense"
                                        : "Transfer",
                        },
                        {
                            title: "Amount",
                            render: (_, row) =>
                                formatMoney(row.amount, row.currencyCode),
                        },
                        {
                            title: "Description",
                            dataIndex: "description",
                        },
                    ]}
                />
            </Card>

            {/* MODAL */}
            <Modal
                title="New transaction"
                open={isModalOpen}
                onCancel={() => setIsModalOpen(false)}
                onOk={handleCreate}
                confirmLoading={isCreating}
            >
                <Form form={form} layout="vertical">
                    <Form.Item name="accountId" label="Account" rules={[{ required: true }]}>
                        <Select
                            options={accounts.map((x) => ({
                                value: x.id,
                                label: x.name,
                            }))}
                        />
                    </Form.Item>

                    <Form.Item name="categoryId" label="Category">
                        <Select
                            allowClear
                            options={categories.map((x) => ({
                                value: x.id,
                                label: x.name,
                            }))}
                        />
                    </Form.Item>

                    <Form.Item name="type" label="Type" rules={[{ required: true }]}>
                        <Select
                            options={[
                                { value: 1, label: "Income" },
                                { value: 2, label: "Expense" },
                                { value: 3, label: "Transfer" },
                            ]}
                        />
                    </Form.Item>

                    <Form.Item name="amount" label="Amount" rules={[{ required: true }]}>
                        <InputNumber style={{ width: "100%" }} />
                    </Form.Item>

                    <Form.Item
                        name="currencyCode"
                        label="Currency"
                        rules={[{ required: true }]}
                    >
                        <Select
                            options={[
                                { value: "USD", label: "USD" },
                                { value: "EUR", label: "EUR" },
                                { value: "RUB", label: "RUB" },
                                { value: "BYN", label: "BYN" },
                                { value: "JPY", label: "JPY" },
                                { value: "CNY", label: "CNY" },
                                { value: "GBP", label: "GBP" },
                            ]}
                        />
                    </Form.Item>

                    <Form.Item
                        name="transactionDate"
                        label="Date"
                        rules={[{ required: true }]}
                    >
                        <DatePicker style={{ width: "100%" }} />
                    </Form.Item>

                    <Form.Item name="description" label="Description">
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>
        </div>
    );
}