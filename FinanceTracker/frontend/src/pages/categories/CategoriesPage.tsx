import { Card, Empty, List, Skeleton, Tag, Typography } from "antd";

import { useGetCategoriesQuery } from "../../features/categories/categoriesApi";

export function CategoriesPage() {
  const { data = [], isLoading } = useGetCategoriesQuery();

  return (
    <div className="page-content">
      <Typography.Title level={2} style={{ margin: 0 }}>
        Categories
      </Typography.Title>

      <Card>
        {isLoading ? (
          <Skeleton active paragraph={{ rows: 6 }} />
        ) : data.length === 0 ? (
          <Empty description="No categories" />
        ) : (
          <List
            dataSource={data}
            renderItem={(item) => (
              <List.Item>
                <List.Item.Meta
                  title={item.name}
                  description={<Tag color={item.type === 1 ? "green" : "red"}>{item.type === 1 ? "Income" : "Expense"}</Tag>}
                />
              </List.Item>
            )}
          />
        )}
      </Card>
    </div>
  );
}
