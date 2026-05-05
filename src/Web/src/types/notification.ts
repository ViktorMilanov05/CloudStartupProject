export interface NotificationDto {
  id: string;
  type: string;
  message: string;
  taskId?: string;
  taskTitle?: string;
  actorId: string;
  actorName: string;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationPagedResult {
  items: NotificationDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}
