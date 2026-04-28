import { createSlice, type PayloadAction } from "@reduxjs/toolkit";

export type ThemeMode = "light" | "dark";

interface UiState {
  theme: ThemeMode;
  language: "ru" | "en";
}

const initialTheme = (localStorage.getItem("ft_theme") as ThemeMode | null) ?? "light";
const initialLanguage = (localStorage.getItem("ft_language") as "ru" | "en" | null) ?? "ru";

const initialState: UiState = {
  theme: initialTheme,
  language: initialLanguage
};

const uiSlice = createSlice({
  name: "ui",
  initialState,
  reducers: {
    toggleTheme(state) {
      state.theme = state.theme === "light" ? "dark" : "light";
      localStorage.setItem("ft_theme", state.theme);
    },
    setTheme(state, action: PayloadAction<ThemeMode>) {
      state.theme = action.payload;
      localStorage.setItem("ft_theme", state.theme);
    },
    setLanguage(state, action: PayloadAction<"ru" | "en">) {
      state.language = action.payload;
      localStorage.setItem("ft_language", state.language);
    }
  }
});

export const { toggleTheme, setTheme, setLanguage } = uiSlice.actions;
export const uiReducer = uiSlice.reducer;
