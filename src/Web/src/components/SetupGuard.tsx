import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { setupApi } from '../api/setup';

interface SetupGuardProps {
  children: React.ReactNode;
}

/**
 * Wraps routes that should only be accessible when NO admin exists (i.e. /setup).
 * Redirects to /login once setup is already complete.
 */
export function SetupRoute({ children }: SetupGuardProps) {
  const [status, setStatus] = useState<'loading' | 'required' | 'done'>('loading');

  useEffect(() => {
    setupApi.getStatus()
      .then((res) => setStatus(res.data.setupRequired ? 'required' : 'done'))
      .catch(() => setStatus('done'));
  }, []);

  if (status === 'loading') {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
        <CircularProgress />
      </Box>
    );
  }

  if (status === 'done') {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

/**
 * Wraps the login page. Redirects to /setup if no admin exists.
 */
export function LoginGuard({ children }: SetupGuardProps) {
  const [status, setStatus] = useState<'loading' | 'required' | 'done'>('loading');

  useEffect(() => {
    setupApi.getStatus()
      .then((res) => setStatus(res.data.setupRequired ? 'required' : 'done'))
      .catch(() => setStatus('done'));
  }, []);

  if (status === 'loading') {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
        <CircularProgress />
      </Box>
    );
  }

  if (status === 'required') {
    return <Navigate to="/setup" replace />;
  }

  return <>{children}</>;
}
