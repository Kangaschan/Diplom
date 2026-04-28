import { configureStore } from "@reduxjs/toolkit";

import { uiReducer } from "../../features/theme/uiSlice";
import { authReducer } from "../../features/auth/authSlice";
import { api } from "../../shared/api/baseApi";

export const store = configureStore({
  reducer: {
    ui: uiReducer,
    auth: authReducer,
    [api.reducerPath]: api.reducer
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(api.middleware)
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
