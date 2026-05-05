import axios from 'axios';
import type { AuthResponse } from '../types/user';

export interface SetupRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface SetupStatus {
  setupRequired: boolean;
}

export const setupApi = {
  getStatus: () =>
    axios.get<SetupStatus>('/api/setup/status'),

  initialize: (data: SetupRequest) =>
    axios.post<AuthResponse>('/api/setup/initialize', data, { withCredentials: true }),
};
