import { Card, CardContent, CardActionArea, Typography, Box, Chip, LinearProgress, Avatar, AvatarGroup, Tooltip } from '@mui/material';
import AccessTimeIcon from '@mui/icons-material/AccessTime';
import type { TaskItemDto, TaskStatus, TaskPriority } from '../../types/task';

const priorityColors: Record<TaskPriority, 'default' | 'info' | 'warning' | 'error'> = {
  Low: 'default',
  Medium: 'info',
  High: 'warning',
  Critical: 'error',
};

const statusColors: Record<TaskStatus, string> = {
  ToDo: '#9e9e9e',
  InProgress: '#1976d2',
  Done: '#2e7d32',
  Blocked: '#d32f2f',
};

interface TaskCardProps {
  task: TaskItemDto;
  onClick: (task: TaskItemDto) => void;
}

export default function TaskCard({ task, onClick }: TaskCardProps) {
  const isOverdue = task.dueDate && new Date(task.dueDate) < new Date() && task.status !== 'Done';
  const progress = task.stepCount > 0 ? (task.completedStepCount / task.stepCount) * 100 : 0;

  return (
    <Card
      variant="outlined"
      sx={{
        mb: 1,
        borderLeft: `4px solid ${isOverdue ? '#d32f2f' : statusColors[task.status]}`,
        '&:hover': { boxShadow: 2 },
      }}
    >
      <CardActionArea onClick={() => onClick(task)}>
        <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
          <Typography variant="subtitle2" noWrap sx={{ mb: 0.5 }}>
            {task.title}
          </Typography>

          <Box sx={{ display: 'flex', gap: 0.5, mb: 0.5, flexWrap: 'wrap' }}>
            <Chip label={task.priority} color={priorityColors[task.priority]} size="small" variant="outlined" />
            {isOverdue && (
              <Chip label="OVERDUE" color="error" size="small" />
            )}
            {task.sourceTemplateName && (
              <Chip label={task.sourceTemplateName} size="small" variant="outlined" color="secondary" />
            )}
          </Box>

          {task.stepCount > 0 && (
            <Box sx={{ mb: 0.5 }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.25 }}>
                <Typography variant="caption" color="text.secondary">
                  Steps: {task.completedStepCount}/{task.stepCount}
                </Typography>
              </Box>
              <LinearProgress variant="determinate" value={progress} sx={{ height: 4, borderRadius: 2 }} />
            </Box>
          )}

          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mt: 0.5 }}>
            <AvatarGroup max={3} sx={{ '& .MuiAvatar-root': { width: 22, height: 22, fontSize: 11 } }}>
              {task.assignees.map(a => (
                <Tooltip key={a.id} title={a.name}>
                  <Avatar sx={{ width: 22, height: 22, fontSize: 11 }}>
                    {a.name.split(' ').map(n => n[0]).join('')}
                  </Avatar>
                </Tooltip>
              ))}
            </AvatarGroup>

            {task.dueDate && (
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.25 }}>
                <AccessTimeIcon sx={{ fontSize: 14, color: isOverdue ? 'error.main' : 'text.secondary' }} />
                <Typography variant="caption" color={isOverdue ? 'error' : 'text.secondary'}>
                  {new Date(task.dueDate).toLocaleDateString()}
                </Typography>
              </Box>
            )}
          </Box>
        </CardContent>
      </CardActionArea>
    </Card>
  );
}
