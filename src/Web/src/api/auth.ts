import client from './client';
import type { AuthResponse, LoginRequest } from '../types/user';

export const authApi = {
  login: (data: LoginRequest) =>
    client.post<AuthResponse>('/auth/login', data),

  refresh: () =>
    client.post<AuthResponse>('/auth/refresh'),

  logout: () =>
    client.post('/auth/logout'),
};
