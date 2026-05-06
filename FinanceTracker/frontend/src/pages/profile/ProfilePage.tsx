import { UserOutlined } from "@ant-design/icons";
import { Avatar, Button, Card, Col, Form, Input, Row, Space, Tag, Typography, message } from "antd";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import { useGetProfileQuery, useUpdateProfileMutation } from "../../features/profile/profileApi";

interface ProfileForm {
  username?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
  avatarUrl?: string;
}

export function ProfilePage() {
  const { t } = useTranslation();
  const [form] = Form.useForm<ProfileForm>();
  const [messageApi, contextHolder] = message.useMessage();

  const { data: profile, isLoading } = useGetProfileQuery();
  const [updateProfile, { isLoading: isUpdating }] = useUpdateProfileMutation();

  useEffect(() => {
    if (!profile) {
      return;
    }

    form.setFieldsValue({
      username: profile.username,
      email: profile.email,
      firstName: profile.firstName ?? undefined,
      lastName: profile.lastName ?? undefined,
      avatarUrl: profile.avatarUrl ?? undefined
    });
  }, [form, profile]);

  async function onFinish(values: ProfileForm) {
    await updateProfile(values).unwrap();
    messageApi.success(t("profile.updated"));
  }

  return (
    <div className="page-content">
      {contextHolder}
      <Typography.Title level={2} style={{ margin: 0 }}>
        {t("profile.title")}
      </Typography.Title>

      <Row gutter={[16, 16]}>
        <Col span={8}>
          <Card loading={isLoading}>
            <Space direction="vertical" align="center" style={{ width: "100%" }}>
              <Avatar size={96} src={profile?.avatarUrl ?? undefined} icon={<UserOutlined />} />
              <Form form={form} layout="vertical" style={{ width: "100%" }}>
                <Form.Item name="avatarUrl" label={t("profile.avatarUrl")}>
                  <Input placeholder="https://..." />
                </Form.Item>
              </Form>
              <Tag color={profile?.hasActivePremium ? "gold" : "blue"}>
                {profile?.hasActivePremium ? "Premium" : t("profile.free")}
              </Tag>
            </Space>
          </Card>
        </Col>

        <Col span={16}>
          <Card title={t("profile.personalData")} loading={isLoading}>
            <Form form={form} layout="vertical" onFinish={(values) => void onFinish(values)}>
              <Form.Item name="username" label={t("profile.username")} rules={[{ required: true }]}>
                <Input />
              </Form.Item>
              <Form.Item name="email" label={t("profile.email")} rules={[{ required: true }]}>
                <Input />
              </Form.Item>
              <Form.Item name="firstName" label={t("profile.firstName")}>
                <Input />
              </Form.Item>
              <Form.Item name="lastName" label={t("profile.lastName")}>
                <Input />
              </Form.Item>
              <Button type="primary" htmlType="submit" loading={isUpdating}>
                {t("profile.save")}
              </Button>
            </Form>
          </Card>
        </Col>
      </Row>
    </div>
  );
}
