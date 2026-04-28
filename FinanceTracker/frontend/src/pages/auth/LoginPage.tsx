import { Alert, Button, Card, Form, Input, Space, Typography } from "antd";
import { Link, useNavigate } from "react-router-dom";
import { useState } from "react";

import { useLoginMutation } from "../../features/auth/authApi";
import { clearAuth, setTokens } from "../../features/auth/authSlice";
import { api } from "../../shared/api/baseApi";
import { useAppDispatch } from "../../shared/lib/hooks";
import { logUiEvent } from "../../shared/lib/logUiEvent";

interface LoginForm {
  email: string;
  password: string;
}

export function LoginPage() {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const [login, { isLoading }] = useLoginMutation();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  async function onFinish(values: LoginForm) {
    setErrorMessage(null);
    try {
      const result = await login(values).unwrap();
      dispatch(clearAuth());
      dispatch(api.util.resetApiState());
      dispatch(setTokens({ accessToken: result.accessToken, refreshToken: result.refreshToken }));
      logUiEvent({ name: "login_success", screen: "login" });
      navigate("/");
    } catch {
      setErrorMessage("Login failed. Check your credentials.");
      logUiEvent({ name: "login_failed", screen: "login" });
    }
  }

  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 20 }}>
      <Card style={{ width: 420 }}>
        <Space direction="vertical" style={{ width: "100%" }} size={18}>
          <Typography.Title level={3} style={{ margin: 0 }}>
            Sign in
          </Typography.Title>

          {errorMessage && <Alert type="error" message={errorMessage} showIcon />}

          <Form layout="vertical" onFinish={onFinish}>
            <Form.Item name="email" label="Email" rules={[{ required: true, type: "email", message: "Valid email is required" }]}>
              <Input placeholder="you@example.com" />
            </Form.Item>
            <Form.Item name="password" label="Password" rules={[{ required: true, message: "Password is required" }]}>
              <Input.Password placeholder="••••••••" />
            </Form.Item>

            <Button block htmlType="submit" type="primary" loading={isLoading}>
              Login
            </Button>
          </Form>

          <Typography.Text>
            No account? <Link to="/register">Create one</Link>
          </Typography.Text>
        </Space>
      </Card>
    </div>
  );
}
