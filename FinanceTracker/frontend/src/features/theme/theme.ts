import type { ThemeConfig } from "antd";
import type { ThemeMode } from "./uiSlice";

const lightTheme: ThemeConfig = {
  token: {
    colorPrimary: "#264f43",
    colorInfo: "#264f43",
    colorSuccess: "#0a7c5f",
    colorLink: "#264f43",
    colorLinkHover: "#0a7c5f",
    colorBgBase: "#edece0",
    colorBgContainer: "#ffffff",
    colorBgElevated: "#ffffff",
    colorTextBase: "#050315",
    colorTextLightSolid: "#ffffff",
    colorBorder: "#d1d4c4",
    colorSplit: "#d1d4c4",
    borderRadius: 10,
    fontFamily: "Manrope, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
  },
  components: {
    Layout: {
      bodyBg: "#edece0",
      headerBg: "#edece0",
      siderBg: "#dddbce"
    },
    Menu: {
      itemBg: "#dddbce",
      itemColor: "#050315",
      itemHoverBg: "#d2d4c4",
      itemHoverColor: "#050315",
      itemSelectedBg: "#264f43",
      itemSelectedColor: "#ffffff",
      itemActiveBg: "#264f43"
    },
    Button: {
      primaryColor: "#ffffff",
      primaryShadow: "none"
    },
    Card: {
      colorBgContainer: "#ffffff",
      colorBorderSecondary: "#d1d4c4",
      headerBg: "#ffffff"
    },
    Segmented: {
      trackBg: "#d8ddcf",
      itemSelectedBg: "#264f43",
      itemSelectedColor: "#ffffff"
    }
  }
};

const darkTheme: ThemeConfig = {
  token: {
    colorPrimary: "#e6ff55",
    colorInfo: "#e6ff55",
    colorSuccess: "#13ae87",
    colorLink: "#433bff",
    colorLinkHover: "#5a54ff",
    colorBgBase: "#042d22",
    colorBgContainer: "#0a3a2d",
    colorBgElevated: "#0d4132",
    colorTextBase: "#e6ff55",
    colorTextLightSolid: "#000000",
    colorBorder: "#13ae87",
    colorSplit: "#116b54",
    borderRadius: 10,
    fontFamily: "Manrope, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
  },
  components: {
    Layout: {
      bodyBg: "#042d22",
      headerBg: "#042d22",
      siderBg: "#0d4132"
    },
    Menu: {
      darkItemBg: "#0d4132",
      darkItemColor: "#e6ff55",
      darkItemHoverBg: "#13614b",
      darkItemHoverColor: "#e6ff55",
      darkItemSelectedBg: "#e6ff55",
      darkItemSelectedColor: "#000000",
      darkItemActiveBg: "#0f5d48"
    },
    Button: {
      defaultBg: "#0a3a2d",
      defaultBorderColor: "#13ae87",
      defaultColor: "#e6ff55",
      defaultHoverBg: "#0f5d48",
      defaultHoverBorderColor: "#e6ff55",
      defaultHoverColor: "#e6ff55",
      primaryColor: "#000000",
      primaryShadow: "none"
    },
    Card: {
      colorBgContainer: "#0a3a2d",
      colorBorderSecondary: "#116b54",
      headerBg: "#0a3a2d"
    },
    Drawer: {
      colorBgElevated: "#0a3a2d",
      colorText: "#e6ff55",
      colorTextHeading: "#e6ff55",
      colorIcon: "#e6ff55"
    },
    Segmented: {
      trackBg: "#0a3a2d",
      itemColor: "#e6ff55",
      itemHoverColor: "#e6ff55",
      itemSelectedBg: "#e6ff55",
      itemSelectedColor: "#000000"
    },
    Tag: {
      defaultBg: "#0f5d48",
      defaultColor: "#e6ff55",
      defaultBorderColor: "#13ae87"
    }
  }
};

export function buildAntdTheme(mode: ThemeMode): ThemeConfig {
  return mode === "dark" ? darkTheme : lightTheme;
}
