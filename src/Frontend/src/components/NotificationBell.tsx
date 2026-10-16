import { useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import {
  connectNotifications,
  fetchNotifications,
  fetchUnreadCount,
  markAllRead,
  markRead,
  type NotificationItem,
} from "./notifications";
import "./NotificationBell.css";

function messageOf(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

export function NotificationBell({ accessToken }: { accessToken: string }) {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unread, setUnread] = useState(0);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const seenIds = useRef(new Set<number>());

  useEffect(() => {
    let alive = true;
    let connection: HubConnection | undefined;

    async function start() {
      const [pageResult, countResult] = await Promise.allSettled([
        fetchNotifications(accessToken),
        fetchUnreadCount(accessToken),
      ]);
      if (!alive) return;

      const loadErrors: string[] = [];
      if (pageResult.status === "fulfilled") {
        setItems(pageResult.value.items);
        seenIds.current = new Set(pageResult.value.items.map((item) => item.notificationId));
      } else {
        loadErrors.push(messageOf(pageResult.reason, "Could not load notifications."));
      }
      if (countResult.status === "fulfilled") {
        setUnread(countResult.value);
      } else {
        loadErrors.push(messageOf(countResult.reason, "Could not load unread count."));
      }
      setError(loadErrors.length > 0 ? loadErrors.join(" ") : null);

      connection = connectNotifications(accessToken, (item) => {
        if (!alive || seenIds.current.has(item.notificationId)) return;
        seenIds.current.add(item.notificationId);
        setItems((old) => [item, ...old]);
        if (!item.isRead) setUnread((old) => old + 1);
      });
      connection.onreconnected(async () => {
        const [latestPageResult, latestCountResult] = await Promise.allSettled([
          fetchNotifications(accessToken),
          fetchUnreadCount(accessToken),
        ]);
        if (!alive) return;

        const refreshErrors: string[] = [];
        if (latestPageResult.status === "fulfilled") {
          const latestPage = latestPageResult.value;
          for (const item of latestPage.items) seenIds.current.add(item.notificationId);
          setItems((old) => {
            const merged = new Map<number, NotificationItem>();
            for (const item of old) merged.set(item.notificationId, item);
            for (const item of latestPage.items) merged.set(item.notificationId, item);
            return [...merged.values()].sort(
              (a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt),
            );
          });
        } else {
          refreshErrors.push(messageOf(latestPageResult.reason, "Could not refresh notifications."));
        }
        if (latestCountResult.status === "fulfilled") {
          setUnread(latestCountResult.value);
        } else {
          refreshErrors.push(messageOf(latestCountResult.reason, "Could not refresh unread count."));
        }
        setError(refreshErrors.length > 0 ? refreshErrors.join(" ") : null);
      });

      try {
        await connection.start();
      } catch (connectionError) {
        if (alive) {
          setError((current) => [
            current,
            `Live notifications are unavailable: ${messageOf(connectionError, "connection failed.")}`,
          ].filter(Boolean).join(" "));
        }
      }
    }

    void start();
    return () => {
      alive = false;
      if (connection) {
        connection.off("ReceiveNotification");
        void connection.stop();
      }
    };
  }, [accessToken]);

  async function read(item: NotificationItem) {
    if (item.isRead) return;
    try {
      await markRead(accessToken, item.notificationId);
      setItems((old) =>
        old.map((current) =>
          current.notificationId === item.notificationId
            ? { ...current, isRead: true }
            : current,
        ),
      );
      setUnread((old) => Math.max(0, old - 1));
      setError(null);
    } catch (readError) {
      setError(messageOf(readError, "Could not mark notification read."));
    }
  }

  async function readAll() {
    try {
      await markAllRead(accessToken);
      setItems((old) => old.map((item) => ({ ...item, isRead: true })));
      setUnread(0);
      setError(null);
    } catch (readError) {
      setError(messageOf(readError, "Could not mark notifications read."));
    }
  }

  return (
    <div className="notification-menu">
      <button
        aria-expanded={open}
        className="notification-toggle"
        type="button"
        onClick={() => setOpen((value) => !value)}
      >
        <span aria-hidden="true">♧</span>
        <span>Notifications</span>
        {unread > 0 && <span className="notification-count">{unread}</span>}
      </button>
      {open && (
        <section aria-label="Notifications" className="notification-panel">
          <div className="notification-heading">
            <div>
              <p className="notification-eyebrow">Inbox</p>
              <h2>Notifications</h2>
            </div>
            {unread > 0 && (
              <button className="read-all-button" type="button" onClick={() => void readAll()}>
                Mark all read
              </button>
            )}
          </div>
          {error && <p className="notification-error" role="alert">{error}</p>}
          {items.length === 0 ? (
            <p className="notification-empty">No notifications yet.</p>
          ) : (
            <ul className="notification-list">
              {items.map((item) => (
                <li
                  className={`notification-item${item.isRead ? "" : " unread"}`}
                  key={item.notificationId}
                >
                  <div className="notification-item-heading">
                    <span className="notification-indicator" />
                    <p>{item.title}</p>
                  </div>
                  <p className="notification-message">{item.message}</p>
                  <div className="notification-meta">
                    <time dateTime={item.createdAt}>
                      {new Date(item.createdAt).toLocaleString()}
                    </time>
                    {!item.isRead && (
                      <button type="button" onClick={() => void read(item)}>Mark read</button>
                    )}
                  </div>
                  {item.actionUrl && (
                    <a className="notification-link" href={item.actionUrl}>
                      Open order <span aria-hidden="true">→</span>
                    </a>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  );
}
