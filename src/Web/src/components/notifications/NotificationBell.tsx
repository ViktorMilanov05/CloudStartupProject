import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  IconButton,
  Badge,
  Popover,
  Box,
  Typography,
  List,
  ListItemButton,
  ListItemText,
  Divider,
  Button,
  CircularProgress,
} from '@mui/material';
import NotificationsIcon from '@mui/icons-material/Notifications';
import DoneAllIcon from '@mui/icons-material/DoneAll';
import CloseIcon from '@mui/icons-material/Close';
import { useNotificationStore } from '../../stores/notificationStore';
import { useAuthStore } from '../../stores/authStore';

export default function NotificationBell() {
  const navigate = useNavigate();
  const { accessToken } = useAuthStore();
  const {
    notifications,
    unreadCount,
    totalCount,
    startConnection,
    stopConnection,
    fetchUnreadCount,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    deleteNotification,
  } = useNotificationStore();

  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [loading, setLoading] = useState(false);
  const open = Boolean(anchorEl);

  // Start SignalR connection and fetch initial count
  useEffect(() => {
    if (accessToken) {
      startConnection(accessToken);
      fetchUnreadCount();
    }
    return () => {
      stopConnection();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken]);

  const handleOpen = async (e: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(e.currentTarget);
    setLoading(true);
    await fetchNotifications(1);
    setLoading(false);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleNotificationClick = async (notification: typeof notifications[0]) => {
    if (!notification.isRead) {
      await markAsRead(notification.id);
    }
    handleClose();
    if (notification.taskId) {
      navigate(`/tasks/${notification.taskId}`);
    }
  };

  const handleMarkAllRead = async () => {
    await markAllAsRead();
  };

  const handleViewAll = () => {
    handleClose();
    navigate('/notifications');
  };

  const parseUtc = (dateStr: string) => {
    return new Date(dateStr.endsWith('Z') ? dateStr : dateStr + 'Z');
  };

  const formatTime = (dateStr: string) => {
    const date = parseUtc(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString(undefined, { dateStyle: 'short' });
  };

  return (
    <>
      <IconButton color="inherit" onClick={handleOpen}>
        <Badge badgeContent={unreadCount} color="error" max={99}>
          <NotificationsIcon />
        </Badge>
      </IconButton>

      <Popover
        open={open}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{
          paper: { sx: { width: 380, maxHeight: 480 } },
        }}
      >
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', px: 2, py: 1.5 }}>
          <Typography variant="subtitle1" fontWeight="bold">
            Notifications
          </Typography>
          {unreadCount > 0 && (
            <Button size="small" startIcon={<DoneAllIcon />} onClick={handleMarkAllRead}>
              Mark all read
            </Button>
          )}
        </Box>
        <Divider />

        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress size={24} />
          </Box>
        ) : notifications.length === 0 ? (
          <Box sx={{ py: 4, textAlign: 'center' }}>
            <Typography variant="body2" color="text.secondary">
              No notifications yet
            </Typography>
          </Box>
        ) : (
          <>
            <List dense disablePadding sx={{ maxHeight: 340, overflow: 'auto' }}>
              {notifications.slice(0, 20).map((n) => (
                <ListItemButton
                  key={n.id}
                  onClick={() => handleNotificationClick(n)}
                  sx={{
                    bgcolor: n.isRead ? 'transparent' : 'action.hover',
                    borderLeft: n.isRead ? 'none' : '3px solid',
                    borderLeftColor: 'primary.main',
                  }}
                >
                  <ListItemText
                    primary={n.message}
                    secondary={formatTime(n.createdAt)}
                    primaryTypographyProps={{
                      variant: 'body2',
                      fontWeight: n.isRead ? 'normal' : 'bold',
                      sx: { wordBreak: 'break-word' },
                    }}
                    secondaryTypographyProps={{ variant: 'caption' }}
                  />
                  <IconButton
                    size="small"
                    onClick={(e) => {
                      e.stopPropagation();
                      deleteNotification(n.id);
                    }}
                    sx={{ ml: 0.5, flexShrink: 0 }}
                  >
                    <CloseIcon fontSize="small" />
                  </IconButton>
                </ListItemButton>
              ))}
            </List>
            {totalCount > 20 && (
              <>
                <Divider />
                <Box sx={{ textAlign: 'center', py: 1 }}>
                  <Button size="small" onClick={handleViewAll}>
                    View all notifications
                  </Button>
                </Box>
              </>
            )}
          </>
        )}

        {notifications.length > 0 && totalCount <= 20 && (
          <>
            <Divider />
            <Box sx={{ textAlign: 'center', py: 1 }}>
              <Button size="small" onClick={handleViewAll}>
                View all notifications
              </Button>
            </Box>
          </>
        )}
      </Popover>
    </>
  );
}
