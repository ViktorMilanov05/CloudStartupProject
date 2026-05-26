import { useState } from 'react';
import {
  Box,
  Typography,
  Avatar,
  IconButton,
  Tooltip,
  Button,
  Divider,
  CircularProgress,
  Alert,
} from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import SaveIcon from '@mui/icons-material/Save';
import CancelIcon from '@mui/icons-material/Cancel';
import AttachFileIcon from '@mui/icons-material/AttachFile';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { tasksApi } from '../../api/tasks';
import { useAuthStore } from '../../stores/authStore';
import RichTextEditor from '../RichTextEditor';
import TaskAttachmentList from './TaskAttachmentList';
import ConfirmDialog from '../common/ConfirmDialog';
import type { TaskComment } from '../../types/task';

interface TaskCommentsProps {
  taskId: string;
  comments: TaskComment[];
}

export default function TaskComments({ taskId, comments }: TaskCommentsProps) {
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const [newComment, setNewComment] = useState('');
  const [commentKey, setCommentKey] = useState(0);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState('');
  const [uploadingCommentId, setUploadingCommentId] = useState<string | null>(null);
  const [deleteCommentId, setDeleteCommentId] = useState<string | null>(null);

  const addMutation = useMutation({
    mutationFn: (content: string) => tasksApi.addComment(taskId, { content }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['task', taskId] });
      setNewComment('');
      setCommentKey((k) => k + 1);
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ commentId, content }: { commentId: string; content: string }) =>
      tasksApi.updateComment(taskId, commentId, { content }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['task', taskId] });
      setEditingId(null);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (commentId: string) => tasksApi.deleteComment(taskId, commentId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['task', taskId] }),
  });

  const uploadMutation = useMutation({
    mutationFn: ({ file, commentId }: { file: File; commentId: string }) =>
      tasksApi.uploadAttachment(taskId, file, commentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['task', taskId] });
      setUploadingCommentId(null);
    },
  });

  const handleSubmit = () => {
    const trimmed = newComment.replace(/<p><\/p>/g, '').trim();
    if (!trimmed || trimmed === '<p></p>') return;
    addMutation.mutate(newComment);
  };

  const startEdit = (comment: TaskComment) => {
    setEditingId(comment.id);
    setEditContent(comment.content);
  };

  const handleUpdate = () => {
    if (!editingId) return;
    const trimmed = editContent.replace(/<p><\/p>/g, '').trim();
    if (!trimmed || trimmed === '<p></p>') return;
    updateMutation.mutate({ commentId: editingId, content: editContent });
  };

  const handleFileUpload = (commentId: string, e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploadingCommentId(commentId);
    uploadMutation.mutate({ file, commentId });
    e.target.value = '';
  };

  return (
    <Box>
      <Typography variant="h6" gutterBottom>
        Comments ({comments.length})
      </Typography>

      {comments.length === 0 && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          No comments yet. Be the first to comment.
        </Typography>
      )}

      {comments.map((comment) => (
        <Box key={comment.id} sx={{ mb: 2 }}>
          <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'flex-start' }}>
            <Tooltip title={comment.authorName}>
              <Avatar sx={{ width: 32, height: 32, fontSize: 13, mt: 0.5 }}>
                {comment.authorName.split(' ').map((n) => n[0]).join('')}
              </Avatar>
            </Tooltip>
            <Box sx={{ flex: 1, minWidth: 0 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                <Typography variant="subtitle2">{comment.authorName}</Typography>
                <Typography variant="caption" color="text.secondary">
                  {new Date(comment.createdAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}
                </Typography>
                {user?.id === comment.authorId && editingId !== comment.id && (
                  <Box sx={{ ml: 'auto', display: 'flex', gap: 0.25 }}>
                    <input
                      type="file"
                      id={`comment-attach-${comment.id}`}
                      hidden
                      onChange={(e) => handleFileUpload(comment.id, e)}
                    />
                    <Tooltip title="Attach file">
                      <span>
                        <IconButton
                          size="small"
                          onClick={() => document.getElementById(`comment-attach-${comment.id}`)?.click()}
                          disabled={uploadingCommentId === comment.id}
                        >
                          {uploadingCommentId === comment.id ? (
                            <CircularProgress size={16} />
                          ) : (
                            <AttachFileIcon fontSize="small" />
                          )}
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip title="Edit">
                      <IconButton size="small" onClick={() => startEdit(comment)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Delete">
                      <IconButton
                        size="small"
                        color="error"
                        onClick={() => setDeleteCommentId(comment.id)}
                        disabled={deleteMutation.isPending}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Box>
                )}
              </Box>

              {editingId === comment.id ? (
                <Box>
                  <RichTextEditor
                    content={editContent}
                    onChange={setEditContent}
                    placeholder="Edit your comment..."
                  />
                  <Box sx={{ display: 'flex', gap: 1, mt: 1 }}>
                    <Button
                      size="small"
                      variant="contained"
                      startIcon={<SaveIcon />}
                      onClick={handleUpdate}
                      disabled={updateMutation.isPending}
                    >
                      Save
                    </Button>
                    <Button
                      size="small"
                      startIcon={<CancelIcon />}
                      onClick={() => setEditingId(null)}
                    >
                      Cancel
                    </Button>
                  </Box>
                </Box>
              ) : (
                <Box
                  sx={{
                    '& p': { m: 0, mb: 0.5 },
                    '& p:last-child': { mb: 0 },
                    '& ul, & ol': { m: 0, pl: 2.5 },
                    '& img': { maxWidth: '100%', borderRadius: 1 },
                  }}
                  dangerouslySetInnerHTML={{ __html: comment.content }}
                />
              )}

              {comment.attachments.length > 0 && (
                <Box sx={{ mt: 1 }}>
                  <TaskAttachmentList
                    taskId={taskId}
                    attachments={comment.attachments}
                    compact
                  />
                </Box>
              )}
            </Box>
          </Box>
          <Divider sx={{ mt: 2 }} />
        </Box>
      ))}

      {/* Add comment form */}
      <Box sx={{ mt: 2 }}>
        <Typography variant="subtitle2" gutterBottom>
          Add a comment
        </Typography>
        {addMutation.isError && (
          <Alert severity="error" sx={{ mb: 1 }}>
            Failed to add comment.
          </Alert>
        )}
        <RichTextEditor
          key={commentKey}
          content={newComment}
          onChange={setNewComment}
          placeholder="Write a comment..."
        />
        <Button
          variant="contained"
          size="small"
          sx={{ mt: 1 }}
          onClick={handleSubmit}
          disabled={addMutation.isPending}
        >
          {addMutation.isPending ? 'Posting...' : 'Post Comment'}
        </Button>
      </Box>

      <ConfirmDialog
        open={!!deleteCommentId}
        title="Delete Comment"
        message="Are you sure you want to delete this comment? This action cannot be undone."
        loading={deleteMutation.isPending}
        onConfirm={() => {
          if (deleteCommentId) deleteMutation.mutate(deleteCommentId);
          setDeleteCommentId(null);
        }}
        onCancel={() => setDeleteCommentId(null)}
      />
    </Box>
  );
}
