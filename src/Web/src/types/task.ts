export type TaskStatus = 'ToDo' | 'InProgress' | 'Done' | 'Blocked';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export interface TaskAssigneeDto {
  id: string;
  name: string;
}

export interface TaskItemDto {
  id: string;
  title: string;
  description?: string;
  status: TaskStatus;
  priority: TaskPriority;
  dueDate?: string;
  createdById: string;
  createdByName: string;
  assignees: TaskAssigneeDto[];
  sourceTemplateId?: string;
  sourceTemplateName?: string;
  createdAt: string;
  updatedAt: string;
  stepCount: number;
  completedStepCount: number;
}

export interface TaskDetailDto extends TaskItemDto {
  steps: TaskStep[];
  comments: TaskComment[];
  attachments: TaskAttachment[];
}

export interface TaskStep {
  id: string;
  taskId: string;
  title: string;
  instructions?: string;
  sortOrder: number;
  isCompleted: boolean;
  completedAt?: string;
  completedById?: string;
  completedByName?: string;
}

export interface TaskComment {
  id: string;
  taskId: string;
  authorId: string;
  authorName: string;
  content: string;
  createdAt: string;
  attachments: TaskAttachment[];
}

export interface TaskAttachment {
  id: string;
  taskId: string;
  commentId?: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  uploadedById: string;
  uploadedByName: string;
  createdAt: string;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  assigneeIds: string[];
  priority: string;
  dueDate?: string;
}

export interface CreateTaskFromTemplateRequest {
  title?: string;
  description?: string;
  assigneeIds: string[];
  priority: string;
  dueDate?: string;
}

export interface UpdateTaskRequest {
  title?: string;
  description?: string;
  status?: string;
  priority?: string;
  dueDate?: string;
  assigneeIds?: string[];
}

export interface CreateTaskStepRequest {
  title: string;
  instructions?: string;
}

export interface UpdateTaskStepRequest {
  title?: string;
  instructions?: string;
}

export interface ReorderTaskStepsRequest {
  stepIds: string[];
}

export interface CreateTaskCommentRequest {
  content: string;
}

export interface UpdateTaskCommentRequest {
  content: string;
}

export interface TaskFilterParams {
  status?: string;
  priority?: string;
  assigneeId?: string;
  dueDateFrom?: string;
  dueDateTo?: string;
  search?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
