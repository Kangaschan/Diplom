export interface ApiErrorResponse {
  code: string;
  message: string;
}

export enum TransactionType {
  Income = 1,
  Expense = 2,
  Transfer = 3
}

export enum TransactionSource {
  Manual = 1,
  Receipt = 2,
  Transfer = 3,
  Recurring = 4
}

export interface AccountDto {
  id: string;
  name: string;
  currencyCode: string;
  currentBalance: number;
  financialGoalAmount?: number | null;
  financialGoalDeadline?: string | null;
  isArchived: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface TransactionDto {
  id: string;
  accountId: string;
  categoryId?: string | null;
  type: TransactionType;
  amount: number;
  currencyCode: string;
  transactionDate: string;
  description?: string | null;
  source: TransactionSource;
  status?: number;
  createdAt?: string;
}

export interface CategoryDto {
  id: string;
  name: string;
  type: number;
  isActive?: boolean;
}

export interface CategoryExpenseStatsDto {
  categoryId: string;
  categoryName: string;
  amount: number;
  transactionsCount: number;
  currencyCode: string;
  from: string;
  to: string;
}

export interface NotificationDto {
  id: string;
  type?: string;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
}

export interface ProfileDto {
  id: string;
  username: string;
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  avatarUrl?: string | null;
  hasActivePremium: boolean;
}

export interface DashboardDto {
  totalBalance?: number;
  totalIncome?: number;
  totalExpense?: number;
  net?: number;
  transactionsCount?: number;
  currencyCode?: string;
}

export interface AnalyticsCategoryDto {
  categoryId?: string | null;
  categoryName: string;
  amount: number;
  transactionsCount: number;
  currencyCode: string;
}

export interface CashFlowPointDto {
  periodStart: string;
  label: string;
  income: number;
  expense: number;
  net: number;
  transactionsCount: number;
  currencyCode: string;
}

export interface BalanceHistoryPointDto {
  pointDate: string;
  label: string;
  balance: number;
  currencyCode: string;
}

export interface AccountDistributionDto {
  accountId: string;
  accountName: string;
  balance: number;
  currencyCode: string;
  sharePercent: number;
}

export interface PremiumComparisonDto {
  previousIncome: number;
  currentIncome: number;
  previousExpense: number;
  currentExpense: number;
  currencyCode: string;
}

export interface RecurringPaymentDto {
  id: string;
  name: string;
  description?: string | null;
  accountId?: string | null;
  accountName?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  type: TransactionType;
  amount: number;
  currencyCode: string;
  frequency: string;
  startDate?: string | null;
  nextExecutionAt?: string | null;
  endDate?: string | null;
  lastExecutedAt?: string | null;
  isActive: boolean;
}

export interface RecurringPaymentAnalyticsItemDto {
  recurringPaymentId: string;
  name: string;
  type: TransactionType;
  frequency: string;
  isActive: boolean;
  nextExecutionAt?: string | null;
  ruleAmount: number;
  ruleCurrencyCode: string;
  generatedAmount: number;
  executionsCount: number;
  currencyCode: string;
  accountName?: string | null;
}

export interface RecurringPaymentsAnalyticsDto {
  activeRulesCount: number;
  totalRulesCount: number;
  generatedTransactionsCount: number;
  generatedIncome: number;
  generatedExpense: number;
  currencyCode: string;
  items: RecurringPaymentAnalyticsItemDto[];
}

export enum BudgetPeriodType {
  Monthly = 1,
  Weekly = 2,
  Custom = 3
}
