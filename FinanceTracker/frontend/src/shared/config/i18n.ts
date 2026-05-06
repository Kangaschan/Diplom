import i18n from "i18next";
import { initReactI18next } from "react-i18next";

import { en } from "../locales/en";
import { ru } from "../locales/ru";

const resources = {
  ru: {
    translation: ru
  },
  en: {
    translation: en
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
