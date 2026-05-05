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
import { setupApi, type SetupRequest } from '../api/setup';
import { useAuthStore } from '../stores/authStore';

const setupSchema = z.object({
  email: z.string().email('A valid email is required'),
  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[a-z]/, 'Password must contain a lowercase letter')
    .regex(/[A-Z]/, 'Password must contain an uppercase letter')
    .regex(/\d/, 'Password must contain a digit'),
  confirmPassword: z.string().min(1, 'Confirm your password'),
  firstName: z.string().min(1, 'First name is required').max(100),
  lastName: z.string().min(1, 'Last name is required').max(100),
}).refine((data) => data.password === data.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword'],
});

type SetupFormData = z.infer<typeof setupSchema>;

export default function SetupPage() {
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const form = useForm<SetupFormData>({
    resolver: zodResolver(setupSchema),
    defaultValues: { email: '', password: '', confirmPassword: '', firstName: '', lastName: '' },
  });

  const handleSetup = async (data: SetupFormData) => {
    setError(null);
    setLoading(true);
    try {
      const request: SetupRequest = {
        email: data.email,
        password: data.password,
        firstName: data.firstName,
        lastName: data.lastName,
      };
      const res = await setupApi.initialize(request);
      setAuth(res.data.user, res.data.accessToken);
      navigate('/');
    } catch (err: any) {
      setError(
        err.response?.data?.errors?.[0] ||
        err.response?.data?.detail ||
        err.response?.data?.message ||
        'Setup failed. Please try again.'
      );
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
      <Typography variant="subtitle1" align="center" color="text.secondary" sx={{ mb: 4 }}>
        Let's set up your admin account to get started
      </Typography>

      <Paper elevation={3} sx={{ p: 5, width: '100%', maxWidth: 480 }}>
        <Typography variant="h5" align="center" sx={{ mb: 3 }}>
          Create Admin Account
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <form onSubmit={form.handleSubmit(handleSetup)}>
          <Stack spacing={3}>
            <Stack direction="row" spacing={2}>
              <TextField
                id="setup-firstName"
                label="First Name"
                autoComplete="given-name"
                fullWidth
                {...form.register('firstName')}
                error={!!form.formState.errors.firstName}
                helperText={form.formState.errors.firstName?.message}
              />
              <TextField
                id="setup-lastName"
                label="Last Name"
                autoComplete="family-name"
                fullWidth
                {...form.register('lastName')}
                error={!!form.formState.errors.lastName}
                helperText={form.formState.errors.lastName?.message}
              />
            </Stack>
            <TextField
              id="setup-email"
              label="Email"
              autoComplete="email"
              fullWidth
              {...form.register('email')}
              error={!!form.formState.errors.email}
              helperText={form.formState.errors.email?.message}
            />
            <TextField
              id="setup-password"
              label="Password"
              type="password"
              autoComplete="new-password"
              fullWidth
              {...form.register('password')}
              error={!!form.formState.errors.password}
              helperText={form.formState.errors.password?.message}
            />
            <TextField
              id="setup-confirmPassword"
              label="Confirm Password"
              type="password"
              autoComplete="new-password"
              fullWidth
              {...form.register('confirmPassword')}
              error={!!form.formState.errors.confirmPassword}
              helperText={form.formState.errors.confirmPassword?.message}
            />
            <Button type="submit" variant="contained" size="large" fullWidth disabled={loading}>
              {loading ? 'Creating account...' : 'Create Admin Account'}
            </Button>
          </Stack>
        </form>
      </Paper>
    </Box>
  );
}
