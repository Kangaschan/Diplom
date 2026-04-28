import { Card, Empty, Progress, Skeleton, Space, Typography } from "antd";

import { useGetBudgetsUsageQuery } from "../../features/budgets/budgetsApi";

export function BudgetsPage() {
  const { data = [], isLoading } = useGetBudgetsUsageQuery();

  return (
    <div className="page-content">
      <Typography.Title level={2} style={{ margin: 0 }}>
        Budgets
      </Typography.Title>

      <Card>
        {isLoading ? (
          <Skeleton active paragraph={{ rows: 6 }} />
        ) : data.length === 0 ? (
          <Empty description="No budgets yet" />
        ) : (
          <Space direction="vertical" style={{ width: "100%" }}>
            {data.map((item, index) => {
              const percent = item.usagePercent ?? 0;
              return (
                <div key={item.budgetId ?? `${index}`}>
                  <Typography.Text strong>{item.title ?? `Budget ${index + 1}`}</Typography.Text>
                  <Progress percent={Math.max(0, Math.min(100, percent))} status={percent > 100 ? "exception" : "active"} />
                </div>
              );
            })}
          </Space>
        )}
      </Card>
    </div>
  );
}
