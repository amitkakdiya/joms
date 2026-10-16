export type OrderItem = {
  orderId: number;
  customerUserId: number;
  description: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
  assignedCadUserId: number | null;
  assignedCadDisplayName: string | null;
  assignedUserId: number | null;
  assignedDisplayName: string | null;
  assignedRole: string | null;
};

export type WorkflowUser = {
  userId: number;
  displayName: string;
};

export type CadUser = WorkflowUser;

const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

async function request<T>(path: string, token: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...init,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...init?.headers,
    },
    cache: "no-store",
  });

  if (!response.ok) {
    const result = (await response.json().catch(() => null)) as {
      error?: string;
      detail?: string;
      title?: string;
    } | null;
    throw new Error(result?.error ?? result?.detail ?? result?.title ?? `Order request failed (${response.status}).`);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export function fetchOrders(token: string): Promise<OrderItem[]> {
  return request<OrderItem[]>("/api/orders", token);
}

export function fetchCadUsers(token: string): Promise<CadUser[]> {
  return request<CadUser[]>("/api/orders/cad-users", token);
}

export function fetchProductionUsers(token: string): Promise<WorkflowUser[]> {
  return request<WorkflowUser[]>("/api/orders/production-users", token);
}

export function fetchBillingUsers(token: string): Promise<WorkflowUser[]> {
  return request<WorkflowUser[]>("/api/orders/billing-users", token);
}

export function submitOrder(token: string, description: string): Promise<unknown> {
  return request("/api/orders/submit", token, {
    method: "POST",
    body: JSON.stringify({ description }),
  });
}

export function assignOrderToCad(
  token: string,
  orderId: number,
  cadUserId: number,
): Promise<unknown> {
  return request(`/api/orders/${orderId}/assign-cad`, token, {
    method: "POST",
    body: JSON.stringify({ cadUserId }),
  });
}

export function submitOrderDesign(token: string, orderId: number): Promise<unknown> {
  return request(`/api/orders/${orderId}/design/submit`, token, { method: "POST" });
}

export function decideOrderDesign(
  token: string,
  orderId: number,
  approved: boolean,
): Promise<unknown> {
  return request(`/api/orders/${orderId}/design/${approved ? "approve" : "reject"}`, token, {
    method: "POST",
    body: JSON.stringify({ comment: null }),
  });
}

export function assignOrderToProduction(
  token: string,
  orderId: number,
  userId: number,
): Promise<unknown> {
  return request(`/api/orders/${orderId}/assign-production`, token, {
    method: "POST",
    body: JSON.stringify({ userId }),
  });
}

export function advanceProduction(token: string, orderId: number): Promise<unknown> {
  return request(`/api/orders/${orderId}/production/advance`, token, { method: "POST" });
}

export function assignOrderToBilling(
  token: string,
  orderId: number,
  userId: number,
): Promise<unknown> {
  return request(`/api/orders/${orderId}/production/assign-billing`, token, {
    method: "POST",
    body: JSON.stringify({ userId }),
  });
}

export function completeOrderBilling(token: string, orderId: number): Promise<unknown> {
  return request(`/api/orders/${orderId}/billing/complete`, token, { method: "POST" });
}

export function markOrderDispatched(token: string, orderId: number): Promise<unknown> {
  return request(`/api/orders/${orderId}/dispatch`, token, { method: "POST" });
}
