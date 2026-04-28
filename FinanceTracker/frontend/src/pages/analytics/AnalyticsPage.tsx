import { Card, Col, Row, Typography } from "antd";

export function AnalyticsPage() {
  return (
    <div className="page-content">
      <Typography.Title level={2} style={{ margin: 0 }}>
        Analytics
      </Typography.Title>

      <Row gutter={[16, 16]}>
        <Col span={12}>
          <Card title="Income / Expense over time">
            <Typography.Paragraph type="secondary">Connect chart component here.</Typography.Paragraph>
          </Card>
        </Col>
        <Col span={12}>
          <Card title="Categories structure">
            <Typography.Paragraph type="secondary">Connect chart component here.</Typography.Paragraph>
          </Card>
        </Col>
        <Col span={12}>
          <Card title="Accounts distribution">
            <Typography.Paragraph type="secondary">Connect chart component here.</Typography.Paragraph>
          </Card>
        </Col>
        <Col span={12}>
          <Card title="Budgets usage">
            <Typography.Paragraph type="secondary">Connect chart component here.</Typography.Paragraph>
          </Card>
        </Col>
      </Row>
    </div>
  );
}
