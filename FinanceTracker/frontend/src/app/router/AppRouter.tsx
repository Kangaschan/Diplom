import { Navigate, Route, Routes } from "react-router-dom";

import { AppShell } from "../../shared/ui/AppShell";
import { PrivateRoute } from "./PrivateRoute";
import { PublicOnlyRoute } from "./PublicOnlyRoute";
import { LoginPage } from "../../pages/auth/LoginPage";
import { RegisterPage } from "../../pages/auth/RegisterPage";
import { DashboardPage } from "../../pages/dashboard/DashboardPage";
import { AccountsPage } from "../../pages/accounts/AccountsPage";
import { TransferPage } from "../../pages/transfer/TransferPage";
import { TransferHistoryPage } from "../../pages/transfer/TransferHistoryPage";
import { TransactionsPage } from "../../pages/transactions/TransactionsPage";
import { CategoriesPage } from "../../pages/categories/CategoriesPage";
import { BudgetsPage } from "../../pages/budgets/BudgetsPage";
import { AnalyticsPage } from "../../pages/analytics/AnalyticsPage";
import { SubscriptionsPage } from "../../pages/subscriptions/SubscriptionsPage";
import { ProfilePage } from "../../pages/profile/ProfilePage";
import { PlaceholderPage } from "../../pages/placeholders/PlaceholderPage";

export function AppRouter() {
  return (
    <Routes>
      <Route element={<PublicOnlyRoute />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>

      <Route element={<PrivateRoute />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/accounts" element={<AccountsPage />} />
          <Route path="/transfer" element={<TransferPage />} />
          <Route path="/transfer/history" element={<TransferHistoryPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/categories" element={<CategoriesPage />} />
          <Route path="/budgets" element={<BudgetsPage />} />
          <Route path="/analytics" element={<AnalyticsPage />} />
          <Route path="/subscriptions" element={<SubscriptionsPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/receipts" element={<PlaceholderPage title="Receipts" description="OCR module will be implemented in the next milestone." />} />
          <Route path="/export" element={<PlaceholderPage title="Export" description="Export reports UI will be implemented in the next milestone." />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
