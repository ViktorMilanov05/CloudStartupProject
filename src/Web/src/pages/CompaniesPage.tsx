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
  DialogActions,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem as SelectItem,
  Stack,
  Alert,
  IconButton,
  Switch,
  FormControlLabel,
  CircularProgress,
  Snackbar,
  Breadcrumbs,
  Link,
} from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import PeopleIcon from '@mui/icons-material/People';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { adminApi } from '../api/admin';
import type { Company } from '../types/company';
import type { User, CreateUserRequest, UpdateUserRequest } from '../types/user';

const createCompanySchema = z.object({
  companyName: z.string().min(1, 'Company name is required').max(200),
  managerEmail: z.string().email('A valid email is required').max(256),
  managerPassword: z.string().min(8, 'Password must be at least 8 characters'),
  managerFirstName: z.string().min(1, 'First name is required').max(100),
  managerLastName: z.string().min(1, 'Last name is required').max(100),
});

const createUserSchema = z.object({
  email: z.string().email('A valid email is required').max(256),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  firstName: z.string().min(1, 'First name is required').max(100),
  lastName: z.string().min(1, 'Last name is required').max(100),
  role: z.enum(['User', 'Manager', 'Admin']),
});

type CreateCompanyFormData = z.infer<typeof createCompanySchema>;
type CreateUserFormData = z.infer<typeof createUserSchema>;

export default function CompaniesPage() {
  const queryClient = useQueryClient();
  const [selectedCompany, setSelectedCompany] = useState<Company | null>(null);
  const [createCompanyOpen, setCreateCompanyOpen] = useState(false);
  const [createUserOpen, setCreateUserOpen] = useState(false);
  const [editUser, setEditUser] = useState<User | null>(null);
  const [editRole, setEditRole] = useState<string>('User');
  const [editActive, setEditActive] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  useEffect(() => {
    if (editUser) {
      setEditRole(editUser.role);
      setEditActive(editUser.isActive);
      setError(null);
    }
  }, [editUser]);

  const { data: companies, isLoading: companiesLoading } = useQuery({
    queryKey: ['admin-companies'],
    queryFn: () => adminApi.getCompanies().then((r) => r.data),
  });

  const { data: companyUsers, isLoading: usersLoading } = useQuery({
    queryKey: ['admin-company-users', selectedCompany?.id],
    queryFn: () => adminApi.getCompanyUsers(selectedCompany!.id).then((r) => r.data),
    enabled: !!selectedCompany,
  });

  const createCompanyMutation = useMutation({
    mutationFn: (data: CreateCompanyFormData) => adminApi.createCompany(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-companies'] });
      setCreateCompanyOpen(false);
      setError(null);
      setSuccessMsg('Company created successfully');
    },
    onError: (err: any) => {
      setError(err.response?.data?.detail || err.response?.data?.errors?.[0] || 'Failed to create company');
    },
  });

  const createUserMutation = useMutation({
    mutationFn: (data: CreateUserRequest) => adminApi.createCompanyUser(selectedCompany!.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-company-users', selectedCompany?.id] });
      queryClient.invalidateQueries({ queryKey: ['admin-companies'] });
      setCreateUserOpen(false);
      setError(null);
      setSuccessMsg('User created successfully');
    },
    onError: (err: any) => {
      setError(err.response?.data?.detail || err.response?.data?.errors?.[0] || 'Failed to create user');
    },
  });

  const updateUserMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateUserRequest }) => adminApi.updateUser(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-company-users', selectedCompany?.id] });
      setEditUser(null);
      setError(null);
      setSuccessMsg('User updated successfully');
    },
    onError: (err: any) => {
      setError(err.response?.data?.detail || err.response?.data?.errors?.[0] || 'Failed to update user');
    },
  });

  const companyForm = useForm<CreateCompanyFormData>({
    resolver: zodResolver(createCompanySchema),
    defaultValues: { companyName: '', managerEmail: '', managerPassword: '', managerFirstName: '', managerLastName: '' },
  });

  const userForm = useForm<CreateUserFormData>({
    resolver: zodResolver(createUserSchema),
    defaultValues: { email: '', password: '', firstName: '', lastName: '', role: 'User' },
  });

  const handleCreateCompany = (data: CreateCompanyFormData) => {
    setError(null);
    createCompanyMutation.mutate(data);
  };

  const handleCreateUser = (data: CreateUserFormData) => {
    setError(null);
    createUserMutation.mutate(data);
  };

  const handleSaveEdit = () => {
    if (!editUser) return;
    const data: UpdateUserRequest = {};
    if (editRole !== editUser.role) data.role = editRole;
    if (editActive !== editUser.isActive) data.isActive = editActive;
    if (Object.keys(data).length === 0) {
      setEditUser(null);
      return;
    }
    updateUserMutation.mutate({ id: editUser.id, data });
  };

  const roleColor = (role: string) => {
    switch (role) {
      case 'Admin': return 'error';
      case 'Manager': return 'primary';
      default: return 'default';
    }
  };

  if (companiesLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  // ─── Company users view ────────────────────────────────────────────
  if (selectedCompany) {
    return (
      <Box>
        <Breadcrumbs sx={{ mb: 2 }}>
          <Link
            component="button"
            variant="body1"
            underline="hover"
            onClick={() => setSelectedCompany(null)}
          >
            Companies
          </Link>
          <Typography color="text.primary">{selectedCompany.name}</Typography>
        </Breadcrumbs>

        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <IconButton onClick={() => setSelectedCompany(null)}>
              <ArrowBackIcon />
            </IconButton>
            <Typography variant="h4">{selectedCompany.name} - Users</Typography>
          </Box>
          <Button
            variant="contained"
            onClick={() => {
              userForm.reset();
              setError(null);
              setCreateUserOpen(true);
            }}
          >
            Add User
          </Button>
        </Box>

        {usersLoading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
            <CircularProgress />
          </Box>
        ) : (
          <TableContainer component={Paper}>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Name</TableCell>
                  <TableCell>Email</TableCell>
                  <TableCell>Role</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {companyUsers?.map((user) => (
                  <TableRow key={user.id}>
                    <TableCell>{user.firstName} {user.lastName}</TableCell>
                    <TableCell>{user.email}</TableCell>
                    <TableCell>
                      <Chip label={user.role} color={roleColor(user.role)} size="small" />
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={user.isActive ? 'Active' : 'Inactive'}
                        color={user.isActive ? 'success' : 'default'}
                        size="small"
                      />
                    </TableCell>
                    <TableCell align="right">
                      <IconButton size="small" onClick={() => setEditUser(user)}>
                        <EditIcon />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        {/* Create User Dialog */}
        <Dialog open={createUserOpen} onClose={() => setCreateUserOpen(false)} maxWidth="sm" fullWidth>
          <form onSubmit={userForm.handleSubmit(handleCreateUser)}>
            <DialogTitle>Add User to {selectedCompany.name}</DialogTitle>
            <DialogContent>
              {error && <Alert severity="error" sx={{ mb: 2, mt: 1 }}>{error}</Alert>}
              <Stack spacing={2} sx={{ mt: 1 }}>
                <Stack direction="row" spacing={2}>
                  <TextField
                    label="First Name"
                    fullWidth
                    {...userForm.register('firstName')}
                    error={!!userForm.formState.errors.firstName}
                    helperText={userForm.formState.errors.firstName?.message}
                  />
                  <TextField
                    label="Last Name"
                    fullWidth
                    {...userForm.register('lastName')}
                    error={!!userForm.formState.errors.lastName}
                    helperText={userForm.formState.errors.lastName?.message}
                  />
                </Stack>
                <TextField
                  label="Email"
                  fullWidth
                  {...userForm.register('email')}
                  error={!!userForm.formState.errors.email}
                  helperText={userForm.formState.errors.email?.message}
                />
                <TextField
                  label="Password"
                  type="password"
                  fullWidth
                  {...userForm.register('password')}
                  error={!!userForm.formState.errors.password}
                  helperText={userForm.formState.errors.password?.message}
                />
                <Controller
                  name="role"
                  control={userForm.control}
                  render={({ field }) => (
                    <FormControl fullWidth>
                      <InputLabel>Role</InputLabel>
                      <Select {...field} label="Role">
                        <SelectItem value="User">User</SelectItem>
                        <SelectItem value="Manager">Manager</SelectItem>
                        <SelectItem value="Admin">Admin</SelectItem>
                      </Select>
                    </FormControl>
                  )}
                />
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setCreateUserOpen(false)}>Cancel</Button>
              <Button type="submit" variant="contained" disabled={createUserMutation.isPending}>
                {createUserMutation.isPending ? 'Creating...' : 'Create User'}
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
                      <SelectItem value="Admin">Admin</SelectItem>
                    </Select>
                  </FormControl>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={editActive}
                        onChange={(e) => setEditActive(e.target.checked)}
                      />
                    }
                    label={editActive ? 'Active' : 'Inactive'}
                  />
                </Stack>
              </DialogContent>
              <DialogActions>
                <Button onClick={() => setEditUser(null)}>Cancel</Button>
                <Button
                  variant="contained"
                  onClick={handleSaveEdit}
                  disabled={updateUserMutation.isPending}
                >
                  {updateUserMutation.isPending ? 'Saving...' : 'Save Changes'}
                </Button>
              </DialogActions>
            </>
          )}
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

  // ─── Companies list view ──────────────────────────────────────────
  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Companies</Typography>
        <Button
          variant="contained"
          onClick={() => {
            companyForm.reset();
            setError(null);
            setCreateCompanyOpen(true);
          }}
        >
          Add Company
        </Button>
      </Box>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Users</TableCell>
              <TableCell>Created</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {companies?.map((company) => (
              <TableRow key={company.id} hover sx={{ cursor: 'pointer' }}>
                <TableCell onClick={() => setSelectedCompany(company)}>
                  {company.name}
                </TableCell>
                <TableCell onClick={() => setSelectedCompany(company)}>
                  <Chip label={company.userCount} size="small" />
                </TableCell>
                <TableCell onClick={() => setSelectedCompany(company)}>
                  {new Date(company.createdAt).toLocaleDateString()}
                </TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => setSelectedCompany(company)}>
                    <PeopleIcon />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {companies?.length === 0 && (
              <TableRow>
                <TableCell colSpan={4} align="center">
                  <Typography color="text.secondary" sx={{ py: 4 }}>
                    No companies yet. Click &quot;Add Company&quot; to create one.
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create Company Dialog */}
      <Dialog open={createCompanyOpen} onClose={() => setCreateCompanyOpen(false)} maxWidth="sm" fullWidth>
        <form onSubmit={companyForm.handleSubmit(handleCreateCompany)}>
          <DialogTitle>Create Company</DialogTitle>
          <DialogContent>
            {error && <Alert severity="error" sx={{ mb: 2, mt: 1 }}>{error}</Alert>}
            <Stack spacing={2} sx={{ mt: 1 }}>
              <TextField
                label="Company Name"
                fullWidth
                {...companyForm.register('companyName')}
                error={!!companyForm.formState.errors.companyName}
                helperText={companyForm.formState.errors.companyName?.message}
              />
              <Typography variant="subtitle2" color="text.secondary" sx={{ pt: 1 }}>
                Initial Manager Account
              </Typography>
              <Stack direction="row" spacing={2}>
                <TextField
                  label="First Name"
                  fullWidth
                  {...companyForm.register('managerFirstName')}
                  error={!!companyForm.formState.errors.managerFirstName}
                  helperText={companyForm.formState.errors.managerFirstName?.message}
                />
                <TextField
                  label="Last Name"
                  fullWidth
                  {...companyForm.register('managerLastName')}
                  error={!!companyForm.formState.errors.managerLastName}
                  helperText={companyForm.formState.errors.managerLastName?.message}
                />
              </Stack>
              <TextField
                label="Manager Email"
                fullWidth
                {...companyForm.register('managerEmail')}
                error={!!companyForm.formState.errors.managerEmail}
                helperText={companyForm.formState.errors.managerEmail?.message}
              />
              <TextField
                label="Manager Password"
                type="password"
                fullWidth
                {...companyForm.register('managerPassword')}
                error={!!companyForm.formState.errors.managerPassword}
                helperText={companyForm.formState.errors.managerPassword?.message}
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setCreateCompanyOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained" disabled={createCompanyMutation.isPending}>
              {createCompanyMutation.isPending ? 'Creating...' : 'Create Company'}
            </Button>
          </DialogActions>
        </form>
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
