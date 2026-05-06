import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  message,
  Progress,
  Row,
  Segmented,
  Skeleton,
  Space,
  Statistic,
  Tag,
  Typography
} from "antd"
import type { Dayjs } from "dayjs"
import dayjs from "dayjs"
import { useEffect, useMemo, useRef, useState } from "react"
import { useTranslation } from "react-i18next"
import { useSearchParams } from "react-router-dom"

import {
  useGetAccountsDistributionAnalyticsQuery,
  useGetBalanceHistoryAnalyticsQuery,
  useGetCashFlowAnalyticsQuery,
  useGetDashboardAnalyticsQuery,
  useGetExpensesByCategoryAnalyticsQuery,
  useGetPremiumComparisonAnalyticsQuery,
  useGetRecurringPaymentsAnalyticsQuery
} from "../../features/analytics/analyticsApi"
import { useGetAccountsQuery } from "../../features/accounts/accountsApi"
import { useGetBudgetsUsageQuery } from "../../features/budgets/budgetsApi"
import { useGetCategoriesQuery } from "../../features/categories/categoriesApi"
import { useGetProfileQuery } from "../../features/profile/profileApi"
import { useGetRecurringPaymentsQuery } from "../../features/recurring-payments/recurringPaymentsApi"
import { useGetTransactionsQuery } from "../../features/transactions/transactionsApi"
import { downloadAnalyticsPdf } from "../../shared/lib/downloadAnalyticsPdf"
import { formatDate } from "../../shared/lib/formatDate"
import { formatMoney } from "../../shared/lib/formatMoney"
import type {
  AnalyticsCategoryDto,
  BalanceHistoryPointDto,
  RecurringPaymentDto,
  TransactionDto
} from "../../shared/types/api"
import { TransactionSource, TransactionType } from "../../shared/types/api"

const { RangePicker } = DatePicker

type RecurringCalendarOccurrence = {
  dateKey: string
  date: Dayjs
  recurringPaymentId: string
  name: string
  type: TransactionType
  amount: number
  currencyCode: string
  accountName?: string | null
  frequency: string
}

type DailyExpenseSnapshot = {
  dateKey: string
  totalAmount: number
  currencyCode?: string | null
  count: number
  hasMixedCurrencies: boolean
}

const PIE_COLORS = ["#326586", "#13ae87", "#433bff", "#ff8a3d", "#d9485f", "#7e57c2", "#00a7c4", "#889b00"]

function formatDeltaPercent(current: number, previous: number) {
  if (previous === 0) {
    return current === 0 ? "0%" : "+100%"
  }

  const value = ((current - previous) / previous) * 100
  const sign = value > 0 ? "+" : ""
  return `${sign}${value.toFixed(1)}%`
}

function getBudgetProgressStatus(status: "normal" | "warning" | "exceeded") {
  if (status === "exceeded") {
    return "exception" as const
  }

  if (status === "warning") {
    return "active" as const
  }

  return "success" as const
}

function getTransactionSourceLabel(source: TransactionSource, t: (key: string, options?: Record<string, unknown>) => string) {
  switch (source) {
    case TransactionSource.Receipt:
      return t("analytics.transactionSource.receipt")
    case TransactionSource.Transfer:
      return t("analytics.transactionSource.transfer")
    case TransactionSource.Recurring:
      return t("analytics.transactionSource.recurring")
    default:
      return t("analytics.transactionSource.manual")
  }
}

function getTransactionTypeLabel(type: TransactionType, t: (key: string, options?: Record<string, unknown>) => string) {
  switch (type) {
    case TransactionType.Income:
      return t("analytics.transactionType.income")
    case TransactionType.Transfer:
      return t("analytics.transactionType.transfer")
    default:
      return t("analytics.transactionType.expense")
  }
}

function getTransactionTypeTagColor(type: TransactionType) {
  switch (type) {
    case TransactionType.Income:
      return "success"
    case TransactionType.Transfer:
      return "processing"
    default:
      return "error"
  }
}

function getCalendarShift(monthStart: Dayjs) {
  return (monthStart.day() + 6) % 7
}

function advanceOccurrence(date: Dayjs, frequency: string) {
  switch (frequency.toLowerCase()) {
    case "daily":
      return date.add(1, "day")
    case "weekly":
      return date.add(1, "week")
    case "yearly":
      return date.add(1, "year")
    default:
      return date.add(1, "month")
  }
}

function buildRecurringOccurrences(rules: RecurringPaymentDto[], month: Dayjs): RecurringCalendarOccurrence[] {
  const monthStart = month.startOf("month")
  const monthEnd = month.endOf("month")
  const occurrences: RecurringCalendarOccurrence[] = []

  for (const rule of rules) {
    if (!rule.isActive) {
      continue
    }

    const seed = rule.nextExecutionAt ?? rule.startDate
    if (!seed) {
      continue
    }

    let occurrence = dayjs(seed).startOf("day")
    const endDate = rule.endDate ? dayjs(rule.endDate).endOf("day") : null

    while (occurrence.endOf("day").valueOf() < monthStart.valueOf()) {
      occurrence = advanceOccurrence(occurrence, rule.frequency)
      if (endDate && occurrence.valueOf() > endDate.valueOf()) {
        break
      }
    }

    while (occurrence.startOf("day").valueOf() <= monthEnd.valueOf()) {
      if (endDate && occurrence.valueOf() > endDate.valueOf()) {
        break
      }

      occurrences.push({
        dateKey: occurrence.format("YYYY-MM-DD"),
        date: occurrence,
        recurringPaymentId: rule.id,
        name: rule.name,
        type: rule.type,
        amount: rule.amount,
        currencyCode: rule.currencyCode,
        accountName: rule.accountName,
        frequency: rule.frequency
      })

      occurrence = advanceOccurrence(occurrence, rule.frequency)
    }
  }

  return occurrences.sort((left, right) => left.date.valueOf() - right.date.valueOf() || left.name.localeCompare(right.name))
}

function DonutChart({
  categories,
  currencyCode
}: {
  categories: AnalyticsCategoryDto[]
  currencyCode: string
}) {
  const { t } = useTranslation()
  const radius = 56
  const circumference = 2 * Math.PI * radius
  const total = categories.reduce((sum, category) => sum + category.amount, 0)

  if (total <= 0) {
    return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.noExpenseData")} />
  }

  let offset = 0

  return (
    <div className="analytics-donut">
      <div className="analytics-donut__chart">
        <svg viewBox="0 0 160 160" className="analytics-donut__svg" aria-hidden="true">
          <circle cx="80" cy="80" r={radius} fill="none" stroke="rgba(128,128,128,0.12)" strokeWidth="18" />
          {categories.map((category, index) => {
            const percentage = category.amount / total
            const dash = circumference * percentage
            const segment = (
              <circle
                key={category.categoryId ?? category.categoryName}
                cx="80"
                cy="80"
                r={radius}
                fill="none"
                stroke={PIE_COLORS[index % PIE_COLORS.length]}
                strokeWidth="18"
                strokeLinecap="round"
                strokeDasharray={`${Math.max(dash - 3, 0)} ${circumference}`}
                strokeDashoffset={-offset}
                transform="rotate(-90 80 80)"
              />
            )

            offset += dash
            return segment
          })}
        </svg>

        <div className="analytics-donut__center">
          <Typography.Text type="secondary">{t("analytics.donutCenter")}</Typography.Text>
          <Typography.Text strong>{formatMoney(total, currencyCode)}</Typography.Text>
        </div>
      </div>

      <div className="analytics-donut__legend">
        {categories.map((category, index) => {
          const percentage = total === 0 ? 0 : (category.amount / total) * 100
          return (
            <div key={category.categoryId ?? category.categoryName} className="analytics-donut__legend-item">
              <div className="analytics-donut__legend-top">
                <Space size={8}>
                  <span
                    className="analytics-donut__swatch"
                    style={{ backgroundColor: PIE_COLORS[index % PIE_COLORS.length] }}
                  />
                  <Typography.Text strong>{category.categoryName}</Typography.Text>
                </Space>
                <Typography.Text>{percentage.toFixed(1)}%</Typography.Text>
              </div>
              <div className="analytics-donut__legend-bottom">
                <Typography.Text>{formatMoney(category.amount, category.currencyCode)}</Typography.Text>
                <Typography.Text type="secondary">{t("analytics.operationsShort", { count: category.transactionsCount })}</Typography.Text>
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

function BalanceLineChart({ points }: { points: BalanceHistoryPointDto[] }) {
  const { t } = useTranslation()
  if (points.length === 0) {
    return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.noBalanceHistory")} />
  }

  const width = 720
  const height = 260
  const padding = 28
  const values = points.map((point) => point.balance)
  const minValue = Math.min(...values)
  const maxValue = Math.max(...values)
  const ySpread = maxValue - minValue || 1
  const xStep = points.length > 1 ? (width - padding * 2) / (points.length - 1) : 0

  const coordinates = points.map((point, index) => {
    const x = padding + index * xStep
    const y = height - padding - ((point.balance - minValue) / ySpread) * (height - padding * 2)
    return { x, y, point }
  })

  const path = coordinates.map((coordinate, index) =>
    `${index === 0 ? "M" : "L"} ${coordinate.x.toFixed(2)} ${coordinate.y.toFixed(2)}`
  ).join(" ")

  const areaPath = `${path} L ${coordinates[coordinates.length - 1].x.toFixed(2)} ${(height - padding).toFixed(2)} L ${coordinates[0].x.toFixed(2)} ${(height - padding).toFixed(2)} Z`

  return (
    <div className="analytics-line-chart">
      <svg viewBox={`0 0 ${width} ${height}`} className="analytics-line-chart__svg" aria-hidden="true">
        {[0, 0.5, 1].map((mark) => {
          const y = padding + (height - padding * 2) * mark
          return (
            <line
              key={mark}
              x1={padding}
              y1={y}
              x2={width - padding}
              y2={y}
              stroke="rgba(128,128,128,0.18)"
              strokeDasharray="4 6"
            />
          )
        })}

        <path d={areaPath} className="analytics-line-chart__area" />
        <path d={path} className="analytics-line-chart__path" />

        {coordinates.map(({ x, y, point }) => (
          <g key={point.pointDate}>
            <circle cx={x} cy={y} r="4.5" className="analytics-line-chart__dot" />
          </g>
        ))}
      </svg>

      <div className="analytics-line-chart__axis">
        {points.map((point) => (
          <div key={point.pointDate} className="analytics-line-chart__axis-item">
            {point.label}
          </div>
        ))}
      </div>

      <div className="analytics-line-chart__summary">
        <div>
          <Typography.Text type="secondary">{t("analytics.start")}</Typography.Text>
          <div>
            <Typography.Text strong>{formatMoney(points[0].balance, points[0].currencyCode)}</Typography.Text>
          </div>
        </div>
        <div>
          <Typography.Text type="secondary">{t("analytics.end")}</Typography.Text>
          <div>
            <Typography.Text strong>
              {formatMoney(points[points.length - 1].balance, points[points.length - 1].currencyCode)}
            </Typography.Text>
          </div>
        </div>
      </div>
    </div>
  )
}

function RecurringCalendar({
  month,
  occurrences,
  dailyExpenses
}: {
  month: Dayjs
  occurrences: RecurringCalendarOccurrence[]
  dailyExpenses: Record<string, DailyExpenseSnapshot>
}) {
  const { t } = useTranslation()
  const monthStart = month.startOf("month")
  const monthEnd = month.endOf("month")
  const shift = getCalendarShift(monthStart)
  const daysInMonth = month.daysInMonth()
  const days = Array.from({ length: daysInMonth }, (_, index) => monthStart.add(index, "day"))
  const cells: Array<Dayjs | null> = [
    ...Array.from({ length: shift }, () => null),
    ...days
  ]

  while (cells.length % 7 !== 0) {
    cells.push(null)
  }

  const groupedOccurrences = occurrences.reduce<Record<string, RecurringCalendarOccurrence[]>>((accumulator, item) => {
    accumulator[item.dateKey] ??= []
    accumulator[item.dateKey].push(item)
    return accumulator
  }, {})

  return (
    <div className="analytics-calendar">
      <div className="analytics-calendar__weekdays">
        {["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"].map((weekday) => (
          <div key={weekday} className="analytics-calendar__weekday">
            {weekday}
          </div>
        ))}
      </div>

      <div className="analytics-calendar__grid">
        {cells.map((day, index) => {
          if (!day) {
            return <div key={`empty-${index}`} className="analytics-calendar__cell analytics-calendar__cell--empty" />
          }

          const isCurrentMonth = day.valueOf() >= monthStart.valueOf() && day.valueOf() <= monthEnd.valueOf()
          const items = groupedOccurrences[day.format("YYYY-MM-DD")] ?? []
          const expense = dailyExpenses[day.format("YYYY-MM-DD")]

          return (
            <div
              key={day.format("YYYY-MM-DD")}
              className={`analytics-calendar__cell${isCurrentMonth ? "" : " analytics-calendar__cell--muted"}`}
            >
              <div className="analytics-calendar__date">{day.date()}</div>

              <div className="analytics-calendar__events">
                {expense ? (
                  <div className="analytics-calendar__expense-summary">
                    <span className="analytics-calendar__expense-label">{t("analytics.spent")}</span>
                    <span className="analytics-calendar__expense-value">
                      {expense.hasMixedCurrencies
                        ? t("analytics.mixedCurrencies")
                        : formatMoney(expense.totalAmount, expense.currencyCode ?? "USD")}
                    </span>
                    <span className="analytics-calendar__expense-count">{t("analytics.operationsShort", { count: expense.count })}</span>
                  </div>
                ) : null}

                {items.slice(0, 3).map((item) => (
                  <div
                    key={`${item.recurringPaymentId}-${item.dateKey}`}
                    className={`analytics-calendar__event analytics-calendar__event--${
                      item.type === TransactionType.Income ? "income" : "expense"
                    }`}
                  >
                    <span className="analytics-calendar__event-name">{item.name}</span>
                    <span className="analytics-calendar__event-amount">
                      {formatMoney(item.amount, item.currencyCode)}
                    </span>
                  </div>
                ))}

                {items.length > 3 ? (
                  <Typography.Text type="secondary" className="analytics-calendar__more">
                    {t("analytics.moreItems", { count: items.length - 3 })}
                  </Typography.Text>
                ) : null}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

export function AnalyticsPage() {
  const { t } = useTranslation()
  const [searchParams] = useSearchParams()
  const isPrintMode = searchParams.get("print") === "1"
  const initialFrom = searchParams.get("from")
  const initialTo = searchParams.get("to")
  const initialGrouping = Number(searchParams.get("grouping") ?? "1")
  const initialMonth = searchParams.get("month")
  const printTriggeredRef = useRef(false)

  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [
    initialFrom ? dayjs(initialFrom) : dayjs().startOf("month"),
    initialTo ? dayjs(initialTo) : dayjs().endOf("day")
  ])
  const [grouping, setGrouping] = useState<number>(Number.isFinite(initialGrouping) && initialGrouping > 0 ? initialGrouping : 1)
  const [calendarMonth, setCalendarMonth] = useState<Dayjs>(() =>
    initialMonth ? dayjs(initialMonth).startOf("month") : dayjs().startOf("month")
  )

  const from = range[0].startOf("day").toISOString()
  const to = range[1].endOf("day").toISOString()

  const previousRange = useMemo(() => {
    const totalDays = range[1].startOf("day").diff(range[0].startOf("day"), "day") + 1
    const previousTo = range[0].subtract(1, "day").endOf("day")
    const previousFrom = previousTo.subtract(totalDays - 1, "day").startOf("day")

    return {
      previousFrom: previousFrom.toISOString(),
      previousTo: previousTo.toISOString(),
      currentFrom: from,
      currentTo: to
    }
  }, [from, range, to])

  const { data: profile } = useGetProfileQuery()
  const { data: dashboard, isLoading: dashboardLoading } = useGetDashboardAnalyticsQuery({ from, to })
  const { data: categories = [], isLoading: categoriesLoading } = useGetExpensesByCategoryAnalyticsQuery({ from, to })
  const { data: cashFlow = [], isLoading: cashFlowLoading } = useGetCashFlowAnalyticsQuery({ from, to, grouping })
  const { data: balanceHistory = [], isLoading: balanceHistoryLoading } = useGetBalanceHistoryAnalyticsQuery({
    from,
    to,
    grouping
  })
  const { data: accounts = [], isLoading: accountsLoading } = useGetAccountsDistributionAnalyticsQuery()
  const { data: recurringAnalytics, isLoading: recurringAnalyticsLoading } = useGetRecurringPaymentsAnalyticsQuery({ from, to })
  const { data: recurringRules = [], isLoading: recurringRulesLoading } = useGetRecurringPaymentsQuery()
  const { data: budgets = [], isLoading: budgetsLoading } = useGetBudgetsUsageQuery()
  const { data: transactions = [], isLoading: transactionsLoading } = useGetTransactionsQuery({ from, to })
  const { data: calendarTransactions = [], isLoading: calendarTransactionsLoading } = useGetTransactionsQuery({
    from: calendarMonth.startOf("month").startOf("day").toISOString(),
    to: calendarMonth.endOf("month").endOf("day").toISOString()
  })
  const { data: accountOptions = [] } = useGetAccountsQuery({ includeArchived: true })
  const { data: categoryOptions = [] } = useGetCategoriesQuery()
  const {
    data: premiumComparison,
    isLoading: premiumComparisonLoading,
    error: premiumComparisonError
  } = useGetPremiumComparisonAnalyticsQuery(previousRange, {
    skip: !profile?.hasActivePremium
  })

  const maxCashFlowValue = useMemo(
    () => cashFlow.reduce((max, item) => Math.max(max, item.income, item.expense, Math.abs(item.net)), 0),
    [cashFlow]
  )

  const topBudgets = useMemo(
    () =>
      [...budgets]
        .sort((left, right) => right.percentUsed - left.percentUsed)
        .slice(0, 5),
    [budgets]
  )

  const accountMap = useMemo(
    () => new Map(accountOptions.map((account) => [account.id, account.name])),
    [accountOptions]
  )

  const categoryMap = useMemo(
    () => new Map(categoryOptions.map((category) => [category.id, category.name])),
    [categoryOptions]
  )

  const paymentHistory = useMemo(
    () =>
      [...transactions]
        .sort((left, right) => dayjs(right.transactionDate).valueOf() - dayjs(left.transactionDate).valueOf())
        .slice(0, 12),
    [transactions]
  )

  const recurringOccurrences = useMemo(
    () => buildRecurringOccurrences(recurringRules, calendarMonth),
    [calendarMonth, recurringRules]
  )

  const calendarDailyExpenses = useMemo(() => {
    return calendarTransactions
      .filter((transaction) => transaction.type === TransactionType.Expense)
      .reduce<Record<string, DailyExpenseSnapshot>>((accumulator, transaction) => {
        const dateKey = dayjs(transaction.transactionDate).format("YYYY-MM-DD")
        const current = accumulator[dateKey]

        if (current) {
          if (current.currencyCode === transaction.currencyCode && !current.hasMixedCurrencies) {
            current.totalAmount += transaction.amount
          } else {
            current.hasMixedCurrencies = true
            current.currencyCode = null
          }

          current.count += 1
          return accumulator
        }

        accumulator[dateKey] = {
          dateKey,
          totalAmount: transaction.amount,
          currencyCode: transaction.currencyCode,
          count: 1,
          hasMixedCurrencies: false
        }

        return accumulator
      }, {})
  }, [calendarTransactions])

  const isPrintLoading =
    dashboardLoading ||
    categoriesLoading ||
    cashFlowLoading ||
    balanceHistoryLoading ||
    recurringRulesLoading ||
    calendarTransactionsLoading ||
    transactionsLoading

  useEffect(() => {
    if (!isPrintMode || isPrintLoading || printTriggeredRef.current) {
      return
    }

    printTriggeredRef.current = true
    window.setTimeout(() => {
      window.print()
    }, 300)
  }, [isPrintLoading, isPrintMode])

  return (
    <div className={`page-content${isPrintMode ? " analytics-print" : ""}`}>
      <div className="page-header">
        <Typography.Title level={2} style={{ margin: 0 }}>
          {isPrintMode ? t("analytics.reportTitle") : t("analytics.title")}
        </Typography.Title>

        {isPrintMode ? (
          <Typography.Text type="secondary">
            {formatDate(from)} - {formatDate(to)}
          </Typography.Text>
        ) : (
          <Button
            type="primary"
            disabled={!profile?.hasActivePremium}
            onClick={async () => {
              if (!profile?.hasActivePremium) {
                message.warning(t("analytics.exportPremiumOnly"))
                return
              }

              try {
                await downloadAnalyticsPdf({ from, to, grouping })
                message.success(t("analytics.exportSuccess"))
              } catch {
                message.error(t("analytics.exportFailed"))
              }
            }}
          >
            {t("analytics.exportPdf")}
          </Button>
        )}
      </div>

      {isPrintMode ? null : (
        <Card>
          <Space wrap size={16}>
            <RangePicker
              value={range}
              format="DD.MM.YYYY"
              onChange={(value) => {
                if (value?.[0] && value[1]) {
                  setRange([value[0], value[1]])
                }
              }}
            />

            <Segmented
              value={grouping}
              onChange={(value) => setGrouping(Number(value))}
              options={[
                { label: t("analytics.groupingDay"), value: 1 },
                { label: t("analytics.groupingWeek"), value: 2 },
                { label: t("analytics.groupingMonth"), value: 3 }
              ]}
            />
          </Space>
        </Card>
      )}

      {dashboardLoading ? (
        <Skeleton active paragraph={{ rows: 4 }} />
      ) : (
        <div className="stat-grid">
          <Card>
            <Statistic
              title={t("analytics.totalBalance")}
              value={formatMoney(dashboard?.totalBalance ?? 0, dashboard?.currencyCode ?? "USD")}
            />
          </Card>
          <Card>
            <Statistic
              title={t("analytics.income")}
              value={formatMoney(dashboard?.totalIncome ?? 0, dashboard?.currencyCode ?? "USD")}
            />
          </Card>
          <Card>
            <Statistic
              title={t("analytics.expenses")}
              value={formatMoney(dashboard?.totalExpense ?? 0, dashboard?.currencyCode ?? "USD")}
            />
          </Card>
          <Card>
            <Statistic
              title={t("analytics.net")}
              value={formatMoney(dashboard?.net ?? 0, dashboard?.currencyCode ?? "USD")}
            />
            <Typography.Text type="secondary">
              {t("analytics.transactionsCount", { count: dashboard?.transactionsCount ?? 0 })}
            </Typography.Text>
          </Card>
        </div>
      )}

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card title={t("analytics.balanceDynamics")}>
            {balanceHistoryLoading ? (
              <Skeleton active paragraph={{ rows: 8 }} />
            ) : (
              <BalanceLineChart points={balanceHistory} />
            )}
          </Card>
        </Col>

        <Col xs={24} xl={10}>
          <Card title={t("analytics.expensesByCategory")}>
            {categoriesLoading ? (
              <Skeleton active paragraph={{ rows: 8 }} />
            ) : (
              <DonutChart categories={categories} currencyCode={dashboard?.currencyCode ?? "USD"} />
            )}
          </Card>
        </Col>

        <Col span={24}>
          <Card
            title={t("analytics.recurringCalendar")}
            extra={
              <Space>
                <Button onClick={() => setCalendarMonth((value) => value.subtract(1, "month"))}>{t("analytics.calendarPrev")}</Button>
                <DatePicker
                  picker="month"
                  value={calendarMonth}
                  format="MMMM YYYY"
                  onChange={(value) => {
                    if (value) {
                      setCalendarMonth(value.startOf("month"))
                    }
                  }}
                />
                <Button onClick={() => setCalendarMonth((value) => value.add(1, "month"))}>{t("analytics.calendarNext")}</Button>
              </Space>
            }
          >
            {!profile?.hasActivePremium ? (
              <Alert
                type="info"
                showIcon
                message={t("analytics.calendarPremiumTitle")}
                description={t("analytics.calendarPremiumDescription")}
              />
            ) : recurringRulesLoading ? (
              <Skeleton active paragraph={{ rows: 10 }} />
            ) : calendarTransactionsLoading ? (
              <Skeleton active paragraph={{ rows: 10 }} />
            ) : recurringOccurrences.length === 0 ? (
              Object.keys(calendarDailyExpenses).length === 0 ? (
                <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.noRecurringOrExpenses")} />
              ) : (
                <RecurringCalendar
                  month={calendarMonth}
                  occurrences={recurringOccurrences}
                  dailyExpenses={calendarDailyExpenses}
                />
              )
            ) : (
              <RecurringCalendar
                month={calendarMonth}
                occurrences={recurringOccurrences}
                dailyExpenses={calendarDailyExpenses}
              />
            )}
          </Card>
        </Col>

        <Col xs={24} xl={14}>
          <Card title={t("analytics.paymentHistory")}>
            {transactionsLoading ? (
              <Skeleton active paragraph={{ rows: 8 }} />
            ) : paymentHistory.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.noPayments")} />
            ) : (
              <div className="analytics-history">
                {paymentHistory.map((transaction: TransactionDto) => (
                  <div key={transaction.id} className="analytics-history__item">
                    <div className="analytics-history__main">
                      <div className="analytics-history__title-row">
                        <Typography.Text strong>
                          {transaction.description?.trim() || getTransactionTypeLabel(transaction.type, t)}
                        </Typography.Text>
                        <Typography.Text strong>
                          {formatMoney(transaction.amount, transaction.currencyCode)}
                        </Typography.Text>
                      </div>

                      <div className="analytics-history__meta-row">
                        <Typography.Text type="secondary">
                          {formatDate(transaction.transactionDate)}
                        </Typography.Text>
                        <Typography.Text type="secondary">
                          {accountMap.get(transaction.accountId) ?? t("common.account")}
                        </Typography.Text>
                        {transaction.categoryId ? (
                          <Typography.Text type="secondary">
                            {categoryMap.get(transaction.categoryId) ?? t("common.category")}
                          </Typography.Text>
                        ) : null}
                      </div>
                    </div>

                    <div className="analytics-history__tags">
                      <Tag color={getTransactionTypeTagColor(transaction.type)}>
                        {getTransactionTypeLabel(transaction.type, t)}
                      </Tag>
                      <Tag>{getTransactionSourceLabel(transaction.source, t)}</Tag>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </Col>

        <Col xs={24} xl={10}>
          <Card title={t("analytics.cashFlow")}>
            {cashFlowLoading ? (
              <Skeleton active paragraph={{ rows: 6 }} />
            ) : cashFlow.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.noTransactions")} />
            ) : (
              <Space direction="vertical" size={14} style={{ width: "100%" }}>
                {cashFlow.map((point) => (
                  <Card key={point.periodStart} size="small">
                    <Space direction="vertical" style={{ width: "100%" }} size={8}>
                      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
                        <Typography.Text strong>{point.label}</Typography.Text>
                        <Typography.Text type="secondary">
                          {t("analytics.operationsShort", { count: point.transactionsCount })}
                        </Typography.Text>
                      </div>

                      <div style={{ display: "grid", gap: 4 }}>
                        <Typography.Text>
                          {t("analytics.cashFlowIncome", { value: formatMoney(point.income, point.currencyCode) })}
                        </Typography.Text>
                        <Typography.Text>
                          {t("analytics.cashFlowExpense", { value: formatMoney(point.expense, point.currencyCode) })}
                        </Typography.Text>
                        <Typography.Text strong>
                          {t("analytics.cashFlowNet", { value: formatMoney(point.net, point.currencyCode) })}
                        </Typography.Text>
                      </div>

                      <Progress
                        percent={
                          maxCashFlowValue === 0
                            ? 0
                            : Number(((Math.max(point.income, point.expense) / maxCashFlowValue) * 100).toFixed(2))
                        }
                        showInfo={false}
                        status={point.net >= 0 ? "success" : "exception"}
                      />
                    </Space>
                  </Card>
                ))}
              </Space>
            )}
          </Card>
        </Col>

        <Col xs={24} xl={12}>
          <Card title={t("analytics.accountsDistribution")}>
            {accountsLoading ? (
              <Skeleton active paragraph={{ rows: 5 }} />
            ) : accounts.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.noActiveAccounts")} />
            ) : (
              <Space direction="vertical" size={14} style={{ width: "100%" }}>
                {accounts.map((account) => (
                  <div key={account.accountId}>
                    <div style={{ display: "flex", justifyContent: "space-between", gap: 16, marginBottom: 6 }}>
                      <Typography.Text strong>{account.accountName}</Typography.Text>
                      <Typography.Text>{formatMoney(account.balance, account.currencyCode)}</Typography.Text>
                    </div>
                    <Progress percent={Number(account.sharePercent)} showInfo={false} />
                    <Typography.Text type="secondary">{account.sharePercent.toFixed(2)}%</Typography.Text>
                  </div>
                ))}
              </Space>
            )}
          </Card>
        </Col>

        <Col xs={24} xl={12}>
          <Card title={t("analytics.budgetsUsage")}>
            {budgetsLoading ? (
              <Skeleton active paragraph={{ rows: 5 }} />
            ) : topBudgets.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.noBudgets")} />
            ) : (
              <Space direction="vertical" size={14} style={{ width: "100%" }}>
                {topBudgets.map((budget) => (
                  <div key={budget.budgetId}>
                    <div style={{ display: "flex", justifyContent: "space-between", gap: 16, marginBottom: 6 }}>
                      <div>
                        <Typography.Text strong>{budget.categoryName}</Typography.Text>
                        <div>
                          <Typography.Text type="secondary">
                            {formatDate(budget.startDate)} - {formatDate(budget.endDate)}
                          </Typography.Text>
                        </div>
                      </div>
                      <Tag color={budget.status === "exceeded" ? "error" : budget.status === "warning" ? "warning" : "success"}>
                        {budget.status}
                      </Tag>
                    </div>
                    <Progress
                      percent={Number(Math.min(budget.percentUsed, 100))}
                      status={getBudgetProgressStatus(budget.status)}
                    />
                    <Typography.Text type="secondary">
                      {t("analytics.ofLimit", {
                        used: formatMoney(budget.usedAmount, budget.currencyCode),
                        limit: formatMoney(budget.limitAmount, budget.currencyCode)
                      })}
                    </Typography.Text>
                  </div>
                ))}
              </Space>
            )}
          </Card>
        </Col>

        <Col span={24}>
          <Card title={t("analytics.recurringAnalysis")}>
            {recurringAnalyticsLoading ? (
              <Skeleton active paragraph={{ rows: 4 }} />
            ) : !recurringAnalytics ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.recurringUnavailable")} />
            ) : (
              <Space direction="vertical" size={16} style={{ width: "100%" }}>
                <Row gutter={[16, 16]}>
                  <Col xs={24} md={6}>
                    <Card size="small">
                      <Statistic title={t("analytics.activeRules")} value={recurringAnalytics.activeRulesCount} />
                    </Card>
                  </Col>
                  <Col xs={24} md={6}>
                    <Card size="small">
                      <Statistic title={t("analytics.totalRules")} value={recurringAnalytics.totalRulesCount} />
                    </Card>
                  </Col>
                  <Col xs={24} md={6}>
                    <Card size="small">
                      <Statistic
                        title={t("analytics.generatedIncome")}
                        value={formatMoney(recurringAnalytics.generatedIncome, recurringAnalytics.currencyCode)}
                      />
                    </Card>
                  </Col>
                  <Col xs={24} md={6}>
                    <Card size="small">
                      <Statistic
                        title={t("analytics.generatedExpense")}
                        value={formatMoney(recurringAnalytics.generatedExpense, recurringAnalytics.currencyCode)}
                      />
                    </Card>
                  </Col>
                </Row>

                {recurringAnalytics.items.length === 0 ? (
                  <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("analytics.noRecurringRules")} />
                ) : (
                  <Space direction="vertical" size={14} style={{ width: "100%" }}>
                    {recurringAnalytics.items.map((item) => (
                      <Card key={item.recurringPaymentId} size="small">
                        <div style={{ display: "flex", justifyContent: "space-between", gap: 16, marginBottom: 8 }}>
                          <div>
                            <Typography.Text strong>{item.name}</Typography.Text>
                            <div>
                              <Typography.Text type="secondary">
                                {item.accountName ?? t("common.account")} · {item.frequency}
                              </Typography.Text>
                            </div>
                          </div>
                          <Space>
                            {item.type === TransactionType.Income ? <Tag color="success">{t("analytics.transactionType.income")}</Tag> : <Tag color="error">{t("analytics.transactionType.expense")}</Tag>}
                            <Tag color={item.isActive ? "success" : "default"}>
                              {item.isActive ? t("analytics.active") : t("analytics.inactive")}
                            </Tag>
                          </Space>
                        </div>

                        <Row gutter={[16, 8]}>
                          <Col xs={24} md={8}>
                            <Typography.Text>
                              {t("analytics.recurringRuleAmount", { value: formatMoney(item.ruleAmount, item.ruleCurrencyCode) })}
                            </Typography.Text>
                          </Col>
                          <Col xs={24} md={8}>
                            <Typography.Text>
                              {t("analytics.recurringCreated", { value: formatMoney(item.generatedAmount, item.currencyCode) })}
                            </Typography.Text>
                          </Col>
                          <Col xs={24} md={8}>
                            <Typography.Text>
                              {t("analytics.recurringExecutions", { count: item.executionsCount })}
                            </Typography.Text>
                          </Col>
                        </Row>

                        <Typography.Text type="secondary">
                          {t("analytics.recurringNextExecution", { value: item.nextExecutionAt ? formatDate(item.nextExecutionAt) : "-" })}
                        </Typography.Text>
                      </Card>
                    ))}
                  </Space>
                )}
              </Space>
            )}
          </Card>
        </Col>

        <Col span={24}>
          <Card title={t("analytics.comparison")}>
            {!profile?.hasActivePremium ? (
              <Alert
                type="info"
                showIcon
                message={t("analytics.comparisonPremiumTitle")}
                description={t("analytics.comparisonPremiumDescription")}
              />
            ) : premiumComparisonLoading ? (
              <Skeleton active paragraph={{ rows: 3 }} />
            ) : premiumComparisonError || !premiumComparison ? (
              <Alert
                type="warning"
                showIcon
                message={t("analytics.comparisonUnavailable")}
                description={t("analytics.comparisonUnavailableDescription")}
              />
            ) : (
              <Row gutter={[16, 16]}>
                <Col xs={24} md={12}>
                  <Card size="small" title={t("analytics.incomeComparison")}>
                    <Space direction="vertical" size={8}>
                      <Typography.Text>
                        {t("analytics.previous", { value: formatMoney(premiumComparison.previousIncome, premiumComparison.currencyCode) })}
                      </Typography.Text>
                      <Typography.Text>
                        {t("analytics.current", { value: formatMoney(premiumComparison.currentIncome, premiumComparison.currencyCode) })}
                      </Typography.Text>
                      <Typography.Text strong>
                        {t("analytics.change", { value: formatDeltaPercent(premiumComparison.currentIncome, premiumComparison.previousIncome) })}
                      </Typography.Text>
                    </Space>
                  </Card>
                </Col>
                <Col xs={24} md={12}>
                  <Card size="small" title={t("analytics.expenseComparison")}>
                    <Space direction="vertical" size={8}>
                      <Typography.Text>
                        {t("analytics.previous", { value: formatMoney(premiumComparison.previousExpense, premiumComparison.currencyCode) })}
                      </Typography.Text>
                      <Typography.Text>
                        {t("analytics.current", { value: formatMoney(premiumComparison.currentExpense, premiumComparison.currencyCode) })}
                      </Typography.Text>
                      <Typography.Text strong>
                        {t("analytics.change", { value: formatDeltaPercent(premiumComparison.currentExpense, premiumComparison.previousExpense) })}
                      </Typography.Text>
                    </Space>
                  </Card>
                </Col>
              </Row>
            )}
          </Card>
        </Col>
      </Row>
    </div>
  )
}

