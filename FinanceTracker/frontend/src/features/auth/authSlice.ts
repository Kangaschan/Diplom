import { createSlice, type PayloadAction } from "@reduxjs/toolkit";
import { clearTokens, loadTokens, saveTokens } from "../../shared/lib/authStorage";

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
}

const stored = typeof window !== "undefined" ? loadTokens() : null;

const initialState: AuthState = {
  accessToken: stored?.accessToken ?? null,
  refreshToken: stored?.refreshToken ?? null,
  isAuthenticated: Boolean(stored?.accessToken)
};

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    setTokens(state, action: PayloadAction<{ accessToken: string; refreshToken: string }>) {
      state.accessToken = action.payload.accessToken;
      state.refreshToken = action.payload.refreshToken;
      state.isAuthenticated = true;
      saveTokens(action.payload);
    },
    clearAuth(state) {
      state.accessToken = null;
      state.refreshToken = null;
      state.isAuthenticated = false;
      clearTokens();
    }
  }
});

export const { setTokens, clearAuth } = authSlice.actions;
export const authReducer = authSlice.reducer;
