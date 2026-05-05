import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Box,
  Typography,
  Paper,
  Chip,
  Button,
  IconButton,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Divider,
  CircularProgress,
  Alert,
  Breadcrumbs,
  Link,
  LinearProgress,
  Avatar,
  AvatarGroup,
  Tooltip,
} from '@mui/material';
import type { SelectChangeEvent } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import EditIcon from '@mui/icons-material/Edit';
import SaveIcon from '@mui/icons-material/Save';
import CancelIcon from '@mui/icons-material/Cancel';
import DeleteIcon from '@mui/icons-material/Delete';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { tasksApi } from '../api/tasks';
import { usersApi } from '../api/users';
import { useAuthStore } from '../stores/authStore';
import TaskStepList from '../components/tasks/TaskStepList';
import TaskComments from '../components/tasks/TaskComments';
import TaskAttachmentList from '../components/tasks/TaskAttachmentList';
import ConfirmDialog from '../components/common/ConfirmDialog';
import type { TaskStatus, TaskPriority } from '../types/task';

const statusLabels: Record<TaskStatus, string> = {
  ToDo: 'To Do',
  InProgress: 'In Progress',
  Done: 'Done',
  Blocked: 'Blocked',
};

const statusColors: Record<TaskStatus, 'default' | 'primary' | 'success' | 'error'> = {
  ToDo: 'default',
  InProgress: 'primary',
  Done: 'success',
  Blocked: 'error',
};

const priorityColors: Record<TaskPriority, 'default' | 'info' | 'warning' | 'error'> = {
  Low: 'default',
  Medium: 'info',
  High: 'warning',
  Critical: 'error',
};

const validTransitions: Record<TaskStatus, TaskStatus[]> = {
  ToDo: ['InProgress', 'Blocked'],
  InProgress: ['ToDo', 'Done', 'Blocked'],
  Blocked: ['ToDo', 'InProgress'],
  Done: ['InProgress'],
};

export default function TaskDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const isManager = user?.role === 'Manager' || user?.role === 'Admin';

  const [editing, setEditing] = useState(false);
  const [editTitle, setEditTitle] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editPriority, setEditPriority] = useState('');
  const [editDueDate, setEditDueDate] = useState('');
  const [editAssigneeIds, setEditAssigneeIds] = useState<string[]>([]);
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ['task', id],
    queryFn: () => tasksApi.getById(id!),
    enabled: !!id,
  });

  const { data: usersData } = useQuery({
    queryKey: ['users'],
    queryFn: () => usersApi.getAll().then(r => r.data),
    enabled: isManager,
  });

  const updateMutation = useMutation({
    mutationFn: (data: Parameters<typeof tasksApi.update>[1]) =>
      tasksApi.update(id!, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['task', id] });
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
      setEditing(false);
    },
  });

  const addStepMutation = useMutation({
    mutationFn: (data: { title: string; instructions?: string }) =>
      tasksApi.addStep(id!, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['task', id] });
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  const completeStepMutation = useMutation({
    mutationFn: (stepId: string) => tasksApi.completeStep(id!, stepId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['task', id] });
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  const uncompleteStepMutation = useMutation({
    mutationFn: (stepId: string) => tasksApi.uncompleteStep(id!, stepId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['task', id] });
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  const deleteStepMutation = useMutation({
    mutationFn: (stepId: string) => tasksApi.deleteStep(id!, stepId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['task', id] }),
  });

  const reorderStepsMutation = useMutation({
    mutationFn: (stepIds: string[]) => tasksApi.reorderSteps(id!, { stepIds }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['task', id] });
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  const deleteTaskMutation = useMutation({
    mutationFn: () => tasksApi.delete(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
      navigate('/tasks');
    },
  });

  const task = data?.data;
  const users = usersData ?? [];

  const startEditing = () => {
    if (!task) return;
    setEditTitle(task.title);
    setEditDescription(task.description ?? '');
    setEditPriority(task.priority);
    setEditDueDate(task.dueDate ? task.dueDate.split('T')[0] : '');
    setEditAssigneeIds(task.assignees.map(a => a.id));
    setEditing(true);
  };

  const handleSave = () => {
    if (!task) return;
    const currentIds = task.assignees.map(a => a.id).sort().join(',');
    const editIds = [...editAssigneeIds].sort().join(',');
    updateMutation.mutate({
      title: editTitle !== task.title ? editTitle : undefined,
      description: editDescription !== (task.description ?? '') ? editDescription : undefined,
      priority: editPriority !== task.priority ? editPriority : undefined,
      dueDate: editDueDate !== (task.dueDate?.split('T')[0] ?? '') ? (editDueDate || undefined) : undefined,
      assigneeIds: editIds !== currentIds ? editAssigneeIds : undefined,
    });
  };

  const handleStatusChange = (newStatus: TaskStatus) => {
    updateMutation.mutate({ status: newStatus });
  };

  const handleToggleComplete = (stepId: string, isCompleted: boolean) => {
    if (isCompleted) {
      uncompleteStepMutation.mutate(stepId);
    } else {
      completeStepMutation.mutate(stepId);
    }
  };

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !task) {
    return (
      <Box>
        <Alert severity="error">Task not found or you don't have access.</Alert>
        <Button sx={{ mt: 2 }} onClick={() => navigate('/tasks')}>Back to Tasks</Button>
      </Box>
    );
  }

  const progress = task.stepCount > 0 ? (task.completedStepCount / task.stepCount) * 100 : 0;
  const isOverdue = task.dueDate && new Date(task.dueDate) < new Date() && task.status !== 'Done';
  const allowedTransitions = validTransitions[task.status] ?? [];

  return (
    <Box>
      <Breadcrumbs sx={{ mb: 2 }}>
        <Link
          component="button"
          underline="hover"
          onClick={() => navigate('/tasks')}
          sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}
        >
          <ArrowBackIcon fontSize="small" />
          Tasks
        </Link>
        <Typography color="text.primary">{task.title}</Typography>
      </Breadcrumbs>

      {updateMutation.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {(updateMutation.error as { response?: { data?: { errors?: string[] } } })?.response?.data?.errors?.[0] ??
            'Failed to update task.'}
        </Alert>
      )}

      <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
        {/* Left: Task info + Steps */}
        <Box sx={{ flex: 2, minWidth: 400 }}>
          <Paper variant="outlined" sx={{ p: 3, mb: 2 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 2 }}>
              {editing ? (
                <TextField
                  value={editTitle}
                  onChange={e => setEditTitle(e.target.value)}
                  size="small"
                  fullWidth
                  sx={{ mr: 2 }}
                />
              ) : (
                <Typography variant="h5">{task.title}</Typography>
              )}
              <Box sx={{ display: 'flex', gap: 0.5, flexShrink: 0 }}>
                {editing ? (
                  <>
                    <IconButton onClick={handleSave} color="primary" disabled={updateMutation.isPending}>
                      <SaveIcon />
                    </IconButton>
                    <IconButton onClick={() => setEditing(false)}>
                      <CancelIcon />
                    </IconButton>
                  </>
                ) : (
                  <>
                    <IconButton onClick={startEditing}>
                      <EditIcon />
                    </IconButton>
                    {isManager && (
                      <Tooltip title="Delete task">
                        <span>
                          <IconButton
                            color="error"
                            onClick={() => setDeleteConfirmOpen(true)}
                            disabled={deleteTaskMutation.isPending}
                          >
                            <DeleteIcon />
                          </IconButton>
                        </span>
                      </Tooltip>
                    )}
                  </>
                )}
              </Box>
            </Box>

            {/* Status + Quick Transitions */}
            <Box sx={{ display: 'flex', gap: 1, mb: 2, flexWrap: 'wrap', alignItems: 'center' }}>
              <Chip label={statusLabels[task.status]} color={statusColors[task.status]} />
              {allowedTransitions.length > 0 && (
                <>
                  <Typography variant="caption" color="text.secondary">→</Typography>
                  {allowedTransitions.map(s => (
                    <Chip
                      key={s}
                      label={statusLabels[s]}
                      variant="outlined"
                      color={statusColors[s]}
                      size="small"
                      clickable
                      onClick={() => handleStatusChange(s)}
                    />
                  ))}
                </>
              )}
            </Box>

            {editing ? (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <TextField
                  label="Description"
                  value={editDescription}
                  onChange={e => setEditDescription(e.target.value)}
                  multiline
                  rows={4}
                  size="small"
                  fullWidth
                />
                <FormControl size="small">
                  <InputLabel>Priority</InputLabel>
                  <Select value={editPriority} label="Priority" onChange={e => setEditPriority(e.target.value)}>
                    <MenuItem value="Low">Low</MenuItem>
                    <MenuItem value="Medium">Medium</MenuItem>
                    <MenuItem value="High">High</MenuItem>
                    <MenuItem value="Critical">Critical</MenuItem>
                  </Select>
                </FormControl>
                <TextField
                  label="Due Date"
                  type="date"
                  value={editDueDate}
                  onChange={e => setEditDueDate(e.target.value)}
                  size="small"
                  slotProps={{ inputLabel: { shrink: true } }}
                />
                {isManager && (
                  <FormControl size="small">
                    <InputLabel>Assignees</InputLabel>
                    <Select
                      multiple
                      value={editAssigneeIds}
                      label="Assignees"
                      onChange={(e: SelectChangeEvent<string[]>) => setEditAssigneeIds(e.target.value as string[])}
                      renderValue={(selected) => {
                        const names = users.filter(u => selected.includes(u.id)).map(u => `${u.firstName} ${u.lastName}`);
                        return names.join(', ');
                      }}
                    >
                      {users.filter(u => u.isActive).map(u => (
                        <MenuItem key={u.id} value={u.id}>
                          {u.firstName} {u.lastName}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              </Box>
            ) : (
              <>
                {task.description && (
                  <Typography variant="body1" sx={{ mb: 2, whiteSpace: 'pre-wrap' }}>
                    {task.description}
                  </Typography>
                )}
              </>
            )}
          </Paper>

          {/* Steps */}
          <Paper variant="outlined" sx={{ p: 3 }}>
            {task.stepCount > 0 && (
              <Box sx={{ mb: 2 }}>
                <LinearProgress variant="determinate" value={progress} sx={{ height: 8, borderRadius: 4, mb: 0.5 }} />
                <Typography variant="caption" color="text.secondary">
                  {task.completedStepCount} of {task.stepCount} steps completed ({Math.round(progress)}%)
                </Typography>
              </Box>
            )}
            <TaskStepList
              steps={task.steps}
              onToggleComplete={handleToggleComplete}
              onAddStep={async (title, instructions) => { await addStepMutation.mutateAsync({ title, instructions }); }}
              onDeleteStep={stepId => deleteStepMutation.mutate(stepId)}
              onReorderSteps={stepIds => reorderStepsMutation.mutate(stepIds)}
              addStepError={
                addStepMutation.isError
                  ? ((addStepMutation.error as { response?: { data?: { errors?: string[] } } })?.response?.data?.errors?.[0] ?? 'Failed to add step.')
                  : null
              }
            />
          </Paper>

          {/* Attachments */}
          <Paper variant="outlined" sx={{ p: 3, mt: 2 }}>
            <TaskAttachmentList
              taskId={id!}
              attachments={task.attachments ?? []}
              showUpload
            />
          </Paper>

          {/* Comments */}
          <Paper variant="outlined" sx={{ p: 3, mt: 2 }}>
            <TaskComments
              taskId={id!}
              comments={task.comments ?? []}
            />
          </Paper>
        </Box>

        {/* Right: Metadata */}
        <Box sx={{ flex: 1, minWidth: 260 }}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" gutterBottom>Details</Typography>
            <Divider sx={{ mb: 1.5 }} />

            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              <Box>
                <Typography variant="caption" color="text.secondary">Priority</Typography>
                <Box>
                  <Chip label={task.priority} color={priorityColors[task.priority]} size="small" variant="outlined" />
                </Box>
              </Box>

              <Box>
                <Typography variant="caption" color="text.secondary">Assignees</Typography>
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5, mt: 0.5 }}>
                  {task.assignees.map(a => (
                    <Box key={a.id} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Avatar sx={{ width: 24, height: 24, fontSize: 11 }}>
                        {a.name.split(' ').map(n => n[0]).join('')}
                      </Avatar>
                      <Typography variant="body2">{a.name}</Typography>
                    </Box>
                  ))}
                </Box>
              </Box>

              <Box>
                <Typography variant="caption" color="text.secondary">Created By</Typography>
                <Typography variant="body2">{task.createdByName}</Typography>
              </Box>

              <Box>
                <Typography variant="caption" color="text.secondary">Due Date</Typography>
                <Typography variant="body2" color={isOverdue ? 'error' : 'text.primary'}>
                  {task.dueDate ? new Date(task.dueDate).toLocaleDateString() : 'Not set'}
                </Typography>
              </Box>

              {task.sourceTemplateName && (
                <Box>
                  <Typography variant="caption" color="text.secondary">Template</Typography>
                  <Typography variant="body2">{task.sourceTemplateName}</Typography>
                </Box>
              )}

              <Divider />

              <Box>
                <Typography variant="caption" color="text.secondary">Created</Typography>
                <Typography variant="body2">{new Date(task.createdAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}</Typography>
              </Box>

              <Box>
                <Typography variant="caption" color="text.secondary">Last Updated</Typography>
                <Typography variant="body2">{new Date(task.updatedAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}</Typography>
              </Box>
            </Box>
          </Paper>
        </Box>
      </Box>

      <ConfirmDialog
        open={deleteConfirmOpen}
        title="Delete Task"
        message={`Are you sure you want to permanently delete the task "${task.title}"? This action cannot be undone.`}
        loading={deleteTaskMutation.isPending}
        onConfirm={() => {
          deleteTaskMutation.mutate();
          setDeleteConfirmOpen(false);
        }}
        onCancel={() => setDeleteConfirmOpen(false)}
      />
    </Box>
  );
}
