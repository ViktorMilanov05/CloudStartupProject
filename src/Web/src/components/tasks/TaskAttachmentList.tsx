import { useRef, useState } from 'react';
import {
  Box,
  Typography,
  IconButton,
  Tooltip,
  Chip,
  Button,
  CircularProgress,
  Alert,
} from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import DeleteIcon from '@mui/icons-material/Delete';
import AttachFileIcon from '@mui/icons-material/AttachFile';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import InsertDriveFileIcon from '@mui/icons-material/InsertDriveFile';
import ImageIcon from '@mui/icons-material/Image';
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { tasksApi } from '../../api/tasks';
import { useAuthStore } from '../../stores/authStore';
import ConfirmDialog from '../common/ConfirmDialog';
import type { TaskAttachment } from '../../types/task';

interface TaskAttachmentListProps {
  taskId: string;
  attachments: TaskAttachment[];
  compact?: boolean;
  showUpload?: boolean;
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function getFileIcon(contentType: string) {
  if (contentType.startsWith('image/')) return <ImageIcon fontSize="small" />;
  if (contentType === 'application/pdf') return <PictureAsPdfIcon fontSize="small" />;
  return <InsertDriveFileIcon fontSize="small" />;
}

export default function TaskAttachmentList({
  taskId,
  attachments,
  compact = false,
  showUpload = false,
}: TaskAttachmentListProps) {
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [deleteTarget, setDeleteTarget] = useState<TaskAttachment | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  const uploadMutation = useMutation({
    mutationFn: (file: File) => tasksApi.uploadAttachment(taskId, file),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['task', taskId] }),
  });

  const deleteMutation = useMutation({
    mutationFn: (attachmentId: string) => tasksApi.deleteAttachment(attachmentId, taskId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['task', taskId] }),
  });

  const handleDownload = async (attachment: TaskAttachment) => {
    try {
      const response = await tasksApi.downloadAttachment(attachment.id, taskId);
      const blob = new Blob([response.data]);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = attachment.fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch {
      // Download failed silently
    }
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    uploadMutation.mutate(file);
    e.target.value = '';
  };

  if (compact) {
    return (
      <>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
          {attachments.map((att) => (
            <Chip
              key={att.id}
              icon={getFileIcon(att.contentType)}
              label={att.fileName}
              size="small"
              variant="outlined"
              onClick={() => handleDownload(att)}
              onDelete={
                user?.id === att.uploadedById
                  ? () => { setDeleteTarget(att); setDeleteDialogOpen(true); }
                  : undefined
              }
              sx={{ maxWidth: 200 }}
            />
          ))}
        </Box>
        <ConfirmDialog
          open={deleteDialogOpen}
          title="Delete Attachment"
          message={`Are you sure you want to delete "${deleteTarget?.fileName}"? This action cannot be undone.`}
          loading={deleteMutation.isPending}
          onConfirm={() => {
            if (deleteTarget) deleteMutation.mutate(deleteTarget.id);
            setDeleteDialogOpen(false);
          }}
          onCancel={() => setDeleteDialogOpen(false)}
          onExited={() => setDeleteTarget(null)}
        />
      </>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <AttachFileIcon fontSize="small" />
        <Typography variant="h6">
          Attachments ({attachments.length})
        </Typography>
        {showUpload && (
          <>
            <input
              type="file"
              ref={fileInputRef}
              hidden
              onChange={handleFileSelect}
            />
            <Button
              size="small"
              variant="outlined"
              startIcon={uploadMutation.isPending ? <CircularProgress size={16} /> : <UploadFileIcon />}
              onClick={() => fileInputRef.current?.click()}
              disabled={uploadMutation.isPending}
              sx={{ ml: 'auto' }}
            >
              Upload
            </Button>
          </>
        )}
      </Box>

      {uploadMutation.isError && (
        <Alert severity="error" sx={{ mb: 1 }}>
          {(uploadMutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Failed to upload file.'}
        </Alert>
      )}

      {attachments.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          No attachments.
        </Typography>
      )}

      {attachments.map((att) => (
        <Box
          key={att.id}
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 1.5,
            py: 1,
            px: 1,
            borderRadius: 1,
            '&:hover': { bgcolor: 'action.hover' },
          }}
        >
          {getFileIcon(att.contentType)}
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography variant="body2" noWrap>
              {att.fileName}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {formatFileSize(att.fileSize)} · {att.uploadedByName} · {new Date(att.createdAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}
            </Typography>
          </Box>
          <Tooltip title="Download">
            <IconButton size="small" onClick={() => handleDownload(att)}>
              <DownloadIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          {user?.id === att.uploadedById && (
            <Tooltip title="Delete">
              <span>
                <IconButton
                  size="small"
                  color="error"
                  onClick={() => { setDeleteTarget(att); setDeleteDialogOpen(true); }}
                  disabled={deleteMutation.isPending}
                >
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </span>
            </Tooltip>
          )}
        </Box>
      ))}

      <ConfirmDialog
        open={deleteDialogOpen}
        title="Delete Attachment"
        message={`Are you sure you want to delete "${deleteTarget?.fileName}"? This action cannot be undone.`}
        loading={deleteMutation.isPending}
        onConfirm={() => {
          if (deleteTarget) deleteMutation.mutate(deleteTarget.id);
          setDeleteDialogOpen(false);
        }}
        onCancel={() => setDeleteDialogOpen(false)}
        onExited={() => setDeleteTarget(null)}
      />
    </Box>
  );
}
