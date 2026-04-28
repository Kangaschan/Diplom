import { CheckCircleOutlined, LockOutlined } from "@ant-design/icons";
import { Button, Card, Col, Row, Space, Typography } from "antd";

import { useGetCurrentSubscriptionQuery } from "../../features/subscriptions/subscriptionsApi";

export function SubscriptionsPage() {
  const { data: current } = useGetCurrentSubscriptionQuery();

  return (
    <div className="page-content">
      <Typography.Title level={2} style={{ margin: 0 }}>
        Subscriptions
      </Typography.Title>

      <Typography.Paragraph type="secondary">
        Current status: {current?.status ?? "unknown"} {current?.type ? `(${current.type})` : ""}
      </Typography.Paragraph>

      <Row gutter={[16, 16]}>
        <Col span={12}>
          <Card title="Free" extra={<CheckCircleOutlined style={{ color: "#52c41a" }} />}>
            <Space direction="vertical">
              <Typography.Text>Basic analytics</Typography.Text>
              <Typography.Text>Accounts and transactions</Typography.Text>
              <Typography.Text>Budgets and notifications</Typography.Text>
            </Space>
          </Card>
        </Col>

        <Col span={12}>
          <Card title="Premium" extra={<LockOutlined />}>
            <Space direction="vertical">
              <Typography.Text>Extended analytics</Typography.Text>
              <Typography.Text>Recurring payments analysis</Typography.Text>
              <Typography.Text>Credit load analysis</Typography.Text>
              <Button type="primary">Upgrade</Button>
            </Space>
          </Card>
        </Col>
      </Row>
    </div>
  );
}
