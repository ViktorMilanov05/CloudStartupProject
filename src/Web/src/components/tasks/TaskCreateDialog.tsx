import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Box,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
  List,
  ListItem,
  ListItemText,
  Alert,
  CircularProgress,
} from '@mui/material';

import { useQuery } from '@tanstack/react-query';
import { tasksApi } from '../../api/tasks';
import { templatesApi } from '../../api/templates';
import { usersApi } from '../../api/users';
import { useAuthStore } from '../../stores/authStore';
import type { CreateTaskRequest, CreateTaskFromTemplateRequest } from '../../types/task';

interface TaskCreateDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}

export default function TaskCreateDialog({ open, onClose, onCreated }: TaskCreateDialogProps) {
  const { user } = useAuthStore();
  const isManager = user?.role === 'Manager' || user?.role === 'Admin';

  const [mode, setMode] = useState<'blank' | 'template'>('blank');
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assigneeIds, setAssigneeIds] = useState<string[]>(user?.id ? [user.id] : []);
  const [priority, setPriority] = useState('Medium');
  const [dueDate, setDueDate] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  // Fetch templates for template mode
  const { data: templatesData } = useQuery({
    queryKey: ['templates', 'active'],
    queryFn: () => templatesApi.getAll(true).then(r => r.data),
    enabled: open,
  });

  // Fetch selected template details
  const { data: templateDetail } = useQuery({
    queryKey: ['template', selectedTemplateId],
    queryFn: () => templatesApi.getById(selectedTemplateId).then(r => r.data),
    enabled: !!selectedTemplateId,
  });

  // Fetch users for manager assignee dropdown
  const { data: users } = useQuery({
    queryKey: ['users'],
    queryFn: () => usersApi.getAll().then(r => r.data),
    enabled: open && isManager,
  });

  useEffect(() => {
    if (open) {
      setMode('blank');
      setSelectedTemplateId('');
      setTitle('');
      setDescription('');
      setAssigneeIds(user?.id ? [user.id] : []);
      setPriority('Medium');
      setDueDate('');
      setError('');
    }
  }, [open, user?.id]);

  const handleSubmit = async () => {
    setSubmitting(true);
    setError('');

    try {
      if (mode === 'blank') {
        if (!title.trim()) {
          setError('Title is required.');
          setSubmitting(false);
          return;
        }
        const data: CreateTaskRequest = {
          title: title.trim(),
          description: description.trim() || undefined,
          assigneeIds,
          priority,
          dueDate: dueDate || undefined,
        };
        await tasksApi.create(data);
      } else {
        if (!selectedTemplateId) {
          setError('Please select a template.');
          setSubmitting(false);
          return;
        }
        const data: CreateTaskFromTemplateRequest = {
          title: title.trim() || undefined,
          description: description.trim() || undefined,
          assigneeIds,
          priority,
          dueDate: dueDate || undefined,
        };
        await tasksApi.createFromTemplate(selectedTemplateId, data);
      }
      onCreated();
      onClose();
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: { errors?: string[] } } })?.response?.data?.errors?.[0] ??
        (err as Error)?.message ??
        'Failed to create task.';
      setError(message);
    } finally {
      setSubmitting(false);
    }
  };

  const templates = templatesData ?? [];

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Create Task</DialogTitle>
      <DialogContent>
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}

          <ToggleButtonGroup
            value={mode}
            exclusive
            onChange={(_, v) => v && setMode(v)}
            size="small"
            fullWidth
          >
            <ToggleButton value="blank">Blank Task</ToggleButton>
            <ToggleButton value="template">From Template</ToggleButton>
          </ToggleButtonGroup>

          {mode === 'template' && (
            <FormControl fullWidth size="small">
              <InputLabel id="create-template-label">Template</InputLabel>
              <Select
                id="create-template"
                labelId="create-template-label"
                value={selectedTemplateId}
                label="Template"
                onChange={e => setSelectedTemplateId(e.target.value)}
              >
                {templates.map(t => (
                  <MenuItem key={t.id} value={t.id}>
                    {t.name} ({t.stepCount} steps)
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          {mode === 'template' && templateDetail && (
            <Box sx={{ bgcolor: 'grey.50', borderRadius: 1, p: 1.5 }}>
              <Typography variant="caption" fontWeight="bold" gutterBottom>
                Template Steps Preview:
              </Typography>
              <List dense disablePadding>
                {templateDetail.steps.map((step, i) => (
                  <ListItem key={step.id} disablePadding sx={{ py: 0.25 }}>
                    <ListItemText
                      primary={`${i + 1}. ${step.title}`}
                      primaryTypographyProps={{ variant: 'body2' }}
                    />
                  </ListItem>
                ))}
              </List>
            </Box>
          )}

          <TextField
            id="create-task-title"
            label={mode === 'template' ? 'Title (optional, defaults to template name)' : 'Title'}
            value={title}
            onChange={e => setTitle(e.target.value)}
            required={mode === 'blank'}
            size="small"
            fullWidth
          />

          <TextField
            id="create-task-description"
            label="Description"
            value={description}
            onChange={e => setDescription(e.target.value)}
            multiline
            rows={3}
            size="small"
            fullWidth
          />

          {isManager ? (
            <FormControl fullWidth size="small">
              <InputLabel id="create-assignees-label">Assignees</InputLabel>
              <Select<string[]>
                id="create-assignees"
                labelId="create-assignees-label"
                multiple
                value={assigneeIds}
                label="Assignees"
                onChange={(e) => setAssigneeIds(e.target.value as string[])}
                renderValue={(selected) => {
                  const names = (users ?? []).filter(u => selected.includes(u.id)).map(u => `${u.firstName} ${u.lastName}`);
                  return names.join(', ');
                }}
              >
                {(users ?? []).filter(u => u.isActive).map(u => (
                  <MenuItem key={u.id} value={u.id}>
                    {u.firstName} {u.lastName} ({u.email})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          ) : (
            <TextField
              id="create-assignee-self"
              label="Assignee"
              value={`${user?.firstName} ${user?.lastName}`}
              disabled
              size="small"
              fullWidth
            />
          )}

          <FormControl fullWidth size="small">
            <InputLabel id="create-priority-label">Priority</InputLabel>
            <Select id="create-priority" labelId="create-priority-label" value={priority} label="Priority" onChange={e => setPriority(e.target.value)}>
              <MenuItem value="Low">Low</MenuItem>
              <MenuItem value="Medium">Medium</MenuItem>
              <MenuItem value="High">High</MenuItem>
              <MenuItem value="Critical">Critical</MenuItem>
            </Select>
          </FormControl>

          <TextField
            id="create-due-date"
            label="Due Date"
            type="date"
            value={dueDate}
            onChange={e => setDueDate(e.target.value)}
            size="small"
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
          />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting}>Cancel</Button>
        <Button onClick={handleSubmit} variant="contained" disabled={submitting}>
          {submitting ? <CircularProgress size={20} /> : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
