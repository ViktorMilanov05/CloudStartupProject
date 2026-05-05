export interface Template {
  id: string;
  name: string;
  description?: string;
  createdById: string;
  createdByName: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  stepCount: number;
}

export interface TemplateDetail {
  id: string;
  name: string;
  description?: string;
  createdById: string;
  createdByName: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  steps: TemplateStep[];
}

export interface TemplateStep {
  id: string;
  templateId: string;
  title: string;
  instructions?: string;
  sortOrder: number;
}

export interface CreateTemplateRequest {
  name: string;
  description?: string;
  steps: CreateTemplateStepRequest[];
}

export interface UpdateTemplateRequest {
  name?: string;
  description?: string;
  isActive?: boolean;
}

export interface CreateTemplateStepRequest {
  title: string;
  instructions?: string;
}

export interface UpdateTemplateStepRequest {
  title?: string;
  instructions?: string;
}

export interface ReorderStepsRequest {
  stepIds: string[];
}
