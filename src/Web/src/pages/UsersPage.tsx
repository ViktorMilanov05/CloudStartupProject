import { useState, useEffect } from 'react';
import {
  Box,
  Typography,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem as SelectItem,
  Stack,
  Alert,
  IconButton,
  CircularProgress,
  Snackbar,
  Tooltip,
} from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { usersApi } from '../api/users';
import type { User, CreateUserRequest, UpdateUserRequest } from '../types/user';

const createUserSchema = z.object({
  email: z.string().email('A valid email is required').max(256),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  firstName: z.string().min(1, 'First name is required').max(100),
  lastName: z.string().min(1, 'Last name is required').max(100),
  role: z.enum(['User', 'Manager']),
});

type CreateUserFormData = z.infer<typeof createUserSchema>;

export default function UsersPage() {
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [editUser, setEditUser] = useState<User | null>(null);
  const [editRole, setEditRole] = useState<string>('User');
  const [deleteTarget, setDeleteTarget] = useState<User | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Sync local edit state when editUser changes
  useEffect(() => {
    if (editUser) {
      setEditRole(editUser.role);
      setError(null);
    }
  }, [editUser]);

  const { data: users, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: () => usersApi.getAll().then((r) => r.data),
  });

  const createMutation = useMutation({
    mutationFn: (data: CreateUserRequest) => usersApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setCreateOpen(false);
      setError(null);
      setSuccessMsg('User created successfully');
    },
    onError: (err: any) => {
      setError(err.response?.data?.detail || 'Failed to create user');
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateUserRequest }) => usersApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setEditUser(null);
      setError(null);
      setSuccessMsg('User updated successfully');
    },
    onError: (err: any) => {
      setError(err.response?.data?.detail || 'Failed to update user');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => usersApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setDeleteTarget(null);
      setSuccessMsg('User permanently deleted');
    },
    onError: (err: any) => {
      setError(err.response?.data?.detail || 'Failed to delete user');
    },
  });

  const createForm = useForm<CreateUserFormData>({
    resolver: zodResolver(createUserSchema),
    defaultValues: { email: '', password: '', firstName: '', lastName: '', role: 'User' },
  });

  const handleCreate = (data: CreateUserFormData) => {
    setError(null);
    createMutation.mutate(data);
  };

  const handleSaveEdit = () => {
    if (!editUser) return;
    const data: UpdateUserRequest = {};
    if (editRole !== editUser.role) data.role = editRole;
    if (Object.keys(data).length === 0) {
      setEditUser(null);
      return;
    }
    updateMutation.mutate({ id: editUser.id, data });
  };

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Users</Typography>
        <Button
          variant="contained"
          onClick={() => {
            createForm.reset();
            setError(null);
            setCreateOpen(true);
          }}
        >
          Add User
        </Button>
      </Box>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Role</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users?.map((user) => (
              <TableRow key={user.id}>
                <TableCell>{user.firstName} {user.lastName}</TableCell>
                <TableCell>{user.email}</TableCell>
                <TableCell>
                  <Chip
                    label={user.role}
                    color={user.role === 'Manager' ? 'primary' : 'default'}
                    size="small"
                  />
                </TableCell>
                <TableCell align="right">
                  <Tooltip title="Edit">
                    <IconButton size="small" onClick={() => setEditUser(user)}>
                      <EditIcon />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete permanently">
                    <IconButton size="small" color="error" onClick={() => setDeleteTarget(user)}>
                      <DeleteIcon />
                    </IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create User Dialog */}
      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} maxWidth="sm" fullWidth>
        <form onSubmit={createForm.handleSubmit(handleCreate)}>
          <DialogTitle>Add New User</DialogTitle>
          <DialogContent>
            {error && <Alert severity="error" sx={{ mb: 2, mt: 1 }}>{error}</Alert>}
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Stack direction="row" spacing={2}>
                <TextField
                  label="First Name"
                  fullWidth
                  {...createForm.register('firstName')}
                  error={!!createForm.formState.errors.firstName}
                  helperText={createForm.formState.errors.firstName?.message}
                />
                <TextField
                  label="Last Name"
                  fullWidth
                  {...createForm.register('lastName')}
                  error={!!createForm.formState.errors.lastName}
                  helperText={createForm.formState.errors.lastName?.message}
                />
              </Stack>
              <TextField
                label="Email"
                fullWidth
                {...createForm.register('email')}
                error={!!createForm.formState.errors.email}
                helperText={createForm.formState.errors.email?.message}
              />
              <TextField
                label="Password"
                type="password"
                fullWidth
                {...createForm.register('password')}
                error={!!createForm.formState.errors.password}
                helperText={createForm.formState.errors.password?.message}
              />
              <Controller
                name="role"
                control={createForm.control}
                render={({ field }) => (
                  <FormControl fullWidth>
                    <InputLabel>Role</InputLabel>
                    <Select {...field} label="Role">
                      <SelectItem value="User">User</SelectItem>
                      <SelectItem value="Manager">Manager</SelectItem>
                    </Select>
                  </FormControl>
                )}
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained" disabled={createMutation.isPending}>
              {createMutation.isPending ? 'Creating...' : 'Create User'}
            </Button>
          </DialogActions>
        </form>
      </Dialog>

      {/* Edit User Dialog */}
      <Dialog open={!!editUser} onClose={() => setEditUser(null)} maxWidth="sm" fullWidth>
        {editUser && (
          <>
            <DialogTitle>Edit User: {editUser.firstName} {editUser.lastName}</DialogTitle>
            <DialogContent>
              {error && <Alert severity="error" sx={{ mb: 2, mt: 1 }}>{error}</Alert>}
              <Stack spacing={2} sx={{ mt: 1 }}>
                <Typography variant="body2" color="text.secondary">
                  Email: {editUser.email}
                </Typography>
                <FormControl fullWidth>
                  <InputLabel>Role</InputLabel>
                  <Select
                    value={editRole}
                    label="Role"
                    onChange={(e) => setEditRole(e.target.value)}
                  >
                    <SelectItem value="User">User</SelectItem>
                    <SelectItem value="Manager">Manager</SelectItem>
                  </Select>
                </FormControl>
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setEditUser(null)}>Cancel</Button>
              <Button
                variant="contained"
                onClick={handleSaveEdit}
                disabled={updateMutation.isPending}
              >
                {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
              </Button>
            </DialogActions>
          </>
        )}
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <Dialog open={!!deleteTarget} onClose={() => setDeleteTarget(null)}>
        <DialogTitle>Delete User</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to permanently delete the user "{deleteTarget?.firstName} {deleteTarget?.lastName}" ({deleteTarget?.email})?
            This action cannot be undone.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteTarget(null)}>Cancel</Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
            disabled={deleteMutation.isPending}
          >
            {deleteMutation.isPending ? 'Deleting...' : 'Delete'}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={!!successMsg}
        autoHideDuration={3000}
        onClose={() => setSuccessMsg(null)}
        message={successMsg}
      />
    </Box>
  );
}
