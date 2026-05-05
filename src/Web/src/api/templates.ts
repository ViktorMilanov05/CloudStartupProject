import client from './client';
import type {
  Template,
  TemplateDetail,
  TemplateStep,
  CreateTemplateRequest,
  UpdateTemplateRequest,
  CreateTemplateStepRequest,
  UpdateTemplateStepRequest,
  ReorderStepsRequest,
} from '../types/template';

export const templatesApi = {
  getAll: (isActive?: boolean) =>
    client.get<Template[]>('/templates', { params: isActive !== undefined ? { isActive } : {} }),

  getById: (id: string) =>
    client.get<TemplateDetail>(`/templates/${id}`),

  create: (data: CreateTemplateRequest) =>
    client.post<TemplateDetail>('/templates', data),

  update: (id: string, data: UpdateTemplateRequest) =>
    client.put<TemplateDetail>(`/templates/${id}`, data),

  delete: (id: string) =>
    client.delete(`/templates/${id}`),

  addStep: (templateId: string, data: CreateTemplateStepRequest) =>
    client.post<TemplateStep>(`/templates/${templateId}/steps`, data),

  updateStep: (templateId: string, stepId: string, data: UpdateTemplateStepRequest) =>
    client.put<TemplateStep>(`/templates/${templateId}/steps/${stepId}`, data),

  deleteStep: (templateId: string, stepId: string) =>
    client.delete(`/templates/${templateId}/steps/${stepId}`),

  reorderSteps: (templateId: string, data: ReorderStepsRequest) =>
    client.put(`/templates/${templateId}/steps/reorder`, data),
};

export const filesApi = {
  uploadImage: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return client.post<{ url: string }>('/files/upload-image', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};
