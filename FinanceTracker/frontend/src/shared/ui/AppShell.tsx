import {
  BellOutlined,
  UserOutlined,
  ReadOutlined
} from "@ant-design/icons";
import { Badge, Button, Drawer, Layout, Menu, Segmented, Skeleton, Space, Switch, Typography } from "antd";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { useAppDispatch, useAppSelector } from "../lib/hooks";
import { clearAuth } from "../../features/auth/authSlice";
import { setLanguage, toggleTheme } from "../../features/theme/uiSlice";
import { useGetNotificationsQuery, useGetUnreadCountQuery, useMarkAllReadMutation } from "../../features/notifications/notificationsApi";
import { formatDate } from "../lib/formatDate";
import { useMeQuery } from "../../features/auth/authApi";
import { api } from "../api/baseApi";

const { Header, Sider, Content } = Layout;

export function AppShell() {
  const location = useLocation();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const dispatch = useAppDispatch();
  const theme = useAppSelector((state) => state.ui.theme);
  const language = useAppSelector((state) => state.ui.language);
  const [notificationsOpen, setNotificationsOpen] = useState(false);

  const { data: me } = useMeQuery();
  const { data: unreadCount = 0, isLoading: unreadLoading } = useGetUnreadCountQuery();
  const { data: notifications = [], isLoading: notificationsLoading } = useGetNotificationsQuery({ unreadOnly: false });
  const [markAllRead] = useMarkAllReadMutation();

  const selectedKey = useMemo(() => {
    if (location.pathname.startsWith("/accounts")) return "/accounts";
    if (location.pathname.startsWith("/transfer/history")) return "/transfer/history";
    if (location.pathname.startsWith("/transfer")) return "/transfer";
    if (location.pathname.startsWith("/transactions")) return "/transactions";
    if (location.pathname.startsWith("/categories")) return "/categories";
    if (location.pathname.startsWith("/budgets")) return "/budgets";
    if (location.pathname.startsWith("/analytics")) return "/analytics";
    if (location.pathname.startsWith("/subscriptions")) return "/subscriptions";
    if (location.pathname.startsWith("/profile")) return "/profile";
    if (location.pathname.startsWith("/receipts")) return "/receipts";
    if (location.pathname.startsWith("/export")) return "/export";
    return "/";
  }, [location.pathname]);

  const headerBg = theme === "dark" ? "#042d22" : "#f4e9d4";
  const headerText = theme === "dark" ? "#e6ff55" : "#000000";
  const siderBg = theme === "dark" ? "#042d22" : "#f4e9d4";

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Sider width={250} theme={theme === "dark" ? "dark" : "light"} style={{ background: siderBg }}>
        <div style={{ padding: 20 }}>
          <Typography.Title level={4} style={{ margin: 0, color: theme === "dark" ? "#e6ff55" : "#000000" }}>
            FinanceTracker
          </Typography.Title>
        </div>
        <Menu
          className="ft-sidebar-menu"
          theme={theme === "dark" ? "dark" : "light"}
          style={{ background: siderBg }}
          mode="inline"
          selectedKeys={[selectedKey]}
          items={[
            { key: "/", label: <Link to="/">{t("nav.dashboard")}</Link> },
            { key: "/accounts", label: <Link to="/accounts">{t("nav.accounts")}</Link> },
            { key: "/transfer", label: <Link to="/transfer">{t("nav.transfer")}</Link> },
            { key: "/transfer/history", label: <Link to="/transfer/history">{t("nav.transferHistory")}</Link> },
            { key: "/transactions", label: <Link to="/transactions">{t("nav.transactions")}</Link> },
            { key: "/categories", label: <Link to="/categories">{t("nav.categories")}</Link> },
            { key: "/budgets", label: <Link to="/budgets">{t("nav.budgets")}</Link> },
            { key: "/analytics", label: <Link to="/analytics">{t("nav.analytics")}</Link> },
            { key: "/subscriptions", label: <Link to="/subscriptions">{t("nav.subscriptions")}</Link> },
            { key: "/profile", icon: <UserOutlined />, label: <Link to="/profile">{t("nav.profile")}</Link> },
            { key: "/receipts", icon: <ReadOutlined />, label: <Link to="/receipts">{t("nav.receipts")}</Link> },
            { key: "/export", label: <Link to="/export">{t("nav.export")}</Link> }
          ]}
        />
      </Sider>

      <Layout>
        <Header style={{ display: "flex", justifyContent: "space-between", alignItems: "center", paddingInline: 20, background: headerBg, borderBottom: theme === "dark" ? "1px solid #13ae87" : "1px solid #d9c8a7" }}>
          <Typography.Text strong style={{ color: headerText }}>
            {me?.username ? `Hello, ${me.username}` : "Personal Finance Hub"}
          </Typography.Text>

          <Space>
            <Segmented
              value={language}
              onChange={(value) => {
                const lang = value as "ru" | "en";
                dispatch(setLanguage(lang));
                void i18n.changeLanguage(lang);
              }}
              options={[
                { label: "RU", value: "ru" },
                { label: "EN", value: "en" }
              ]}
            />

            <Switch checked={theme === "dark"} onChange={() => dispatch(toggleTheme())} checkedChildren="Dark" unCheckedChildren="Light" />

            <Badge count={unreadLoading ? 0 : unreadCount}>
              <Button shape="circle" icon={<BellOutlined />} onClick={() => setNotificationsOpen(true)} />
            </Badge>

            <Button
              onClick={() => {
                dispatch(clearAuth());
                dispatch(api.util.resetApiState());
                navigate("/login");
              }}
            >
              Logout
            </Button>
          </Space>
        </Header>

        <Content style={{ padding: 20 }}>
          <Outlet />
        </Content>
      </Layout>

      <Drawer
        title="Notifications"
        open={notificationsOpen}
        onClose={() => setNotificationsOpen(false)}
        extra={<Button onClick={() => void markAllRead()}>Mark all read</Button>}
      >
        {notificationsLoading ? (
          <Skeleton active paragraph={{ rows: 5 }} />
        ) : (
          <Space direction="vertical" style={{ width: "100%" }}>
            {notifications.map((item) => (
              <div key={item.id} style={{ borderBottom: "1px solid #f0f0f0", paddingBottom: 10 }}>
                <Typography.Text strong>{item.title}</Typography.Text>
                <Typography.Paragraph style={{ marginBottom: 4 }}>{item.message}</Typography.Paragraph>
                <Typography.Text type="secondary">{formatDate(item.createdAt)}</Typography.Text>
              </div>
            ))}
            {notifications.length === 0 && <Typography.Text type="secondary">No notifications.</Typography.Text>}
          </Space>
        )}
      </Drawer>
    </Layout>
  );
}
