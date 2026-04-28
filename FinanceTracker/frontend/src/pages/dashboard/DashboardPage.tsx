import { Button, Card, Col, Progress, Row, Skeleton, Space, Statistic, Typography } from "antd";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import { useGetDashboardAnalyticsQuery } from "../../features/analytics/analyticsApi";
import { formatMoney } from "../../shared/lib/formatMoney";

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const { data: accounts = [], isLoading: accountsLoading } = useGetAccountsQuery({ includeArchived: false });
  const { data: dashboard, isLoading: dashboardLoading } = useGetDashboardAnalyticsQuery();

  const totalBalance = accounts.reduce((sum, acc) => sum + acc.currentBalance, 0);

  return (
    <div className="page-content">
      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          Dashboard
        </Typography.Title>
      </div>

      <Card>
        <Space size={12} wrap>
          <Typography.Text strong>{t("actions.quickActions")}</Typography.Text>
          <Button onClick={() => navigate("/transactions")}>{t("actions.addTransaction")}</Button>
          <Button type="primary" onClick={() => navigate("/transfer")}>{t("actions.createTransfer")}</Button>
          <Button onClick={() => navigate("/budgets")}>{t("actions.createBudget")}</Button>
        </Space>
      </Card>

      {accountsLoading || dashboardLoading ? (
        <Skeleton active paragraph={{ rows: 6 }} />
      ) : (
        <div className="stat-grid">
          <Card><Statistic title="Total balance" value={formatMoney(totalBalance)} /></Card>
          <Card><Statistic title="Income (period)" value={formatMoney(dashboard?.totalIncome ?? 0)} /></Card>
          <Card><Statistic title="Expenses (period)" value={formatMoney(dashboard?.totalExpense ?? 0)} /></Card>
          <Card><Statistic title="Net result" value={formatMoney(dashboard?.net ?? 0)} /></Card>
        </div>
      )}

      <Row gutter={[16, 16]}>
        <Col span={12}>
          <Card title="Income / Expense trend">
            <Typography.Paragraph type="secondary">Chart placeholder (connect line chart in next step).</Typography.Paragraph>
            <Progress percent={68} status="active" />
          </Card>
        </Col>
        <Col span={12}>
          <Card title="Categories distribution">
            <Typography.Paragraph type="secondary">Chart placeholder (connect doughnut chart in next step).</Typography.Paragraph>
            <Progress percent={52} status="active" />
          </Card>
        </Col>
        <Col span={12}>
          <Card title="Accounts snapshot">
            <Typography.Paragraph type="secondary">Chart placeholder (connect bar chart in next step).</Typography.Paragraph>
            <Progress percent={76} status="active" />
          </Card>
        </Col>
        <Col span={12}>
          <Card title="Budgets usage">
            <Typography.Paragraph type="secondary">Chart placeholder (connect progress chart in next step).</Typography.Paragraph>
            <Progress percent={59} status="active" />
          </Card>
        </Col>
      </Row>
    </div>
  );
}
