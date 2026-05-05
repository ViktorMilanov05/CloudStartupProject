/// <reference types="vitest" />
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import LoginPage from '../pages/LoginPage';

describe('LoginPage', () => {
  const renderLoginPage = () =>
    render(
      <BrowserRouter>
        <LoginPage />
      </BrowserRouter>
    );

  it('renders login form', () => {
    renderLoginPage();
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
  });

  it('renders welcome text', () => {
    renderLoginPage();
    expect(screen.getByText(/welcome to planify/i)).toBeInTheDocument();
  });

  it('has sign in button enabled by default', () => {
    renderLoginPage();
    expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled();
  });
});
