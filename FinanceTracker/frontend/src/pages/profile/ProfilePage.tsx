import { Avatar, Button, Card, Col, Form, Input, Row, Space, Tag, Typography, message } from "antd";
import { UserOutlined } from "@ant-design/icons";
import { useEffect } from "react";

import { useGetProfileQuery, useUpdateProfileMutation } from "../../features/profile/profileApi";

interface ProfileForm {
  username?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
  avatarUrl?: string;
}

export function ProfilePage() {
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
    messageApi.success("Profile updated.");
  }

  return (
    <div className="page-content">
      {contextHolder}
      <Typography.Title level={2} style={{ margin: 0 }}>
        Profile
      </Typography.Title>

      <Row gutter={[16, 16]}>
        <Col span={8}>
          <Card loading={isLoading}>
            <Space direction="vertical" align="center" style={{ width: "100%" }}>
              <Avatar size={96} src={profile?.avatarUrl ?? undefined} icon={<UserOutlined />} />
              <Form form={form} layout="vertical" style={{ width: "100%" }}>
                <Form.Item name="avatarUrl" label="Avatar URL">
                  <Input placeholder="https://..." />
                </Form.Item>
              </Form>
              <Tag color={profile?.hasActivePremium ? "gold" : "blue"}>{profile?.hasActivePremium ? "Premium" : "Free"}</Tag>
            </Space>
          </Card>
        </Col>

        <Col span={16}>
          <Card title="Personal data" loading={isLoading}>
            <Form form={form} layout="vertical" onFinish={(values) => void onFinish(values)}>
              <Form.Item name="username" label="Username" rules={[{ required: true }]}>
                <Input />
              </Form.Item>
              <Form.Item name="email" label="Email" rules={[{ required: true }]}>
                <Input />
              </Form.Item>
              <Form.Item name="firstName" label="First name">
                <Input />
              </Form.Item>
              <Form.Item name="lastName" label="Last name">
                <Input />
              </Form.Item>
              <Button type="primary" htmlType="submit" loading={isUpdating}>
                Save
              </Button>
            </Form>
          </Card>
        </Col>
      </Row>
    </div>
  );
}
