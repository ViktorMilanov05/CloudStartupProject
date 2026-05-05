export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: 'User' | 'Manager' | 'Admin';
  isActive: boolean;
  companyId: string | null;
  companyName: string | null;
  createdAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  user: User;
}

export interface CreateUserRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  role: string;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  role?: string;
  isActive?: boolean;
}
