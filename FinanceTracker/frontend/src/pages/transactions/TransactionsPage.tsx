import { Card, Input, Select, Space, Table, Typography } from "antd";
import { useMemo, useState } from "react";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";
import { useGetTransactionsQuery } from "../../features/transactions/transactionsApi";
import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";

export function TransactionsPage() {
  const [search, setSearch] = useState("");
  const [type, setType] = useState<number | undefined>(undefined);
  const [accountId, setAccountId] = useState<string | undefined>(undefined);
  const [categoryId, setCategoryId] = useState<string | undefined>(undefined);

  const { data: accounts = [] } = useGetAccountsQuery({ includeArchived: false });
  const { data: categories = [] } = useGetCategoriesQuery();
  const { data: transactions = [], isLoading } = useGetTransactionsQuery({
    search: search || undefined,
    type,
    accountId,
    categoryId
  });

  const accountMap = useMemo(() => new Map(accounts.map((x) => [x.id, x.name])), [accounts]);
  const categoryMap = useMemo(() => new Map(categories.map((x) => [x.id, x.name])), [categories]);

  return (
    <div className="page-content">
      <Typography.Title level={2} style={{ margin: 0 }}>
        Transactions
      </Typography.Title>

      <Card>
        <Space wrap style={{ marginBottom: 12 }}>
          <Input.Search placeholder="Search by description" style={{ width: 260 }} allowClear onSearch={setSearch} />
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
            options={accounts.map((x) => ({ value: x.id, label: `${x.name} (${x.currencyCode})` }))}
          />
          <Select
            placeholder="Category"
            allowClear
            style={{ width: 220 }}
            value={categoryId}
            onChange={(value) => setCategoryId(value)}
            options={categories.map((x) => ({ value: x.id, label: x.name }))}
          />
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
            { title: "Description", dataIndex: "description" }
          ]}
        />
      </Card>
    </div>
  );
}
