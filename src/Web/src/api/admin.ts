import client from './client';
import type { Company, CreateCompanyRequest } from '../types/company';
import type { User, CreateUserRequest, UpdateUserRequest } from '../types/user';

export const adminApi = {
  getCompanies: () =>
    client.get<Company[]>('/admin/companies'),

  createCompany: (data: CreateCompanyRequest) =>
    client.post<Company>('/admin/companies', data),

  getCompanyUsers: (companyId: string) =>
    client.get<User[]>(`/admin/companies/${companyId}/users`),

  createCompanyUser: (companyId: string, data: CreateUserRequest) =>
    client.post<User>(`/admin/companies/${companyId}/users`, data),

  updateUser: (userId: string, data: UpdateUserRequest) =>
    client.put<User>(`/admin/users/${userId}`, data),
};
