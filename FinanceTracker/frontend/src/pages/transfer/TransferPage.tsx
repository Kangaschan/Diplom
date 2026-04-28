import { Alert, Button, Card, Col, Form, Input, InputNumber, Row, Select, Space, Typography, message } from "antd";
import { useEffect, useMemo, useState } from "react";

import { useGetAccountsQuery, useTransferMutation } from "../../features/accounts/accountsApi";
import { getExchangeRate } from "../../shared/lib/exchangeRate";
import { formatMoney } from "../../shared/lib/formatMoney";
import { logUiEvent } from "../../shared/lib/logUiEvent";
import { pushTransferHistory } from "../../shared/lib/transferHistoryStorage";

interface TransferForm {
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  description?: string;
}

export function TransferPage() {
  const [form] = Form.useForm<TransferForm>();
  const [messageApi, contextHolder] = message.useMessage();
  const [transfer, { isLoading }] = useTransferMutation();
  const { data: accounts = [], isLoading: accountsLoading } = useGetAccountsQuery({ includeArchived: false });

  const [rate, setRate] = useState<number | null>(1);
  const [rateLoading, setRateLoading] = useState(false);

  const values = Form.useWatch([], form) as TransferForm | undefined;

  const from = useMemo(() => accounts.find((a) => a.id === values?.fromAccountId), [accounts, values?.fromAccountId]);
  const to = useMemo(() => accounts.find((a) => a.id === values?.toAccountId), [accounts, values?.toAccountId]);

  useEffect(() => {
    let active = true;

    async function loadRate() {
      if (!from || !to) {
        setRate(1);
        return;
      }

      if (from.currencyCode === to.currencyCode) {
        setRate(1);
        return;
      }

      setRateLoading(true);
      try {
        const nextRate = await getExchangeRate(from.currencyCode, to.currencyCode);
        if (!active) {
          return;
        }

        setRate(nextRate);
      } finally {
        if (active) {
          setRateLoading(false);
        }
      }
    }

    void loadRate();
    return () => {
      active = false;
    };
  }, [from, to]);

  const estimatedReceive = useMemo(() => {
    const amount = values?.amount ?? 0;
    if (!rate) {
      return null;
    }

    return Number((amount * rate).toFixed(2));
  }, [values?.amount, rate]);

  async function onFinish(data: TransferForm) {
    if (!from || !to) {
      return;
    }

    const body = {
      fromAccountId: data.fromAccountId,
      toAccountId: data.toAccountId,
      amount: data.amount,
      currencyCode: from.currencyCode,
      description: data.description
    };

    await transfer(body).unwrap();

    pushTransferHistory({
      id: crypto.randomUUID(),
      createdAt: new Date().toISOString(),
      fromAccountName: from.name,
      toAccountName: to.name,
      amountSent: data.amount,
      amountReceived: estimatedReceive ?? data.amount,
      sourceCurrency: from.currencyCode,
      targetCurrency: to.currencyCode,
      estimatedRate: rate,
      description: data.description
    });

    logUiEvent({
      name: "transfer_created",
      screen: "transfer",
      details: {
        ...body,
        estimatedRate: rate,
        estimatedReceive
      }
    });

    messageApi.success("Transfer completed.");
    form.resetFields();
  }

  return (
    <div className="page-content">
      {contextHolder}

      <Typography.Title level={2} style={{ margin: 0 }}>
        Transfer with currency conversion
      </Typography.Title>

      <Row gutter={[16, 16]}>
        <Col span={14}>
          <Card loading={accountsLoading}>
            <Form form={form} layout="vertical" onFinish={(data) => void onFinish(data)}>
              <Form.Item name="fromAccountId" label="From account" rules={[{ required: true }]}>
                <Select
                  options={accounts.map((acc) => ({
                    label: `${acc.name} (${acc.currencyCode}) — ${formatMoney(acc.currentBalance, acc.currencyCode)}`,
                    value: acc.id
                  }))}
                />
              </Form.Item>

              <Form.Item name="toAccountId" label="To account" rules={[{ required: true }]}>
                <Select
                  options={accounts.map((acc) => ({
                    label: `${acc.name} (${acc.currencyCode}) — ${formatMoney(acc.currentBalance, acc.currencyCode)}`,
                    value: acc.id
                  }))}
                />
              </Form.Item>

              <Form.Item name="amount" label="Amount" rules={[{ required: true }]}>
                <InputNumber style={{ width: "100%" }} min={0.01} precision={2} />
              </Form.Item>

              <Form.Item name="description" label="Description">
                <Input placeholder="Optional transfer note" />
              </Form.Item>

              <Button type="primary" htmlType="submit" loading={isLoading}>
                Confirm transfer
              </Button>
            </Form>
          </Card>
        </Col>

        <Col span={10}>
          <Card title="Preview before confirm">
            <Space direction="vertical" style={{ width: "100%" }}>
              <Typography.Text>Source currency: {from?.currencyCode ?? "-"}</Typography.Text>
              <Typography.Text>Target currency: {to?.currencyCode ?? "-"}</Typography.Text>
              <Typography.Text>Estimated rate: {rateLoading ? "Loading..." : rate ?? "n/a"}</Typography.Text>
              <Typography.Text strong>
                Estimated receive amount: {estimatedReceive === null || !to ? "n/a" : formatMoney(estimatedReceive, to.currencyCode)}
              </Typography.Text>
              {!rate && from && to && (
                <Alert
                  type="warning"
                  showIcon
                  message="Rate preview unavailable"
                  description="Transfer still works: backend will apply the final conversion using cached rates."
                />
              )}
            </Space>
          </Card>
        </Col>
      </Row>
    </div>
  );
}
