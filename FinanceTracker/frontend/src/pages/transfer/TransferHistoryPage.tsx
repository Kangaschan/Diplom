import { Card, Table, Typography } from "antd";
import { useTranslation } from "react-i18next";

import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";
import { getTransferHistory } from "../../shared/lib/transferHistoryStorage";

export function TransferHistoryPage() {
  const { t } = useTranslation();
  const data = getTransferHistory();

  return (
    <div className="page-content">
      <Typography.Title level={2} style={{ margin: 0 }}>
        {t("transferHistory.title")}
      </Typography.Title>

      <Card>
        <Table
          rowKey="id"
          dataSource={data}
          columns={[
            {
              title: t("transferHistory.date"),
              dataIndex: "createdAt",
              render: (value: string) => formatDate(value)
            },
            {
              title: t("transferHistory.from"),
              dataIndex: "fromAccountName"
            },
            {
              title: t("transferHistory.to"),
              dataIndex: "toAccountName"
            },
            {
              title: t("transferHistory.debited"),
              render: (_, row) => formatMoney(row.amountSent, row.sourceCurrency)
            },
            {
              title: t("transferHistory.credited"),
              render: (_, row) => formatMoney(row.amountReceived, row.targetCurrency)
            },
            {
              title: t("transferHistory.rate"),
              render: (_, row) => {
                if (row.manualRate && row.manualRate > 0) {
                  return `${row.manualRate} (${t("transferHistory.manualSuffix")})`;
                }

                return row.estimatedRate ?? t("common.notAvailable");
              }
            },
            {
              title: t("transferHistory.description"),
              dataIndex: "description"
            }
          ]}
        />
      </Card>
    </div>
  );
}
