import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  Avatar,
  AvatarGroup,
  Box,
  Typography,
  LinearProgress,
  TableSortLabel,
  Tooltip,
} from '@mui/material';
import type { TaskItemDto, TaskPriority, TaskStatus } from '../../types/task';

const priorityColors: Record<TaskPriority, 'default' | 'info' | 'warning' | 'error'> = {
  Low: 'default',
  Medium: 'info',
  High: 'warning',
  Critical: 'error',
};

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

interface TaskListViewProps {
  tasks: TaskItemDto[];
  onTaskClick: (task: TaskItemDto) => void;
  sortBy?: string;
  sortDescending?: boolean;
  onSortChange: (field: string) => void;
}

export default function TaskListView({ tasks, onTaskClick, sortBy, sortDescending, onSortChange }: TaskListViewProps) {
  const sortableColumns = [
    { field: 'title', label: 'Title' },
    { field: 'status', label: 'Status' },
    { field: 'priority', label: 'Priority' },
    { field: 'assignee', label: 'Assignee' },
    { field: 'duedate', label: 'Due Date' },
    { field: 'createdat', label: 'Created' },
  ];

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead>
          <TableRow>
            {sortableColumns.map(col => (
              <TableCell key={col.field}>
                <TableSortLabel
                  active={sortBy === col.field}
                  direction={sortBy === col.field && sortDescending ? 'desc' : 'asc'}
                  onClick={() => onSortChange(col.field)}
                >
                  {col.label}
                </TableSortLabel>
              </TableCell>
            ))}
            <TableCell>Progress</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {tasks.map(task => {
            const isOverdue = task.dueDate && new Date(task.dueDate) < new Date() && task.status !== 'Done';
            const progress = task.stepCount > 0 ? (task.completedStepCount / task.stepCount) * 100 : 0;

            return (
              <TableRow
                key={task.id}
                hover
                sx={{ cursor: 'pointer' }}
                onClick={() => onTaskClick(task)}
              >
                <TableCell>
                  <Typography variant="body2" fontWeight={500}>
                    {task.title}
                  </Typography>
                  {task.sourceTemplateName && (
                    <Typography variant="caption" color="text.secondary">
                      From: {task.sourceTemplateName}
                    </Typography>
                  )}
                </TableCell>
                <TableCell>
                  <Chip label={statusLabels[task.status]} color={statusColors[task.status]} size="small" />
                </TableCell>
                <TableCell>
                  <Chip label={task.priority} color={priorityColors[task.priority]} size="small" variant="outlined" />
                </TableCell>
                <TableCell>
                  <AvatarGroup max={3} sx={{ justifyContent: 'flex-start', '& .MuiAvatar-root': { width: 24, height: 24, fontSize: 11 } }}>
                    {task.assignees.map(a => (
                      <Tooltip key={a.id} title={a.name}>
                        <Avatar sx={{ width: 24, height: 24, fontSize: 11 }}>
                          {a.name.split(' ').map(n => n[0]).join('')}
                        </Avatar>
                      </Tooltip>
                    ))}
                  </AvatarGroup>
                </TableCell>
                <TableCell>
                  {task.dueDate ? (
                    <Typography variant="body2" color={isOverdue ? 'error' : 'text.primary'}>
                      {new Date(task.dueDate).toLocaleDateString()}
                    </Typography>
                  ) : (
                    <Typography variant="body2" color="text.secondary">—</Typography>
                  )}
                </TableCell>
                <TableCell>
                  <Typography variant="body2">{new Date(task.createdAt).toLocaleDateString()}</Typography>
                </TableCell>
                <TableCell sx={{ minWidth: 120 }}>
                  {task.stepCount > 0 ? (
                    <Box>
                      <Typography variant="caption">
                        {task.completedStepCount}/{task.stepCount}
                      </Typography>
                      <LinearProgress variant="determinate" value={progress} sx={{ height: 4, borderRadius: 2 }} />
                    </Box>
                  ) : (
                    <Typography variant="caption" color="text.secondary">—</Typography>
                  )}
                </TableCell>
              </TableRow>
            );
          })}
          {tasks.length === 0 && (
            <TableRow>
              <TableCell colSpan={7} sx={{ textAlign: 'center', py: 4 }}>
                <Typography color="text.secondary">No tasks found</Typography>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
