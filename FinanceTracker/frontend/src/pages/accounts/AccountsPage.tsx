import { Button, Card, Col, Empty, Form, Input, InputNumber, Modal, Row, Skeleton, Space, Statistic, Typography, message } from "antd";
import { useMemo, useState } from "react";

import {
  useArchiveAccountMutation,
  useCreateAccountMutation,
  useGetAccountsQuery,
  useSetBalanceMutation
} from "../../features/accounts/accountsApi";
import { formatMoney } from "../../shared/lib/formatMoney";
import { logUiEvent } from "../../shared/lib/logUiEvent";

interface CreateAccountForm {
  name: string;
  currencyCode: string;
  initialBalance: number;
}

interface SetBalanceForm {
  newBalance: number;
}

export function AccountsPage() {
  const [messageApi, contextHolder] = message.useMessage();
  const [createOpen, setCreateOpen] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [createForm] = Form.useForm<CreateAccountForm>();
  const [balanceForm] = Form.useForm<SetBalanceForm>();

  const { data: accounts = [], isLoading } = useGetAccountsQuery({ includeArchived: false });
  const [createAccount, { isLoading: isCreating }] = useCreateAccountMutation();
  const [setBalance, { isLoading: isSettingBalance }] = useSetBalanceMutation();
  const [archiveAccount] = useArchiveAccountMutation();

  const selected = useMemo(() => accounts.find((x) => x.id === selectedId) ?? null, [accounts, selectedId]);

  async function submitCreate(values: CreateAccountForm) {
    await createAccount(values).unwrap();
    messageApi.success("Account created.");
    setCreateOpen(false);
    createForm.resetFields();
  }

  async function submitSetBalance(values: SetBalanceForm) {
    if (!selectedId) {
      return;
    }

    await setBalance({ id: selectedId, body: values }).unwrap();
    logUiEvent({ name: "balance_updated", screen: "accounts", details: { accountId: selectedId, newBalance: values.newBalance } });
    messageApi.success("Balance updated.");
    setSelectedId(null);
    balanceForm.resetFields();
  }

  async function confirmArchive(id: string) {
    Modal.confirm({
      title: "Archive account?",
      content: "You can unarchive later in settings.",
      okText: "Archive",
      okButtonProps: { danger: true },
      onOk: async () => {
        await archiveAccount(id).unwrap();
        messageApi.success("Account archived.");
      }
    });
  }

  return (
    <div className="page-content">
      {contextHolder}

      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          Accounts
        </Typography.Title>
        <Space>
          <Button onClick={() => setCreateOpen(true)}>New account</Button>
        </Space>
      </div>

      {isLoading ? (
        <Skeleton active paragraph={{ rows: 8 }} />
      ) : accounts.length === 0 ? (
        <Card>
          <Empty description="No accounts yet" />
        </Card>
      ) : (
        <Row gutter={[16, 16]}>
          {accounts.map((account) => (
            <Col key={account.id} span={8}>
              <Card>
                <Space direction="vertical" style={{ width: "100%" }}>
                  <Typography.Text strong>{account.name}</Typography.Text>
                  <Statistic value={formatMoney(account.currentBalance, account.currencyCode)} />
                  <Typography.Text type="secondary">Currency: {account.currencyCode}</Typography.Text>
                  <Space>
                    <Button
                      onClick={() => {
                        setSelectedId(account.id);
                        balanceForm.setFieldValue("newBalance", account.currentBalance);
                      }}
                    >
                      Adjust balance
                    </Button>
                    <Button danger onClick={() => void confirmArchive(account.id)}>
                      Archive
                    </Button>
                  </Space>
                </Space>
              </Card>
            </Col>
          ))}
        </Row>
      )}

      <Modal
        title="Create account"
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        confirmLoading={isCreating}
        onOk={() => void createForm.submit()}
      >
        <Form form={createForm} layout="vertical" onFinish={(v) => void submitCreate(v)}>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}>
            <Input placeholder="Main account" />
          </Form.Item>
          <Form.Item name="currencyCode" label="Currency" rules={[{ required: true, len: 3 }]}>
            <Input placeholder="USD" maxLength={3} />
          </Form.Item>
          <Form.Item name="initialBalance" label="Initial balance" rules={[{ required: true }]}>
            <InputNumber style={{ width: "100%" }} precision={2} min={0} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="Manual balance adjustment"
        open={Boolean(selected)}
        onCancel={() => setSelectedId(null)}
        confirmLoading={isSettingBalance}
        onOk={() => void balanceForm.submit()}
      >
        <Typography.Paragraph type="secondary">Account: {selected?.name}</Typography.Paragraph>
        <Form form={balanceForm} layout="vertical" onFinish={(v) => void submitSetBalance(v)}>
          <Form.Item name="newBalance" label="New balance" rules={[{ required: true }]}>
            <InputNumber style={{ width: "100%" }} precision={2} min={0} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
