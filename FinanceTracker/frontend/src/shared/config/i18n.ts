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
