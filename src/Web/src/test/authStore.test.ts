/// <reference types="vitest" />
import { describe, it, expect } from 'vitest';
import { useAuthStore } from '../stores/authStore';

describe('authStore', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, accessToken: null, isAuthenticated: false });
  });

  it('starts unauthenticated', () => {
    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(false);
    expect(state.user).toBeNull();
    expect(state.accessToken).toBeNull();
  });

  it('sets auth state on login', () => {
    const mockUser = {
      id: '123',
      email: 'test@test.com',
      firstName: 'Test',
      lastName: 'User',
      role: 'Admin' as const,
      isActive: true,
      companyId: null,
      companyName: null,
      createdAt: new Date().toISOString(),
    };

    useAuthStore.getState().setAuth(mockUser, 'test-token');

    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(true);
    expect(state.user?.email).toBe('test@test.com');
    expect(state.accessToken).toBe('test-token');
  });

  it('clears state on logout', () => {
    const mockUser = {
      id: '123',
      email: 'test@test.com',
      firstName: 'Test',
      lastName: 'User',
      role: 'Admin' as const,
      isActive: true,
      companyId: null,
      companyName: null,
      createdAt: new Date().toISOString(),
    };

    useAuthStore.getState().setAuth(mockUser, 'test-token');
    useAuthStore.getState().logout();

    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(false);
    expect(state.user).toBeNull();
    expect(state.accessToken).toBeNull();
  });
});
