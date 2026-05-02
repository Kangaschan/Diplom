import { CheckCircleOutlined, CrownOutlined, LockOutlined } from "@ant-design/icons";
import { Button, Card, Col, Row, Space, Tag, Typography, message } from "antd";
import { useTranslation } from "react-i18next";

import { useMeQuery } from "../../features/auth/authApi";
import {
  type CurrentSubscriptionDto,
  useCreateCheckoutSessionMutation,
  useCreatePortalSessionMutation,
  useGetCurrentSubscriptionQuery,
  useGetSubscriptionHistoryQuery,
  useGetSubscriptionPlansQuery
} from "../../features/subscriptions/subscriptionsApi";
import { formatDate } from "../../shared/lib/formatDate";

function mapSubscriptionType(type?: number, hasActivePremium?: boolean) {
  if (type === 1 || hasActivePremium) {
    return "premium";
  }

  return "free";
}

function mapSubscriptionStatus(status?: number, hasActivePremium?: boolean) {
  if (status === 1 || hasActivePremium) {
    return "active";
  }

  if (status === 2) {
    return "expired";
  }

  if (status === 3) {
    return "cancelled";
  }

  return "inactive";
}

function resolveDisplayedSubscription(
  current?: CurrentSubscriptionDto | null,
  history?: CurrentSubscriptionDto[],
  hasActivePremium?: boolean
) {
  const source = current ?? history?.[0];

  return {
    typeKey: mapSubscriptionType(source?.type, hasActivePremium),
    statusKey: mapSubscriptionStatus(source?.status, hasActivePremium),
    endDate: source?.endDate ?? null
  };
}

export function SubscriptionsPage() {
  const { t } = useTranslation();
  const [messageApi, contextHolder] = message.useMessage();

  const { data: me } = useMeQuery();
  const { data: current } = useGetCurrentSubscriptionQuery();
  const { data: history = [] } = useGetSubscriptionHistoryQuery();
  const { data: plans = [], isLoading: plansLoading } = useGetSubscriptionPlansQuery();
  const [createCheckoutSession, { isLoading: checkoutLoading }] = useCreateCheckoutSessionMutation();
  const [createPortalSession, { isLoading: portalLoading }] = useCreatePortalSessionMutation();

  const subscription = resolveDisplayedSubscription(current, history, me?.hasActivePremium);
  const premiumPlan = plans.find((plan) => plan.name.toLowerCase() === "premium") ?? plans[0];
  const monthlyPrice = premiumPlan?.prices[0];
  const premiumActive = subscription.typeKey === "premium" && subscription.statusKey === "active";
  const planLabel = t(`subscription.${subscription.typeKey}`);
  const statusLabel = t(`subscription.${subscription.statusKey}`);

  async function handleUpgrade() {
    if (premiumActive) {
      messageApi.info(t("subscription.alreadyActive"));
      return;
    }

    if (!monthlyPrice?.id) {
      messageApi.error(t("subscription.checkoutUnavailable"));
      return;
    }

    try {
      const checkoutUrl = await createCheckoutSession({ priceId: monthlyPrice.id }).unwrap();
      window.location.href = checkoutUrl;
    } catch {
      messageApi.error(t("subscription.checkoutFailed"));
    }
  }

  async function handlePortal() {
    try {
      const portalUrl = await createPortalSession().unwrap();
      window.location.href = portalUrl;
    } catch {
      messageApi.error(t("subscription.portalFailed"));
    }
  }

  return (
    <div className="page-content">
      {contextHolder}

      <Space direction="vertical" size={16} style={{ width: "100%" }}>
        <div>
          <Typography.Title level={2} style={{ margin: 0 }}>
            {t("subscription.title")}
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t("subscription.subtitle")}
          </Typography.Paragraph>
        </div>

        <Card>
          <Space direction="vertical" size={10} style={{ width: "100%" }}>
            <Space size={12} wrap>
              <Typography.Text strong>{t("subscription.currentPlan")}</Typography.Text>
              <Tag className="ft-current-plan-badge">{planLabel}</Tag>
              <Typography.Text strong>{t("subscription.currentStatus")}</Typography.Text>
              <Tag className="ft-current-plan-badge">{statusLabel}</Tag>
            </Space>

            <Typography.Text>
              {t("subscription.summary", {
                plan: planLabel.toLowerCase(),
                status: statusLabel.toLowerCase()
              })}
            </Typography.Text>

            <Typography.Text type="secondary">
              {t("subscription.expiresAt")}:{" "}
              {subscription.endDate ? formatDate(subscription.endDate) : t("subscription.noExpiration")}
            </Typography.Text>
          </Space>
        </Card>

        <Row gutter={[16, 16]}>
          <Col xs={24} xl={12}>
            <Card title={t("subscription.freeCardTitle")} extra={<CheckCircleOutlined style={{ color: "#13ae87" }} />}>
              <Space direction="vertical">
                <Typography.Text>{t("subscription.freeFeature1")}</Typography.Text>
                <Typography.Text>{t("subscription.freeFeature2")}</Typography.Text>
                <Typography.Text>{t("subscription.freeFeature3")}</Typography.Text>
                {subscription.typeKey === "free" && <Tag className="ft-current-plan-badge">{t("subscription.currentPlanBadge")}</Tag>}
              </Space>
            </Card>
          </Col>

          <Col xs={24} xl={12}>
            <Card
              title={t("subscription.premiumCardTitle")}
              extra={premiumActive ? <CrownOutlined style={{ color: "#faad14" }} /> : <LockOutlined />}
            >
              <Space direction="vertical" style={{ width: "100%" }}>
                <Typography.Text>{t("subscription.premiumFeature1")}</Typography.Text>
                <Typography.Text>{t("subscription.premiumFeature2")}</Typography.Text>
                <Typography.Text>{t("subscription.premiumFeature3")}</Typography.Text>
                <Typography.Text>{t("subscription.premiumFeature4")}</Typography.Text>
                <Typography.Text type="secondary">
                  {monthlyPrice?.name ?? t("subscription.monthlyPlan")}
                  {monthlyPrice?.durationDays ? ` • ${monthlyPrice.durationDays} ${t("subscription.durationDays")}` : ""}
                </Typography.Text>
                {premiumActive && <Tag className="ft-current-plan-badge">{t("subscription.currentPlanBadge")}</Tag>}

                {premiumActive ? (
                  <Button type="default" onClick={() => void handlePortal()} loading={portalLoading}>
                    {t("subscription.manage")}
                  </Button>
                ) : (
                  <Button type="primary" onClick={() => void handleUpgrade()} loading={checkoutLoading || plansLoading}>
                    {t("subscription.upgrade")}
                  </Button>
                )}
              </Space>
            </Card>
          </Col>
        </Row>
      </Space>
    </div>
  );
}
