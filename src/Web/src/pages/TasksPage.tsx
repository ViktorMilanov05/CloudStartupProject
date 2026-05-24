import { useState, useCallback, useEffect, useRef } from 'react';
import {
  Box,
  Typography,
  Button,
  ToggleButton,
  ToggleButtonGroup,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  Pagination,
  CircularProgress,
  Alert,
  Chip,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import ViewKanbanIcon from '@mui/icons-material/ViewKanban';
import ViewListIcon from '@mui/icons-material/ViewList';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import { useQuery, useMutation, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { tasksApi } from '../api/tasks';
import { usersApi } from '../api/users';
import { useAuthStore } from '../stores/authStore';
import TaskBoard from '../components/tasks/TaskBoard';
import TaskListView from '../components/tasks/TaskListView';
import TaskCreateDialog from '../components/tasks/TaskCreateDialog';
import type { TaskItemDto, TaskStatus, TaskFilterParams } from '../types/task';

export default function TasksPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const isManager = user?.role === 'Manager' || user?.role === 'Admin';

  const [view, setView] = useState<'board' | 'list'>('board');
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [showOverdueOnly, setShowOverdueOnly] = useState(false);
  const [searchText, setSearchText] = useState('');
  const [filters, setFilters] = useState<TaskFilterParams>({
    page: 1,
    pageSize: 200, // Load more for board view
  });

  // Debounce search input - only update filters after 300ms of no typing
  const debounceRef = useRef<ReturnType<typeof setTimeout>>();
  useEffect(() => {
    debounceRef.current = setTimeout(() => {
      setFilters(prev => {
        const newSearch = searchText || undefined;
        if (prev.search === newSearch) return prev;
        return { ...prev, search: newSearch, page: 1 };
      });
    }, 300);
    return () => clearTimeout(debounceRef.current);
  }, [searchText]);

  const { data, isLoading, error } = useQuery({
    queryKey: ['tasks', filters],
    queryFn: () => tasksApi.getAll(filters),
    placeholderData: keepPreviousData,
  });

  // Fetch users list for assignee filter (managers only)
  const { data: usersData } = useQuery({
    queryKey: ['users'],
    queryFn: () => usersApi.getAll().then(r => r.data),
    enabled: isManager,
  });

  const statusMutation = useMutation({
    mutationFn: ({ taskId, status }: { taskId: string; status: string }) =>
      tasksApi.update(taskId, { status }),
    onMutate: async ({ taskId, status }) => {
      await queryClient.cancelQueries({ queryKey: ['tasks', filters] });
      const previous = queryClient.getQueryData(['tasks', filters]);
      queryClient.setQueryData(['tasks', filters], (old: typeof data) => {
        if (!old?.data?.items) return old;
        return {
          ...old,
          data: {
            ...old.data,
            items: old.data.items.map((t: TaskItemDto) =>
              t.id === taskId ? { ...t, status } : t
            ),
          },
        };
      });
      return { previous };
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(['tasks', filters], context.previous);
      }
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['tasks'] }),
  });

  const handleTaskClick = useCallback(
    (task: TaskItemDto) => navigate(`/tasks/${task.id}`),
    [navigate]
  );

  const handleStatusChange = useCallback(
    (taskId: string, newStatus: TaskStatus) => {
      statusMutation.mutate({ taskId, status: newStatus });
    },
    [statusMutation]
  );

  const handleFilterChange = (key: keyof TaskFilterParams, value: string | undefined) => {
    setFilters(prev => ({ ...prev, [key]: value || undefined, page: 1 }));
  };

  const handleSortChange = (field: string) => {
    setFilters(prev => ({
      ...prev,
      sortBy: field,
      sortDescending: prev.sortBy === field ? !prev.sortDescending : false,
    }));
  };

  const tasks = data?.data?.items ?? [];
  const totalPages = data?.data?.totalPages ?? 0;
  const users = usersData ?? [];
  const now = new Date();
  const overdueCount = tasks.filter(t => t.dueDate && new Date(t.dueDate) < now && t.status !== 'Done').length;
  const displayTasks = showOverdueOnly
    ? tasks.filter(t => t.dueDate && new Date(t.dueDate) < now && t.status !== 'Done')
    : tasks;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Typography variant="h4">Tasks</Typography>
          {overdueCount > 0 && (
            <Chip
              icon={<WarningAmberIcon />}
              label={`${overdueCount} Overdue`}
              color="error"
              size="small"
              variant="filled"
            />
          )}
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <ToggleButtonGroup value={view} exclusive onChange={(_, v) => v && setView(v)} size="small">
            <ToggleButton value="board"><ViewKanbanIcon /></ToggleButton>
            <ToggleButton value="list"><ViewListIcon /></ToggleButton>
          </ToggleButtonGroup>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateDialogOpen(true)}>
            New Task
          </Button>
        </Box>
      </Box>

      {/* Filters */}
      <Box sx={{ display: 'flex', gap: 1.5, mb: 2, flexWrap: 'wrap', alignItems: 'center' }}>
        <TextField
          id="task-search"
          label="Search"
          value={searchText}
          onChange={e => setSearchText(e.target.value)}
          size="small"
          sx={{ minWidth: 200 }}
        />
        {view === 'list' && (
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <InputLabel id="status-filter-label">Status</InputLabel>
            <Select
              id="status-filter"
              labelId="status-filter-label"
              value={filters.status ?? ''}
              label="Status"
              onChange={e => handleFilterChange('status', e.target.value)}
            >
              <MenuItem value="">All</MenuItem>
              <MenuItem value="ToDo">To Do</MenuItem>
              <MenuItem value="InProgress">In Progress</MenuItem>
              <MenuItem value="Done">Done</MenuItem>
              <MenuItem value="Blocked">Blocked</MenuItem>
            </Select>
          </FormControl>
        )}
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel id="priority-filter-label">Priority</InputLabel>
          <Select
            id="priority-filter"
            labelId="priority-filter-label"
            value={filters.priority ?? ''}
            label="Priority"
            onChange={e => handleFilterChange('priority', e.target.value)}
          >
            <MenuItem value="">All</MenuItem>
            <MenuItem value="Low">Low</MenuItem>
            <MenuItem value="Medium">Medium</MenuItem>
            <MenuItem value="High">High</MenuItem>
            <MenuItem value="Critical">Critical</MenuItem>
          </Select>
        </FormControl>
        {isManager && (
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="assignee-filter-label">Assignee</InputLabel>
            <Select
              id="assignee-filter"
              labelId="assignee-filter-label"
              value={filters.assigneeId ?? ''}
              label="Assignee"
              onChange={e => handleFilterChange('assigneeId', e.target.value)}
            >
              <MenuItem value="">All</MenuItem>
              {users.filter(u => u.isActive).map(u => (
                <MenuItem key={u.id} value={u.id}>
                  {u.firstName} {u.lastName}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}
        {overdueCount > 0 && (
          <Chip
            label={showOverdueOnly ? 'Show All' : 'Overdue Only'}
            color={showOverdueOnly ? 'error' : 'default'}
            variant={showOverdueOnly ? 'filled' : 'outlined'}
            onClick={() => setShowOverdueOnly(prev => !prev)}
            sx={{ fontWeight: showOverdueOnly ? 600 : 400 }}
          />
        )}
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Failed to load tasks. Please try again.
        </Alert>
      )}

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
          <CircularProgress />
        </Box>
      ) : view === 'board' ? (
        <TaskBoard tasks={displayTasks} onTaskClick={handleTaskClick} onStatusChange={handleStatusChange} />
      ) : (
        <>
          <TaskListView
            tasks={displayTasks}
            onTaskClick={handleTaskClick}
            sortBy={filters.sortBy}
            sortDescending={filters.sortDescending}
            onSortChange={handleSortChange}
          />
          {totalPages > 1 && (
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
              <Pagination
                count={totalPages}
                page={filters.page ?? 1}
                onChange={(_, page) => setFilters(prev => ({ ...prev, page }))}
              />
            </Box>
          )}
        </>
      )}

      <TaskCreateDialog
        open={createDialogOpen}
        onClose={() => setCreateDialogOpen(false)}
        onCreated={() => queryClient.invalidateQueries({ queryKey: ['tasks'] })}
      />
    </Box>
  );
}
