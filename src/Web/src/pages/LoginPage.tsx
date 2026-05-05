import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box,
  Button,
  TextField,
  Typography,
  Alert,
  Paper,
  Stack,
} from '@mui/material';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { authApi } from '../api/auth';
import { useAuthStore } from '../stores/authStore';

const loginSchema = z.object({
  email: z.string().email('A valid email is required'),
  password: z.string().min(1, 'Password is required'),
});

type LoginFormData = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const loginForm = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const handleLogin = async (data: LoginFormData) => {
    setError(null);
    setLoading(true);
    try {
      const res = await authApi.login(data);
      setAuth(res.data.user, res.data.accessToken);
      navigate('/');
    } catch (err: any) {
      setError(err.response?.data?.detail || err.response?.data?.message || 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        width: '100vw',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        pt: 8,
        bgcolor: 'grey.100',
      }}
    >
      <Typography variant="h2" align="center" fontWeight={700} gutterBottom>
        Welcome to Planify
      </Typography>
      <Typography variant="subtitle1" align="center" color="text.secondary" sx={{ mb: 6 }}>
        The best application for managing your company processes
      </Typography>

      <Paper elevation={3} sx={{ p: 5, width: '100%', maxWidth: 440 }}>
        <Typography variant="h5" align="center" sx={{ mb: 3 }}>
          Login
        </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <form onSubmit={loginForm.handleSubmit(handleLogin)}>
            <Stack spacing={3}>
              <TextField
                id="login-email"
                label="Email"
                autoComplete="email"
                fullWidth
                {...loginForm.register('email')}
                error={!!loginForm.formState.errors.email}
                helperText={loginForm.formState.errors.email?.message}
              />
              <TextField
                id="login-password"
                label="Password"
                type="password"
                autoComplete="current-password"
                fullWidth
                {...loginForm.register('password')}
                error={!!loginForm.formState.errors.password}
                helperText={loginForm.formState.errors.password?.message}
              />
              <Button type="submit" variant="contained" size="large" fullWidth disabled={loading}>
                {loading ? 'Signing in...' : 'Sign In'}
              </Button>
            </Stack>
          </form>
        </Paper>
    </Box>
  );
}
