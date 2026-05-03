import { Card, Table, Typography } from "antd";

import { formatDate } from "../../shared/lib/formatDate";
import { formatMoney } from "../../shared/lib/formatMoney";
import { getTransferHistory } from "../../shared/lib/transferHistoryStorage";

export function TransferHistoryPage() {
  const data = getTransferHistory();

  return (
    <div className="page-content">
      <Typography.Title level={2} style={{ margin: 0 }}>
        Transfer history
      </Typography.Title>

      <Card>
        <Table
          rowKey="id"
          dataSource={data}
          columns={[
            {
              title: "Date",
              dataIndex: "createdAt",
              render: (value: string) => formatDate(value)
            },
            {
              title: "From",
              dataIndex: "fromAccountName"
            },
            {
              title: "To",
              dataIndex: "toAccountName"
            },
            {
              title: "Sent",
              render: (_, row) => formatMoney(row.amountSent, row.sourceCurrency)
            },
            {
              title: "Received",
              render: (_, row) => formatMoney(row.amountReceived, row.targetCurrency)
            },
            {
              title: "Rate",
              render: (_, row) => {
                if (row.manualRate && row.manualRate > 0) {
                  return `${row.manualRate} (manual)`;
                }

                return row.estimatedRate ?? "n/a";
              }
            },
            {
              title: "Description",
              dataIndex: "description"
            }
          ]}
        />
      </Card>
    </div>
  );
}
