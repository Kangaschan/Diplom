import i18n from "i18next";
import { initReactI18next } from "react-i18next";

const resources = {
  ru: {
    translation: {
      nav: {
        dashboard: "Дашборд",
        accounts: "Счета",
        transfer: "Перевод",
        transferHistory: "История переводов",
        transactions: "Транзакции",
        categories: "Категории",
        budgets: "Бюджеты",
        analytics: "Аналитика",
        subscriptions: "Подписки",
        profile: "Профиль",
        receipts: "Чеки",
        export: "Экспорт"
      },
      actions: {
        quickActions: "Быстрые действия",
        addTransaction: "Добавить транзакцию",
        createTransfer: "Сделать перевод",
        createBudget: "Создать бюджет"
      },
      subscription: {
        title: "Подписки",
        subtitle: "Управляйте текущим тарифом и доступом к premium-возможностям.",
        currentPlan: "Текущий план",
        currentStatus: "Статус",
        expiresAt: "Действует до",
        noExpiration: "Не ограничено",
        free: "Бесплатный",
        premium: "Premium",
        active: "Активна",
        expired: "Истекла",
        cancelled: "Отменена",
        inactive: "Не активна",
        freeCardTitle: "Бесплатный",
        premiumCardTitle: "Premium",
        freeFeature1: "Базовая аналитика",
        freeFeature2: "Счета и транзакции",
        freeFeature3: "Бюджеты и уведомления",
        premiumFeature1: "Расширенная аналитика",
        premiumFeature2: "Анализ регулярных платежей",
        premiumFeature3: "Анализ кредитной нагрузки",
        premiumFeature4: "Работа с чеками и экспорт",
        monthlyPlan: "Месячная подписка",
        durationDays: "дней",
        upgrade: "Оформить подписку",
        manage: "Управлять подпиской",
        currentPlanBadge: "Текущий план",
        summary: "Сейчас у вас {{plan}} подписка со статусом {{status}}.",
        alreadyActive: "Подписка уже активна",
        checkoutUnavailable: "Не удалось найти тариф для покупки.",
        checkoutFailed: "Не удалось открыть страницу оплаты.",
        portalFailed: "Не удалось открыть управление подпиской."
      }
    }
  },
  en: {
    translation: {
      nav: {
        dashboard: "Dashboard",
        accounts: "Accounts",
        transfer: "Transfer",
        transferHistory: "Transfer History",
        transactions: "Transactions",
        categories: "Categories",
        budgets: "Budgets",
        analytics: "Analytics",
        subscriptions: "Subscriptions",
        profile: "Profile",
        receipts: "Receipts",
        export: "Export"
      },
      actions: {
        quickActions: "Quick actions",
        addTransaction: "Add transaction",
        createTransfer: "Create transfer",
        createBudget: "Create budget"
      },
      subscription: {
        title: "Subscriptions",
        subtitle: "Manage your current plan and premium access.",
        currentPlan: "Current plan",
        currentStatus: "Status",
        expiresAt: "Valid until",
        noExpiration: "Unlimited",
        free: "Free",
        premium: "Premium",
        active: "Active",
        expired: "Expired",
        cancelled: "Cancelled",
        inactive: "Inactive",
        freeCardTitle: "Free",
        premiumCardTitle: "Premium",
        freeFeature1: "Basic analytics",
        freeFeature2: "Accounts and transactions",
        freeFeature3: "Budgets and notifications",
        premiumFeature1: "Extended analytics",
        premiumFeature2: "Recurring payments analysis",
        premiumFeature3: "Credit load analysis",
        premiumFeature4: "Receipts and export",
        monthlyPlan: "Monthly subscription",
        durationDays: "days",
        upgrade: "Upgrade now",
        manage: "Manage subscription",
        currentPlanBadge: "Current plan",
        summary: "Your current subscription is {{plan}} with status {{status}}.",
        alreadyActive: "Subscription is already active",
        checkoutUnavailable: "No purchasable plan was found.",
        checkoutFailed: "Failed to open checkout page.",
        portalFailed: "Failed to open subscription portal."
      }
    }
  }
};

void i18n.use(initReactI18next).init({
  resources,
  lng: "ru",
  fallbackLng: "en",
  interpolation: {
    escapeValue: false
  }
});

export default i18n;
