import client from './client';
import type { User, CreateUserRequest, UpdateUserRequest } from '../types/user';

export const usersApi = {
  getAll: () =>
    client.get<User[]>('/users'),

  getMe: () =>
    client.get<User>('/users/me'),

  create: (data: CreateUserRequest) =>
    client.post<User>('/users', data),

  update: (id: string, data: UpdateUserRequest) =>
    client.put<User>(`/users/${id}`, data),

  delete: (id: string) =>
    client.delete(`/users/${id}`),
};
