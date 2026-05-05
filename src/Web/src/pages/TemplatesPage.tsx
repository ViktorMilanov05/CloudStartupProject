import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
  IconButton,
  CircularProgress,
  Snackbar,
  ToggleButtonGroup,
  ToggleButton,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
  Tooltip,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { templatesApi } from '../api/templates';
import type { Template } from '../types/template';
import { useAuthStore } from '../stores/authStore';

type FilterMode = 'active' | 'inactive' | 'all';

export default function TemplatesPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const [filter, setFilter] = useState<FilterMode>('active');
  const [deleteTarget, setDeleteTarget] = useState<Template | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const isActiveParam = filter === 'all' ? undefined : filter === 'active';

  const { data: templates, isLoading } = useQuery({
    queryKey: ['templates', filter],
    queryFn: () => templatesApi.getAll(isActiveParam).then((r) => r.data),
  });

  const canManage = user?.role === 'Admin' || user?.role === 'Manager';

  const deleteMutation = useMutation({
    mutationFn: (id: string) => templatesApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['templates'] });
      setDeleteTarget(null);
      setSuccessMsg('Template permanently deleted');
    },
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      templatesApi.update(id, { isActive }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['templates'] });
      setSuccessMsg('Template status updated');
    },
  });

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
        <Typography variant="h4">Templates</Typography>
        <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
          <ToggleButtonGroup
            value={filter}
            exclusive
            onChange={(_, v) => v && setFilter(v)}
            size="small"
          >
            <ToggleButton value="active">Active</ToggleButton>
            <ToggleButton value="inactive">Inactive</ToggleButton>
            <ToggleButton value="all">All</ToggleButton>
          </ToggleButtonGroup>
          {canManage && (
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={() => navigate('/templates/new')}
            >
              New Template
            </Button>
          )}
        </Box>
      </Box>

      {templates?.length === 0 ? (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">
            {filter === 'active'
              ? 'No active templates yet. Create your first template to get started.'
              : 'No templates found.'}
          </Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Description</TableCell>
                <TableCell align="center">Steps</TableCell>
                <TableCell>Created By</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Last Updated</TableCell>
                {canManage && <TableCell align="right">Actions</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {templates?.map((template) => (
                <TableRow
                  key={template.id}
                  hover
                  sx={{ cursor: 'pointer' }}
                  onClick={() => {
                    if (canManage) navigate(`/templates/${template.id}/edit`);
                  }}
                >
                  <TableCell>
                    <Typography fontWeight={500}>{template.name}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography
                      variant="body2"
                      color="text.secondary"
                      sx={{
                        maxWidth: 300,
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {template.description || '-'}
                    </Typography>
                  </TableCell>
                  <TableCell align="center">
                    <Chip label={template.stepCount} size="small" variant="outlined" />
                  </TableCell>
                  <TableCell>{template.createdByName}</TableCell>
                  <TableCell>
                    <Chip
                      label={template.isActive ? 'Active' : 'Inactive'}
                      color={template.isActive ? 'success' : 'default'}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>
                    {new Date(template.updatedAt).toLocaleDateString()}
                  </TableCell>
                  {canManage && (
                    <TableCell align="right" onClick={(e) => e.stopPropagation()}>
                      <Tooltip title="Edit">
                        <IconButton
                          size="small"
                          onClick={() => navigate(`/templates/${template.id}/edit`)}
                        >
                          <EditIcon />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={template.isActive ? 'Deactivate' : 'Activate'}>
                        <Chip
                          label={template.isActive ? 'Deactivate' : 'Activate'}
                          size="small"
                          variant="outlined"
                          onClick={() =>
                            toggleActiveMutation.mutate({
                              id: template.id,
                              isActive: !template.isActive,
                            })
                          }
                          sx={{ mx: 0.5, cursor: 'pointer' }}
                        />
                      </Tooltip>
                      <Tooltip title="Delete permanently">
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => setDeleteTarget(template)}
                        >
                          <DeleteIcon />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Delete Confirmation Dialog */}
      <Dialog open={!!deleteTarget} onClose={() => setDeleteTarget(null)}>
        <DialogTitle>Delete Template</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to permanently delete the template "{deleteTarget?.name}"?
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
