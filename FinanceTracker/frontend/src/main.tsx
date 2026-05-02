import "@fontsource/manrope/400.css";
import "@fontsource/manrope/500.css";
import "@fontsource/manrope/600.css";
import "@fontsource/manrope/700.css";
import "antd/dist/reset.css";
import "./styles.css";

import React, { useEffect } from "react";
import ReactDOM from "react-dom/client";
import { Provider } from "react-redux";
import { ConfigProvider } from "antd";
import { BrowserRouter } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { AppRouter } from "./app/router/AppRouter";
import { store } from "./app/providers/store";
import { useAppSelector } from "./shared/lib/hooks";
import { buildAntdTheme } from "./features/theme/theme";
import "./shared/config/i18n";

function Root() {
  const themeMode = useAppSelector((state) => state.ui.theme);
  const language = useAppSelector((state) => state.ui.language);
  const { i18n } = useTranslation();

  useEffect(() => {
    if (i18n.language !== language) {
      void i18n.changeLanguage(language);
    }
  }, [i18n, language]);

  useEffect(() => {
    document.body.dataset.theme = themeMode;
  }, [themeMode]);

  return (
    <ConfigProvider theme={buildAntdTheme(themeMode)}>
      <BrowserRouter>
        <AppRouter />
      </BrowserRouter>
    </ConfigProvider>
  );
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Provider store={store}>
      <Root />
    </Provider>
  </React.StrictMode>
);
