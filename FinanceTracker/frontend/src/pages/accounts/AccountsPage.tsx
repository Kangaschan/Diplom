import { Button, Card, Col, Empty, Form, Input, InputNumber, Modal, Row, Skeleton, Space, Statistic, Typography, message } from "antd";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

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
  const { t } = useTranslation();
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
    messageApi.success(t("accounts.createSuccess"));
    setCreateOpen(false);
    createForm.resetFields();
  }

  async function submitSetBalance(values: SetBalanceForm) {
    if (!selectedId) {
      return;
    }

    await setBalance({ id: selectedId, body: values }).unwrap();
    logUiEvent({ name: "balance_updated", screen: "accounts", details: { accountId: selectedId, newBalance: values.newBalance } });
    messageApi.success(t("accounts.balanceUpdated"));
    setSelectedId(null);
    balanceForm.resetFields();
  }

  function confirmArchive(id: string) {
    Modal.confirm({
      title: t("accounts.archiveTitle"),
      content: t("accounts.archiveContent"),
      okText: t("accounts.archiveConfirm"),
      cancelText: t("common.cancel"),
      okButtonProps: { danger: true },
      onOk: async () => {
        await archiveAccount(id).unwrap();
        messageApi.success(t("accounts.archiveSuccess"));
      }
    });
  }

  return (
    <div className="page-content">
      {contextHolder}

      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          {t("accounts.title")}
        </Typography.Title>
        <Space>
          <Button onClick={() => setCreateOpen(true)}>{t("accounts.newAccount")}</Button>
        </Space>
      </div>

      {isLoading ? (
        <Skeleton active paragraph={{ rows: 8 }} />
      ) : accounts.length === 0 ? (
        <Card>
          <Empty description={t("accounts.noAccounts")} />
        </Card>
      ) : (
        <Row gutter={[16, 16]}>
          {accounts.map((account) => (
            <Col key={account.id} span={8}>
              <Card>
                <Space direction="vertical" style={{ width: "100%" }}>
                  <Typography.Text strong>{account.name}</Typography.Text>
                  <Statistic value={formatMoney(account.currentBalance, account.currencyCode)} />
                  <Typography.Text type="secondary">
                    {t("accounts.currency")}: {account.currencyCode}
                  </Typography.Text>
                  <Space>
                    <Button
                      onClick={() => {
                        setSelectedId(account.id);
                        balanceForm.setFieldValue("newBalance", account.currentBalance);
                      }}
                    >
                      {t("accounts.changeBalance")}
                    </Button>
                    <Button danger onClick={() => void confirmArchive(account.id)}>
                      {t("accounts.archive")}
                    </Button>
                  </Space>
                </Space>
              </Card>
            </Col>
          ))}
        </Row>
      )}

      <Modal
        title={t("accounts.createModalTitle")}
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        confirmLoading={isCreating}
        onOk={() => void createForm.submit()}
        okText={t("common.create")}
        cancelText={t("common.cancel")}
      >
        <Form form={createForm} layout="vertical" onFinish={(v) => void submitCreate(v)}>
          <Form.Item name="name" label={t("accounts.name")} rules={[{ required: true }]}>
            <Input placeholder={t("accounts.namePlaceholder")} />
          </Form.Item>
          <Form.Item name="currencyCode" label={t("common.currency")} rules={[{ required: true, len: 3 }]}>
            <Input placeholder="USD" maxLength={3} />
          </Form.Item>
          <Form.Item name="initialBalance" label={t("accounts.initialBalance")} rules={[{ required: true }]}>
            <InputNumber style={{ width: "100%" }} precision={2} min={0} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t("accounts.adjustBalanceTitle")}
        open={Boolean(selected)}
        onCancel={() => setSelectedId(null)}
        confirmLoading={isSettingBalance}
        onOk={() => void balanceForm.submit()}
        okText={t("common.save")}
        cancelText={t("common.cancel")}
      >
        <Typography.Paragraph type="secondary">
          {t("accounts.selectedAccount")}: {selected?.name}
        </Typography.Paragraph>
        <Form form={balanceForm} layout="vertical" onFinish={(v) => void submitSetBalance(v)}>
          <Form.Item name="newBalance" label={t("accounts.newBalance")} rules={[{ required: true }]}>
            <InputNumber style={{ width: "100%" }} precision={2} min={0} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
