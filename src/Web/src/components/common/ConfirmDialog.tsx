import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
  Button,
} from '@mui/material';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  onExited?: () => void;
}

export default function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Delete',
  cancelLabel = 'Cancel',
  loading = false,
  onConfirm,
  onCancel,
  onExited,
}: ConfirmDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={onCancel}
      TransitionProps={{ onExited }}
    >
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <DialogContentText>{message}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel}>{cancelLabel}</Button>
        <Button
          color="error"
          variant="contained"
          onClick={onConfirm}
          disabled={loading}
        >
          {loading ? 'Deleting...' : confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
