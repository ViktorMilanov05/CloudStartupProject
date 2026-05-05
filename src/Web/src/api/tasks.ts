import client from './client';
import type {
  TaskItemDto,
  TaskDetailDto,
  TaskStep,
  TaskComment,
  TaskAttachment,
  CreateTaskRequest,
  CreateTaskFromTemplateRequest,
  UpdateTaskRequest,
  CreateTaskStepRequest,
  UpdateTaskStepRequest,
  ReorderTaskStepsRequest,
  CreateTaskCommentRequest,
  UpdateTaskCommentRequest,
  TaskFilterParams,
  PagedResult,
} from '../types/task';

export const tasksApi = {
  getAll: (params?: TaskFilterParams) =>
    client.get<PagedResult<TaskItemDto>>('/tasks', { params }),

  getById: (id: string) =>
    client.get<TaskDetailDto>(`/tasks/${id}`),

  create: (data: CreateTaskRequest) =>
    client.post<TaskDetailDto>('/tasks', data),

  createFromTemplate: (templateId: string, data: CreateTaskFromTemplateRequest) =>
    client.post<TaskDetailDto>(`/tasks/from-template/${templateId}`, data),

  update: (id: string, data: UpdateTaskRequest) =>
    client.put<TaskDetailDto>(`/tasks/${id}`, data),

  delete: (id: string) =>
    client.delete(`/tasks/${id}`),

  // Steps
  addStep: (taskId: string, data: CreateTaskStepRequest) =>
    client.post<TaskStep>(`/tasks/${taskId}/steps`, data),

  updateStep: (taskId: string, stepId: string, data: UpdateTaskStepRequest) =>
    client.put<TaskStep>(`/tasks/${taskId}/steps/${stepId}`, data),

  completeStep: (taskId: string, stepId: string) =>
    client.put(`/tasks/${taskId}/steps/${stepId}/complete`),

  uncompleteStep: (taskId: string, stepId: string) =>
    client.put(`/tasks/${taskId}/steps/${stepId}/uncomplete`),

  deleteStep: (taskId: string, stepId: string) =>
    client.delete(`/tasks/${taskId}/steps/${stepId}`),

  reorderSteps: (taskId: string, data: ReorderTaskStepsRequest) =>
    client.put(`/tasks/${taskId}/steps/reorder`, data),

  // Comments
  getComments: (taskId: string) =>
    client.get<TaskComment[]>(`/tasks/${taskId}/comments`),

  addComment: (taskId: string, data: CreateTaskCommentRequest) =>
    client.post<TaskComment>(`/tasks/${taskId}/comments`, data),

  updateComment: (taskId: string, commentId: string, data: UpdateTaskCommentRequest) =>
    client.put<TaskComment>(`/tasks/${taskId}/comments/${commentId}`, data),

  deleteComment: (taskId: string, commentId: string) =>
    client.delete(`/tasks/${taskId}/comments/${commentId}`),

  // Attachments
  uploadAttachment: (taskId: string, file: File, commentId?: string) => {
    const formData = new FormData();
    formData.append('file', file);
    const params: Record<string, string> = { taskId };
    if (commentId) params.commentId = commentId;
    return client.post<TaskAttachment>('/files/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      params,
    });
  },

  downloadAttachment: (attachmentId: string, taskId: string) =>
    client.get(`/files/${attachmentId}`, {
      params: { taskId },
      responseType: 'blob',
    }),

  deleteAttachment: (attachmentId: string, taskId: string) =>
    client.delete(`/files/${attachmentId}`, { params: { taskId } }),
};
