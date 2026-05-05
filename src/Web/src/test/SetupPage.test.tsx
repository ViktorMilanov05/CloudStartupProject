/// <reference types="vitest" />
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import SetupPage from '../pages/SetupPage';

describe('SetupPage', () => {
  const renderSetupPage = () =>
    render(
      <BrowserRouter>
        <SetupPage />
      </BrowserRouter>
    );

  it('renders setup form fields', () => {
    renderSetupPage();
    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/last name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^password$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/confirm password/i)).toBeInTheDocument();
  });

  it('renders heading text', () => {
    renderSetupPage();
    expect(screen.getByText(/let's set up your admin account/i)).toBeInTheDocument();
    expect(screen.getByText(/welcome to planify/i)).toBeInTheDocument();
  });

  it('has submit button', () => {
    renderSetupPage();
    expect(screen.getByRole('button', { name: /create admin account/i })).toBeInTheDocument();
  });
});
