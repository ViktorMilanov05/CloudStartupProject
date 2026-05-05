import { create } from 'zustand';
import * as signalR from '@microsoft/signalr';
import { notificationsApi } from '../api/notifications';
import type { NotificationDto } from '../types/notification';

interface NotificationState {
  notifications: NotificationDto[];
  unreadCount: number;
  totalCount: number;
  page: number;
  connection: signalR.HubConnection | null;

  startConnection: (accessToken: string) => void;
  stopConnection: () => void;
  fetchUnreadCount: () => Promise<void>;
  fetchNotifications: (page?: number) => Promise<void>;
  markAsRead: (id: string) => Promise<void>;
  markAllAsRead: () => Promise<void>;
  deleteNotification: (id: string) => Promise<void>;
  deleteAllNotifications: () => Promise<void>;
  addNotification: (notification: NotificationDto) => void;
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  notifications: [],
  unreadCount: 0,
  totalCount: 0,
  page: 1,
  connection: null,

  startConnection: (accessToken: string) => {
    const existing = get().connection;
    if (existing) {
      // In StrictMode, stop the old connection before creating a new one
      existing.stop();
      set({ connection: null });
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', {
        accessTokenFactory: () => accessToken,
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Critical)
      .build();

    connection.on('ReceiveNotification', (notification: NotificationDto) => {
      get().addNotification(notification);
    });

    set({ connection });

    connection.start().catch((err) => {
      // AbortError / stop() race is expected when React StrictMode double-mounts
      if (err?.name !== 'AbortError' && !err?.message?.includes('stop() was called')) {
        console.error('SignalR connection failed:', err);
      }
    });
  },

  stopConnection: () => {
    const conn = get().connection;
    if (conn) {
      conn.stop();
      set({ connection: null, notifications: [], unreadCount: 0, totalCount: 0, page: 1 });
    }
  },

  fetchUnreadCount: async () => {
    try {
      const { data } = await notificationsApi.getUnreadCount();
      set({ unreadCount: data.count });
    } catch {
      // ignore
    }
  },

  fetchNotifications: async (page = 1) => {
    try {
      const { data } = await notificationsApi.getAll(page);
      if (page === 1) {
        set({ notifications: data.items, totalCount: data.totalCount, page });
      } else {
        set((state) => ({
          notifications: [...state.notifications, ...data.items],
          totalCount: data.totalCount,
          page,
        }));
      }
    } catch {
      // ignore
    }
  },

  markAsRead: async (id: string) => {
    try {
      await notificationsApi.markAsRead(id);
      set((state) => ({
        notifications: state.notifications.map((n) =>
          n.id === id ? { ...n, isRead: true } : n
        ),
        unreadCount: Math.max(0, state.unreadCount - 1),
      }));
    } catch {
      // ignore
    }
  },

  markAllAsRead: async () => {
    try {
      await notificationsApi.markAllAsRead();
      set((state) => ({
        notifications: state.notifications.map((n) => ({ ...n, isRead: true })),
        unreadCount: 0,
      }));
    } catch {
      // ignore
    }
  },

  deleteNotification: async (id: string) => {
    try {
      const notification = get().notifications.find((n) => n.id === id);
      await notificationsApi.delete(id);
      set((state) => ({
        notifications: state.notifications.filter((n) => n.id !== id),
        totalCount: Math.max(0, state.totalCount - 1),
        unreadCount: notification && !notification.isRead ? Math.max(0, state.unreadCount - 1) : state.unreadCount,
      }));
    } catch {
      // ignore
    }
  },

  deleteAllNotifications: async () => {
    try {
      await notificationsApi.deleteAll();
      set({ notifications: [], totalCount: 0, unreadCount: 0 });
    } catch {
      // ignore
    }
  },

  addNotification: (notification: NotificationDto) => {
    set((state) => ({
      notifications: [notification, ...state.notifications],
      unreadCount: state.unreadCount + 1,
      totalCount: state.totalCount + 1,
    }));
  },
}));
