import { ArrowDownOutlined, ArrowUpOutlined, BankOutlined, CalendarOutlined } from "@ant-design/icons";
import { Button, Card, Col, Empty, Row, Skeleton, Space, Statistic, Tag, Typography } from "antd";
import dayjs from "dayjs";
import { useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { useGetAccountsQuery } from "../../features/accounts/accountsApi";
import {
  useGetDashboardAnalyticsQuery,
  useGetExpensesByCategoryAnalyticsQuery
} from "../../features/analytics/analyticsApi";
import { useGetTransactionsQuery } from "../../features/transactions/transactionsApi";
import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";
import { TransactionSource, TransactionType } from "../../shared/types/api";

function getTransactionTypeColor(type: TransactionType) {
  switch (type) {
    case TransactionType.Income:
      return "success";
    case TransactionType.Transfer:
      return "processing";
    default:
      return "error";
  }
}

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const monthFrom = dayjs().startOf("month").toISOString();
  const monthTo = dayjs().endOf("day").toISOString();
  const recentFrom = dayjs().subtract(90, "day").startOf("day").toISOString();

  const { data: dashboard, isLoading: dashboardLoading } = useGetDashboardAnalyticsQuery({
    from: monthFrom,
    to: monthTo
  });
  const { data: accounts = [], isLoading: accountsLoading } = useGetAccountsQuery({ includeArchived: false });
  const { data: categories = [], isLoading: categoriesLoading } = useGetExpensesByCategoryAnalyticsQuery({
    from: monthFrom,
    to: monthTo
  });
  const { data: transactions = [], isLoading: transactionsLoading } = useGetTransactionsQuery({
    from: recentFrom,
    to: monthTo
  });

  const topAccounts = useMemo(
    () => [...accounts].sort((left, right) => right.currentBalance - left.currentBalance).slice(0, 3),
    [accounts]
  );

  const recentTransactions = useMemo(
    () =>
      [...transactions]
        .sort((left, right) => dayjs(right.transactionDate).valueOf() - dayjs(left.transactionDate).valueOf())
        .slice(0, 2),
    [transactions]
  );

  const leadingCategory = categories[0];
  const totalCategoriesAmount = categories.reduce((sum, category) => sum + category.amount, 0);
  const freeCash = (dashboard?.totalIncome ?? 0) - (dashboard?.totalExpense ?? 0);

  function getTransactionTypeLabel(type: TransactionType) {
    switch (type) {
      case TransactionType.Income:
        return t("dashboard.transactionType.income");
      case TransactionType.Transfer:
        return t("dashboard.transactionType.transfer");
      default:
        return t("dashboard.transactionType.expense");
    }
  }

  function getTransactionSourceLabel(source: TransactionSource) {
    switch (source) {
      case TransactionSource.Receipt:
        return t("dashboard.transactionSource.receipt");
      case TransactionSource.Transfer:
        return t("dashboard.transactionSource.transfer");
      case TransactionSource.Recurring:
        return t("dashboard.transactionSource.recurring");
      default:
        return t("dashboard.transactionSource.manual");
    }
  }

  return (
    <div className="page-content">
      <div className="page-header">
        <div>
          <Typography.Title level={2} style={{ margin: 0 }}>
            {t("dashboard.title")}
          </Typography.Title>
          <Typography.Text type="secondary">
            {t("dashboard.subtitle")}
          </Typography.Text>
        </div>
      </div>

      <Card className="dashboard-hero-card">
        <div className="dashboard-hero">
          <div className="dashboard-hero__content">
            <Typography.Text className="dashboard-hero__eyebrow">{t("dashboard.heroEyebrow")}</Typography.Text>
            <Typography.Title level={3} style={{ marginTop: 8, marginBottom: 8 }}>
              {dashboardLoading
                ? t("dashboard.heroLoading")
                : formatMoney(dashboard?.totalBalance ?? 0, dashboard?.currencyCode ?? "USD")}
            </Typography.Title>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              {dashboardLoading
                ? t("dashboard.heroDescriptionLoading")
                : t("dashboard.heroDescription", {
                    net: formatMoney(dashboard?.net ?? 0, dashboard?.currencyCode ?? "USD"),
                    trend: freeCash >= 0 ? t("dashboard.trendPositive") : t("dashboard.trendNegative")
                  })}
            </Typography.Paragraph>
          </div>

          <Space size={12} wrap>
            <Button type="primary" onClick={() => navigate("/transactions")}>
              {t("actions.addTransaction")}
            </Button>
            <Button onClick={() => navigate("/transfer")}>{t("actions.createTransfer")}</Button>
            <Button onClick={() => navigate("/analytics")}>{t("actions.openAnalytics")}</Button>
          </Space>
        </div>
      </Card>

      {dashboardLoading ? (
        <Skeleton active paragraph={{ rows: 4 }} />
      ) : (
        <div className="stat-grid">
          <Card>
            <Statistic
              title={t("dashboard.totalBalance")}
              value={formatMoney(dashboard?.totalBalance ?? 0, dashboard?.currencyCode ?? "USD")}
              prefix={<BankOutlined />}
            />
          </Card>
          <Card>
            <Statistic
              title={t("dashboard.totalIncome")}
              value={formatMoney(dashboard?.totalIncome ?? 0, dashboard?.currencyCode ?? "USD")}
              prefix={<ArrowUpOutlined />}
            />
          </Card>
          <Card>
            <Statistic
              title={t("dashboard.totalExpense")}
              value={formatMoney(dashboard?.totalExpense ?? 0, dashboard?.currencyCode ?? "USD")}
              prefix={<ArrowDownOutlined />}
            />
          </Card>
          <Card>
            <Statistic
              title={t("dashboard.transactionsCount")}
              value={dashboard?.transactionsCount ?? 0}
              prefix={<CalendarOutlined />}
            />
          </Card>
        </div>
      )}

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={10}>
          <Card title={t("dashboard.accountsTitle")}>
            {accountsLoading ? (
              <Skeleton active paragraph={{ rows: 5 }} />
            ) : topAccounts.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("dashboard.noAccounts")} />
            ) : (
              <div className="dashboard-list">
                {topAccounts.map((account) => (
                  <div key={account.id} className="dashboard-list__item">
                    <div>
                      <Typography.Text strong>{account.name}</Typography.Text>
                      <div>
                        <Typography.Text type="secondary">{account.currencyCode}</Typography.Text>
                      </div>
                    </div>
                    <Typography.Text strong>{formatMoney(account.currentBalance, account.currencyCode)}</Typography.Text>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </Col>

        <Col xs={24} xl={14}>
          <Card title={t("dashboard.monthFocus")}>
            {categoriesLoading ? (
              <Skeleton active paragraph={{ rows: 4 }} />
            ) : categories.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("dashboard.noExpenses")} />
            ) : (
              <div className="dashboard-focus">
                <div className="dashboard-focus__lead">
                  <Typography.Text type="secondary">{t("dashboard.leadingCategory")}</Typography.Text>
                  <Typography.Title level={4} style={{ marginTop: 6, marginBottom: 6 }}>
                    {leadingCategory?.categoryName ?? t("dashboard.uncategorized")}
                  </Typography.Title>
                  <Typography.Text strong>
                    {formatMoney(leadingCategory?.amount ?? 0, leadingCategory?.currencyCode ?? dashboard?.currencyCode ?? "USD")}
                  </Typography.Text>
                  <Typography.Paragraph type="secondary" style={{ marginTop: 10, marginBottom: 0 }}>
                    {t("dashboard.operationsCount", { count: leadingCategory?.transactionsCount ?? 0 })}{" "}
                    {t("dashboard.categoriesTotal", {
                      total: formatMoney(totalCategoriesAmount, dashboard?.currencyCode ?? "USD")
                    })}
                  </Typography.Paragraph>
                </div>

                <div className="dashboard-focus__side">
                  {categories.slice(0, 3).map((category) => (
                    <div key={category.categoryId ?? category.categoryName} className="dashboard-focus__category">
                      <div className="dashboard-focus__category-top">
                        <Typography.Text strong>{category.categoryName}</Typography.Text>
                        <Typography.Text>{formatMoney(category.amount, category.currencyCode)}</Typography.Text>
                      </div>
                      <Typography.Text type="secondary">
                        {t("dashboard.operationsCount", { count: category.transactionsCount })}
                      </Typography.Text>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </Card>
        </Col>

        <Col xs={24} xl={12}>
          <Card
            title={t("dashboard.lastTransactions")}
            extra={<Button type="link" onClick={() => navigate("/transactions")}>{t("common.allOperations")}</Button>}
          >
            {transactionsLoading ? (
              <Skeleton active paragraph={{ rows: 4 }} />
            ) : recentTransactions.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("dashboard.noTransactions")} />
            ) : (
              <div className="dashboard-list">
                {recentTransactions.map((transaction) => {
                  const accountName = accounts.find((account) => account.id === transaction.accountId)?.name ?? t("common.account");

                  return (
                    <div key={transaction.id} className="dashboard-transaction">
                      <div className="dashboard-transaction__main">
                        <div className="dashboard-transaction__top">
                          <Typography.Text strong>
                            {transaction.description?.trim() || getTransactionTypeLabel(transaction.type)}
                          </Typography.Text>
                          <Typography.Text strong>
                            {formatMoney(transaction.amount, transaction.currencyCode)}
                          </Typography.Text>
                        </div>
                        <Typography.Text type="secondary">
                          {formatDate(transaction.transactionDate)} · {accountName}
                        </Typography.Text>
                      </div>
                      <Space wrap>
                        <Tag color={getTransactionTypeColor(transaction.type)}>{getTransactionTypeLabel(transaction.type)}</Tag>
                        <Tag>{getTransactionSourceLabel(transaction.source)}</Tag>
                      </Space>
                    </div>
                  );
                })}
              </div>
            )}
          </Card>
        </Col>

        <Col xs={24} xl={12}>
          <Card title={t("dashboard.nextActions")}>
            <div className="dashboard-actions">
              <Button block onClick={() => navigate("/accounts")}>{t("dashboard.openAccounts")}</Button>
              <Button block onClick={() => navigate("/budgets")}>{t("dashboard.openBudgets")}</Button>
              <Button block onClick={() => navigate("/recurring-payments")}>{t("dashboard.openRecurring")}</Button>
              <Button block onClick={() => navigate("/subscriptions")}>{t("dashboard.openSubscriptions")}</Button>
            </div>
          </Card>
        </Col>
      </Row>
    </div>
  );
}
