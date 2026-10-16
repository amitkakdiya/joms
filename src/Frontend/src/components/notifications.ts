import * as signalR from "@microsoft/signalr";

export type NotificationItem = {
  notificationId: number;
  userId: number;
  orderId: number;
  eventType: string;
  title: string;
  message: string;
  actionUrl?: string | null;
  isRead: boolean;
  createdAt: string;
  readAt?: string | null;
};

export type NotificationPage = {
  items: NotificationItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

async function ensureSuccess(response: Response, fallback: string): Promise<void> {
  if (response.ok) return;

  const result = await response.json().catch(() => null) as {
    detail?: string;
    title?: string;
    error?: string;
  } | null;
  throw new Error(result?.detail ?? result?.error ?? result?.title ?? `${fallback} (${response.status}).`);
}

export function connectNotifications(
  token: string,
  onNotification: (item: NotificationItem) => void,
) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/notifications`, {
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .build();

  connection.on("ReceiveNotification", onNotification);
  return connection;
}

export async function fetchNotifications(
  token: string,
  page = 1,
  pageSize = 20,
): Promise<NotificationPage> {
  const response = await fetch(
    `${API_URL}/api/notifications?page=${page}&pageSize=${pageSize}`,
    { headers: { Authorization: `Bearer ${token}` }, cache: "no-store" },
  );
  await ensureSuccess(response, "Could not load notifications");
  return response.json();
}

export async function fetchUnreadCount(token: string): Promise<number> {
  const response = await fetch(`${API_URL}/api/notifications/unread-count`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  await ensureSuccess(response, "Could not load unread count");
  const data: { count: number } = await response.json();
  return data.count;
}

export async function markRead(token: string, id: number): Promise<void> {
  const response = await fetch(`${API_URL}/api/notifications/${id}/read`, {
    method: "PATCH",
    headers: { Authorization: `Bearer ${token}` },
  });
  await ensureSuccess(response, "Could not mark notification read");
}

export async function markAllRead(token: string): Promise<number> {
  const response = await fetch(`${API_URL}/api/notifications/read-all`, {
    method: "PATCH",
    headers: { Authorization: `Bearer ${token}` },
  });
  await ensureSuccess(response, "Could not mark notifications read");
  const data: { updatedCount: number } = await response.json();
  return data.updatedCount;
}
