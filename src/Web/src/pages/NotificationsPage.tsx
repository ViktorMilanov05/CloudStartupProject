import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box,
  Typography,
  Paper,
  List,
  ListItemButton,
  ListItemText,
  Button,
  Divider,
  CircularProgress,
  Chip,
  IconButton,
} from '@mui/material';
import DoneAllIcon from '@mui/icons-material/DoneAll';
import DeleteIcon from '@mui/icons-material/Delete';
import DeleteSweepIcon from '@mui/icons-material/DeleteSweep';
import { useNotificationStore } from '../stores/notificationStore';

export default function NotificationsPage() {
  const navigate = useNavigate();
  const {
    notifications,
    unreadCount,
    totalCount,
    page,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    deleteNotification,
    deleteAllNotifications,
  } = useNotificationStore();

  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    fetchNotifications(1).finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleLoadMore = async () => {
    setLoading(true);
    await fetchNotifications(page + 1);
    setLoading(false);
  };

  const handleClick = async (n: typeof notifications[0]) => {
    if (!n.isRead) {
      await markAsRead(n.id);
    }
    if (n.taskId) {
      navigate(`/tasks/${n.taskId}`);
    }
  };

  const formatTime = (dateStr: string) => {
    const utc = dateStr.endsWith('Z') ? dateStr : dateStr + 'Z';
    return new Date(utc).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' });
  };

  const typeLabels: Record<string, string> = {
    TaskAssigned: 'Assigned',
    TaskUnassigned: 'Unassigned',
    TaskStatusChanged: 'Status',
    TaskEdited: 'Edited',
    TaskDeleted: 'Deleted',
    StepAdded: 'Step',
    StepCompleted: 'Step Done',
    CommentAdded: 'Comment',
    CommentEdited: 'Comment',
    AttachmentAdded: 'File',
  };

  const typeColors: Record<string, 'default' | 'primary' | 'success' | 'warning' | 'error' | 'info'> = {
    TaskAssigned: 'primary',
    TaskUnassigned: 'warning',
    TaskStatusChanged: 'info',
    TaskEdited: 'default',
    TaskDeleted: 'error',
    StepAdded: 'default',
    StepCompleted: 'success',
    CommentAdded: 'info',
    CommentEdited: 'default',
    AttachmentAdded: 'default',
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">Notifications</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          {unreadCount > 0 && (
            <Button startIcon={<DoneAllIcon />} onClick={markAllAsRead}>
              Mark all read ({unreadCount})
            </Button>
          )}
          {notifications.length > 0 && (
            <Button startIcon={<DeleteSweepIcon />} color="error" onClick={deleteAllNotifications}>
              Delete all
            </Button>
          )}
        </Box>
      </Box>

      <Paper variant="outlined">
        {loading && notifications.length === 0 ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
            <CircularProgress />
          </Box>
        ) : notifications.length === 0 ? (
          <Box sx={{ py: 6, textAlign: 'center' }}>
            <Typography color="text.secondary">No notifications yet</Typography>
          </Box>
        ) : (
          <List disablePadding>
            {notifications.map((n, i) => (
              <Box key={n.id}>
                {i > 0 && <Divider />}
                <ListItemButton
                  onClick={() => handleClick(n)}
                  sx={{
                    bgcolor: n.isRead ? 'transparent' : 'action.hover',
                    borderLeft: n.isRead ? 'none' : '3px solid',
                    borderLeftColor: 'primary.main',
                    py: 1.5,
                  }}
                >
                  <ListItemText
                    primary={
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <Chip
                          label={typeLabels[n.type] ?? n.type}
                          color={typeColors[n.type] ?? 'default'}
                          size="small"
                          variant="outlined"
                        />
                        <Typography
                          variant="body2"
                          fontWeight={n.isRead ? 'normal' : 'bold'}
                          sx={{ wordBreak: 'break-word' }}
                        >
                          {n.message}
                        </Typography>
                      </Box>
                    }
                    secondary={formatTime(n.createdAt)}
                    secondaryTypographyProps={{ variant: 'caption' }}
                  />
                  <IconButton
                    size="small"
                    onClick={(e) => {
                      e.stopPropagation();
                      deleteNotification(n.id);
                    }}
                    sx={{ ml: 1, flexShrink: 0 }}
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </ListItemButton>
              </Box>
            ))}
          </List>
        )}

        {notifications.length < totalCount && (
          <>
            <Divider />
            <Box sx={{ textAlign: 'center', py: 2 }}>
              <Button onClick={handleLoadMore} disabled={loading}>
                {loading ? <CircularProgress size={20} /> : 'Load more'}
              </Button>
            </Box>
          </>
        )}
      </Paper>
    </Box>
  );
}
