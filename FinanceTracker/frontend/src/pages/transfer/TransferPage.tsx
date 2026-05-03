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
  manualRate?: number;
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

  const from = useMemo(() => accounts.find((account) => account.id === values?.fromAccountId), [accounts, values?.fromAccountId]);
  const to = useMemo(() => accounts.find((account) => account.id === values?.toAccountId), [accounts, values?.toAccountId]);

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

  const effectiveRate = useMemo(() => {
    if (!from || !to) {
      return null;
    }

    if (from.currencyCode === to.currencyCode) {
      return 1;
    }

    if (values?.manualRate && values.manualRate > 0) {
      return values.manualRate;
    }

    return rate;
  }, [from, to, rate, values?.manualRate]);

  const estimatedReceive = useMemo(() => {
    const amount = values?.amount ?? 0;
    if (!effectiveRate) {
      return null;
    }

    return Number((amount * effectiveRate).toFixed(2));
  }, [values?.amount, effectiveRate]);

  async function onFinish(data: TransferForm) {
    if (!from || !to) {
      return;
    }

    const body = {
      fromAccountId: data.fromAccountId,
      toAccountId: data.toAccountId,
      amount: data.amount,
      currencyCode: from.currencyCode,
      manualRate: data.manualRate && data.manualRate > 0 ? data.manualRate : null,
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
      estimatedRate: effectiveRate,
      manualRate: body.manualRate,
      description: data.description
    });

    logUiEvent({
      name: "transfer_created",
      screen: "transfer",
      details: {
        ...body,
        effectiveRate,
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
                  options={accounts.map((account) => ({
                    label: `${account.name} (${account.currencyCode}) - ${formatMoney(account.currentBalance, account.currencyCode)}`,
                    value: account.id
                  }))}
                />
              </Form.Item>

              <Form.Item name="toAccountId" label="To account" rules={[{ required: true }]}>
                <Select
                  options={accounts.map((account) => ({
                    label: `${account.name} (${account.currencyCode}) - ${formatMoney(account.currentBalance, account.currencyCode)}`,
                    value: account.id
                  }))}
                />
              </Form.Item>

              <Form.Item name="amount" label="Amount" rules={[{ required: true }]}>
                <InputNumber style={{ width: "100%" }} min={0.01} precision={2} />
              </Form.Item>

              <Form.Item
                name="manualRate"
                label="Manual exchange rate"
                extra="Optional. Leave empty to use the current exchange rate automatically."
                rules={[
                  {
                    validator(_, value: number | undefined) {
                      if (value === undefined || value === null || value > 0) {
                        return Promise.resolve();
                      }

                      return Promise.reject(new Error("Manual rate must be greater than zero"));
                    }
                  }
                ]}
              >
                <InputNumber style={{ width: "100%" }} min={0.000001} precision={6} placeholder="For example: 4" />
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
              <Typography.Text>
                Auto rate: {rateLoading ? "Loading..." : rate ?? "n/a"}
              </Typography.Text>
              <Typography.Text>
                Applied rate: {effectiveRate ?? "n/a"}
              </Typography.Text>
              <Typography.Text type="secondary">
                {values?.manualRate && values.manualRate > 0
                  ? "Manual rate is enabled for this transfer."
                  : "Current exchange rate will be used automatically."}
              </Typography.Text>
              <Typography.Text strong>
                Estimated receive amount: {estimatedReceive === null || !to ? "n/a" : formatMoney(estimatedReceive, to.currencyCode)}
              </Typography.Text>

              {!rate && from && to && !values?.manualRate && (
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
