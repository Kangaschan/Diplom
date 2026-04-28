import { Alert, Button, Card, Form, Input, Space, Typography } from "antd";
import { Link, useNavigate } from "react-router-dom";
import { useState } from "react";

import { useRegisterMutation } from "../../features/auth/authApi";
import { clearAuth, setTokens } from "../../features/auth/authSlice";
import { api } from "../../shared/api/baseApi";
import { useAppDispatch } from "../../shared/lib/hooks";

interface RegisterForm {
  username: string;
  email: string;
  password: string;
  firstName?: string;
  lastName?: string;
}

export function RegisterPage() {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const [registerUser, { isLoading }] = useRegisterMutation();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  async function onFinish(values: RegisterForm) {
    setErrorMessage(null);
    try {
      const result = await registerUser(values).unwrap();
      dispatch(clearAuth());
      dispatch(api.util.resetApiState());
      dispatch(setTokens({ accessToken: result.accessToken, refreshToken: result.refreshToken }));
      navigate("/");
    } catch {
      setErrorMessage("Registration failed. Try another email or username.");
    }
  }

  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 20 }}>
      <Card style={{ width: 460 }}>
        <Space direction="vertical" style={{ width: "100%" }} size={18}>
          <Typography.Title level={3} style={{ margin: 0 }}>
            Create account
          </Typography.Title>

          {errorMessage && <Alert type="error" message={errorMessage} showIcon />}

          <Form layout="vertical" onFinish={onFinish}>
            <Form.Item name="username" label="Username" rules={[{ required: true }]}>
              <Input />
            </Form.Item>
            <Form.Item name="email" label="Email" rules={[{ required: true, type: "email" }]}>
              <Input />
            </Form.Item>
            <Form.Item name="password" label="Password" rules={[{ required: true, min: 4 }]}>
              <Input.Password />
            </Form.Item>
            <Form.Item name="firstName" label="First name">
              <Input />
            </Form.Item>
            <Form.Item name="lastName" label="Last name">
              <Input />
            </Form.Item>

            <Button block htmlType="submit" type="primary" loading={isLoading}>
              Register
            </Button>
          </Form>

          <Typography.Text>
            Already have an account? <Link to="/login">Login</Link>
          </Typography.Text>
        </Space>
      </Card>
    </div>
  );
}
