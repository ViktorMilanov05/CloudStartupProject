import { useNavigate, useParams } from 'react-router-dom';
import {
  Box,
  Typography,
  Paper,
  Chip,
  CircularProgress,
  IconButton,
  Tooltip,
  Divider,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import EditIcon from '@mui/icons-material/Edit';
import { useQuery } from '@tanstack/react-query';
import { templatesApi } from '../api/templates';
import { useAuthStore } from '../stores/authStore';

export default function TemplateViewPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuthStore();
  const canManage = user?.role === 'Admin' || user?.role === 'Manager';

  const { data: template, isLoading } = useQuery({
    queryKey: ['template', id],
    queryFn: () => templatesApi.getById(id!).then((r) => r.data),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!template) {
    return (
      <Box sx={{ mt: 4, textAlign: 'center' }}>
        <Typography color="text.secondary">Template not found.</Typography>
      </Box>
    );
  }

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 3 }}>
        <Tooltip title="Back to templates">
          <IconButton onClick={() => navigate('/templates')}>
            <ArrowBackIcon />
          </IconButton>
        </Tooltip>
        <Typography variant="h4" sx={{ flexGrow: 1 }}>
          {template.name}
        </Typography>
        {canManage && (
          <Tooltip title="Edit template">
            <IconButton
              color="primary"
              onClick={() => navigate(`/templates/${id}/edit`)}
            >
              <EditIcon />
            </IconButton>
          </Tooltip>
        )}
      </Box>

      {/* Template Info */}
      <Paper sx={{ p: 3, mb: 3 }}>
        {template.description && (
          <Typography variant="body1" color="text.secondary" sx={{ mb: 2 }}>
            {template.description}
          </Typography>
        )}
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
          <Chip
            label={template.isActive ? 'Active' : 'Inactive'}
            color={template.isActive ? 'success' : 'default'}
            size="small"
          />
          <Typography variant="body2" color="text.secondary">
            Created by: {template.createdByName}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Updated: {new Date(template.updatedAt).toLocaleDateString()}
          </Typography>
        </Box>
      </Paper>

      {/* Steps */}
      <Typography variant="h6" sx={{ mb: 2 }}>
        Steps ({template.steps.length})
      </Typography>

      {template.steps.length === 0 ? (
        <Paper sx={{ p: 3, textAlign: 'center' }}>
          <Typography color="text.secondary">No steps defined.</Typography>
        </Paper>
      ) : (
        template.steps
          .sort((a, b) => a.sortOrder - b.sortOrder)
          .map((step, index) => (
            <Paper key={step.id} variant="outlined" sx={{ mb: 1.5 }}>
              <Box sx={{ px: 2, py: 1.5, bgcolor: 'grey.50', borderBottom: '1px solid', borderColor: 'divider' }}>
                <Typography variant="subtitle2">
                  #{index + 1} - {step.title}
                </Typography>
              </Box>
              {step.instructions && (
                <Box
                  sx={{ px: 2, py: 1.5 }}
                  dangerouslySetInnerHTML={{ __html: step.instructions }}
                />
              )}
            </Paper>
          ))
      )}
    </Box>
  );
}