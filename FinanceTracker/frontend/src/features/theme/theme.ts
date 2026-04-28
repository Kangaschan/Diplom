import type { ThemeConfig } from "antd";
import type { ThemeMode } from "./uiSlice";

const lightTheme: ThemeConfig = {
  token: {
    colorPrimary: "#326586",
    colorInfo: "#326586",
    colorSuccess: "#13ae87",
    colorLink: "#433bff",
    colorLinkHover: "#5a54ff",
    colorBgBase: "#f4e9d4",
    colorTextBase: "#000000",
    colorBorder: "#d9c8a7",
    borderRadius: 10,
    fontFamily: "Manrope, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
  },
  components: {
    Layout: {
      bodyBg: "#f4e9d4",
      headerBg: "#f4e9d4",
      siderBg: "#f4e9d4"
    },
    Menu: {
      itemBg: "#f4e9d4",
      itemColor: "#000000",
      itemHoverBg: "#e9e0cf",
      itemHoverColor: "#000000",
      itemSelectedBg: "#d8e5ee",
      itemSelectedColor: "#326586",
      itemActiveBg: "#d8e5ee"
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
    colorTextBase: "#e6ff55",
    colorTextLightSolid: "#000000",
    colorBorder: "#13ae87",
    borderRadius: 10,
    fontFamily: "Manrope, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
  },
  components: {
    Layout: {
      bodyBg: "#042d22",
      headerBg: "#042d22",
      siderBg: "#042d22"
    },
    Menu: {
      darkItemBg: "#042d22",
      darkItemColor: "#e6ff55",
      darkItemHoverBg: "#0b4737",
      darkItemHoverColor: "#e6ff55",
      darkItemSelectedBg: "#e6ff55",
      darkItemSelectedColor: "#000000",
      darkItemActiveBg: "#0f5d48"
    },
    Button: {
      primaryColor: "#000000",
      defaultBorderColor: "#13ae87",
      defaultColor: "#e6ff55"
    }
  }
};

export function buildAntdTheme(mode: ThemeMode): ThemeConfig {
  return mode === "dark" ? darkTheme : lightTheme;
}
