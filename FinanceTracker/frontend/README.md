# FinanceTracker Frontend

React SPA for FinanceTracker built with TypeScript, Vite, Ant Design, Redux Toolkit, RTK Query and React Router.

## Setup

1. Install Node.js 20+.
2. Copy `.env.example` to `.env`.
3. Fill `VITE_API_URL` and (optionally) exchange-rate vars for transfer preview.
4. Install dependencies: `npm install`.
5. Run in dev mode: `npm run dev`.

## Implemented in current iteration

- Auth: register/login/me + localStorage token persistence.
- Auto-refresh token flow via `/api/auth/refresh` on 401.
- Protected routing and public-only routing.
- App shell with sidebar, dark/light theme switch and RU/EN switch.
- Notifications drawer integrated with API (`list`, `unread-count`, `read-all`).
- Dashboard integrated with accounts and analytics endpoints.
- Accounts page integrated with API:
  - list accounts
  - create account
  - archive account (confirmation)
  - manual balance update (`PATCH /api/accounts/{id}/balance`)
- Transfer page integrated with API (`POST /api/accounts/transfer`) and estimated conversion preview.
- Transfer history page backed by transfer-type transactions.
- Transactions page integrated with API and filters.
- Profile page integrated with `GET/PUT /api/profile`.
- Subscription screen and placeholders for Receipts/Export.
- UI event logging to `localStorage` for demo/debug.

## Notes

- Transfer preview uses public exchange-rate API if `VITE_EXCHANGE_RATE_API_KEY` is provided.
- Final conversion and balances are still enforced on backend.
- In this environment, Node execution is blocked by local policy, so runtime build was not executed here.
