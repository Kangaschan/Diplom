export type UiEvent =
  | "login_success"
  | "login_failed"
  | "transfer_created"
  | "balance_updated"
  | "premium_block_opened";

interface EventPayload {
  name: UiEvent;
  screen: string;
  details?: Record<string, unknown>;
}

export function logUiEvent(payload: EventPayload): void {
  const entry = {
    ...payload,
    timestamp: new Date().toISOString()
  };

  const prev = localStorage.getItem("ft_ui_events");
  const list = prev ? (JSON.parse(prev) as EventPayload[]) : [];
  list.push(entry);
  localStorage.setItem("ft_ui_events", JSON.stringify(list.slice(-200)));
  console.info("[UI_EVENT]", entry);
}
