import client from './client';
import type { NotificationPagedResult } from '../types/notification';

export const notificationsApi = {
  getAll: (page = 1, pageSize = 20) =>
    client.get<NotificationPagedResult>('/notifications', { params: { page, pageSize } }),

  getUnreadCount: () =>
    client.get<{ count: number }>('/notifications/unread-count'),

  markAsRead: (id: string) =>
    client.put(`/notifications/${id}/read`),

  markAllAsRead: () =>
    client.put('/notifications/read-all'),

  delete: (id: string) =>
    client.delete(`/notifications/${id}`),

  deleteAll: () =>
    client.delete('/notifications'),
};
