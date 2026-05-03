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
  Transfer = 3
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
  currencyCode?: string;
}

export enum BudgetPeriodType {
  Monthly = 1,
  Weekly = 2,
  Custom = 3
}
