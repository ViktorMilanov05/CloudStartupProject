import { useState, useEffect, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Box,
  Typography,
  Button,
  TextField,
  Paper,
  Stack,
  Alert,
  CircularProgress,
  Snackbar,
  IconButton,
  Tooltip,
  Collapse,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import SaveIcon from '@mui/icons-material/Save';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { templatesApi } from '../api/templates';
import type {
  CreateTemplateRequest,
} from '../types/template';
import RichTextEditor from '../components/RichTextEditor';

// ---------- Sortable Step Item ----------

interface SortableStepProps {
  step: LocalStep;
  index: number;
  onUpdate: (id: string, field: 'title' | 'instructions', value: string) => void;
  onRemove: (id: string) => void;
}

function SortableStepItem({ step, index, onUpdate, onRemove }: SortableStepProps) {
  const [expanded, setExpanded] = useState(true);
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: step.id });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };

  return (
    <Paper
      ref={setNodeRef}
      style={style}
      variant="outlined"
      sx={{ mb: 1.5 }}
    >
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          px: 2,
          py: 1,
          gap: 1,
          bgcolor: 'grey.50',
          borderBottom: expanded ? '1px solid' : 'none',
          borderColor: 'divider',
        }}
      >
        <Tooltip title="Drag to reorder">
          <Box
            {...attributes}
            {...listeners}
            sx={{ cursor: 'grab', display: 'flex', alignItems: 'center', color: 'text.secondary' }}
          >
            <DragIndicatorIcon fontSize="small" />
          </Box>
        </Tooltip>
        <Typography variant="body2" color="text.secondary" sx={{ minWidth: 30 }}>
          #{index + 1}
        </Typography>
        <TextField
          size="small"
          placeholder="Step title"
          value={step.title}
          onChange={(e) => onUpdate(step.id, 'title', e.target.value)}
          sx={{ flexGrow: 1 }}
          variant="standard"
          InputProps={{ disableUnderline: !step.title }}
        />
        <IconButton size="small" onClick={() => setExpanded(!expanded)}>
          {expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
        </IconButton>
        <Tooltip title="Remove step">
          <IconButton size="small" color="error" onClick={() => onRemove(step.id)}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>
      <Collapse in={expanded}>
        <Box sx={{ p: 2 }}>
          <Typography variant="caption" color="text.secondary" sx={{ mb: 1, display: 'block' }}>
            Instructions
          </Typography>
          <RichTextEditor
            content={step.instructions || ''}
            onChange={(html) => onUpdate(step.id, 'instructions', html)}
            placeholder="Add step instructions..."
          />
        </Box>
      </Collapse>
    </Paper>
  );
}

// ---------- Local step type ----------

interface LocalStep {
  id: string;
  title: string;
  instructions: string;
  isNew: boolean;
}

let tempIdCounter = 0;
function generateTempId() {
  return `temp-${++tempIdCounter}`;
}

// ---------- Template Editor Page ----------

export default function TemplateEditorPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEditMode = !!id;

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [steps, setSteps] = useState<LocalStep[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [initialized, setInitialized] = useState(false);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  // Load existing template in edit mode
  const { data: template, isLoading } = useQuery({
    queryKey: ['template', id],
    queryFn: () => templatesApi.getById(id!).then((r) => r.data),
    enabled: isEditMode,
  });

  // Initialize form from loaded template
  useEffect(() => {
    if (template && !initialized) {
      setName(template.name);
      setDescription(template.description || '');
      setSteps(
        template.steps.map((s) => ({
          id: s.id,
          title: s.title,
          instructions: s.instructions || '',
          isNew: false,
        })),
      );
      setInitialized(true);
    }
  }, [template, initialized]);

  // Initialize with one empty step for new templates
  useEffect(() => {
    if (!isEditMode && !initialized) {
      setSteps([{ id: generateTempId(), title: '', instructions: '', isNew: true }]);
      setInitialized(true);
    }
  }, [isEditMode, initialized]);

  // Create template mutation
  const createMutation = useMutation({
    mutationFn: (data: CreateTemplateRequest) => templatesApi.create(data),
    onSuccess: (response) => {
      queryClient.invalidateQueries({ queryKey: ['templates'] });
      setSuccessMsg('Template created successfully');
      navigate(`/templates/${response.data.id}/edit`, { replace: true });
    },
    onError: (err: any) => {
      setError(err.response?.data?.errors?.join(', ') || err.response?.data?.detail || 'Failed to create template');
    },
  });

  // Update template metadata mutation
  const updateMutation = useMutation({
    mutationFn: () => templatesApi.update(id!, { name, description: description || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['templates'] });
      queryClient.invalidateQueries({ queryKey: ['template', id] });
    },
    onError: (err: any) => {
      setError(err.response?.data?.errors?.join(', ') || err.response?.data?.detail || 'Failed to update template');
    },
  });

  // Save steps (add/update/delete/reorder) for edit mode
  const saveStepsMutation = useMutation({
    mutationFn: async () => {
      if (!id || !template) return;

      const existingIds = new Set(template.steps.map((s) => s.id));
      const currentIds = new Set(steps.filter((s) => !s.isNew).map((s) => s.id));

      // Delete removed steps
      const deletedIds = [...existingIds].filter((eid) => !currentIds.has(eid));
      for (const deletedId of deletedIds) {
        await templatesApi.deleteStep(id, deletedId);
      }

      // Add new steps
      for (const step of steps) {
        if (step.isNew) {
          const result = await templatesApi.addStep(id, {
            title: step.title,
            instructions: step.instructions || undefined,
          });
          step.id = result.data.id;
          step.isNew = false;
        }
      }

      // Update existing steps
      for (const step of steps) {
        if (!step.isNew) {
          const original = template.steps.find((s) => s.id === step.id);
          if (
            original &&
            (original.title !== step.title || (original.instructions || '') !== step.instructions)
          ) {
            await templatesApi.updateStep(id, step.id, {
              title: step.title,
              instructions: step.instructions || undefined,
            });
          }
        }
      }

      // Reorder
      await templatesApi.reorderSteps(id, {
        stepIds: steps.map((s) => s.id),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['templates'] });
      queryClient.invalidateQueries({ queryKey: ['template', id] });
      setSuccessMsg('Template saved successfully');
    },
    onError: (err: any) => {
      setError(err.response?.data?.errors?.join(', ') || err.response?.data?.detail || 'Failed to save steps');
    },
  });

  const handleSave = useCallback(async () => {
    setError(null);

    // Validate
    if (!name.trim()) {
      setError('Template name is required.');
      return;
    }

    const nonEmptySteps = steps.filter((s) => s.title.trim());
    if (nonEmptySteps.length === 0) {
      setError('At least one step with a title is required.');
      return;
    }

    if (isEditMode) {
      await updateMutation.mutateAsync();
      await saveStepsMutation.mutateAsync();
    } else {
      createMutation.mutate({
        name: name.trim(),
        description: description.trim() || undefined,
        steps: nonEmptySteps.map((s) => ({
          title: s.title.trim(),
          instructions: s.instructions || undefined,
        })),
      });
    }
  }, [name, description, steps, isEditMode, id]);

  const handleUpdateStep = useCallback((stepId: string, field: 'title' | 'instructions', value: string) => {
    setSteps((prev) =>
      prev.map((s) => (s.id === stepId ? { ...s, [field]: value } : s)),
    );
  }, []);

  const handleRemoveStep = useCallback((stepId: string) => {
    setSteps((prev) => prev.filter((s) => s.id !== stepId));
  }, []);

  const handleAddStep = useCallback(() => {
    setSteps((prev) => [
      ...prev,
      { id: generateTempId(), title: '', instructions: '', isNew: true },
    ]);
  }, []);

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      setSteps((prev) => {
        const oldIndex = prev.findIndex((s) => s.id === active.id);
        const newIndex = prev.findIndex((s) => s.id === over.id);
        return arrayMove(prev, oldIndex, newIndex);
      });
    }
  }, []);

  const isSaving =
    createMutation.isPending || updateMutation.isPending || saveStepsMutation.isPending;

  if (isEditMode && isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
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
          {isEditMode ? 'Edit Template' : 'New Template'}
        </Typography>
        <Button
          variant="contained"
          startIcon={<SaveIcon />}
          onClick={handleSave}
          disabled={isSaving}
        >
          {isSaving ? 'Saving...' : 'Save'}
        </Button>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Template Metadata */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Stack spacing={2}>
          <TextField
            label="Template Name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            fullWidth
            required
            inputProps={{ maxLength: 300 }}
          />
          <TextField
            label="Description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            fullWidth
            multiline
            rows={3}
            inputProps={{ maxLength: 4000 }}
          />
        </Stack>
      </Paper>

      {/* Steps Section */}
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h6">
          Steps ({steps.length})
        </Typography>
        <Button
          variant="outlined"
          startIcon={<AddIcon />}
          onClick={handleAddStep}
          size="small"
        >
          Add Step
        </Button>
      </Box>

      <DndContext
        sensors={sensors}
        collisionDetection={closestCenter}
        onDragEnd={handleDragEnd}
      >
        <SortableContext
          items={steps.map((s) => s.id)}
          strategy={verticalListSortingStrategy}
        >
          {steps.map((step, index) => (
            <SortableStepItem
              key={step.id}
              step={step}
              index={index}
              onUpdate={handleUpdateStep}
              onRemove={handleRemoveStep}
            />
          ))}
        </SortableContext>
      </DndContext>

      {steps.length === 0 && (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary" sx={{ mb: 2 }}>
            No steps yet. Add your first step to define the process.
          </Typography>
          <Button variant="outlined" startIcon={<AddIcon />} onClick={handleAddStep}>
            Add Step
          </Button>
        </Paper>
      )}

      <Snackbar
        open={!!successMsg}
        autoHideDuration={3000}
        onClose={() => setSuccessMsg(null)}
        message={successMsg}
      />
    </Box>
  );
}
