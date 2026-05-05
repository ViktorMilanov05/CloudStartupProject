import { useState } from 'react';
import {
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Checkbox,
  IconButton,
  TextField,
  Box,
  Typography,
  Button,
  Collapse,
  Tooltip,
  Paper,
  Alert,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import AddIcon from '@mui/icons-material/Add';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
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
import type { TaskStep } from '../../types/task';
import RichTextEditor from '../RichTextEditor';

interface TaskStepListProps {
  steps: TaskStep[];
  onToggleComplete: (stepId: string, isCompleted: boolean) => void;
  onAddStep: (title: string, instructions?: string) => Promise<void>;
  onDeleteStep: (stepId: string) => void;
  onReorderSteps?: (stepIds: string[]) => void;
  addStepError?: string | null;
}

// ---------- Sortable Step Item ----------

interface SortableStepItemProps {
  step: TaskStep;
  index: number;
  expandedSteps: Set<string>;
  onToggleExpanded: (stepId: string) => void;
  onToggleComplete: (stepId: string, isCompleted: boolean) => void;
  onDeleteStep: (stepId: string) => void;
}

function SortableStepItem({ step, index, expandedSteps, onToggleExpanded, onToggleComplete, onDeleteStep }: SortableStepItemProps) {
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
    <Box ref={setNodeRef} style={style}>
      <ListItem
        disablePadding
        sx={{
          py: 0.5,
          opacity: step.isCompleted ? 0.7 : 1,
        }}
        secondaryAction={
          <Box sx={{ display: 'flex', gap: 0.5 }}>
            {step.instructions && (
              <IconButton size="small" onClick={() => onToggleExpanded(step.id)}>
                {expandedSteps.has(step.id) ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
              </IconButton>
            )}
            <Tooltip title="Delete step">
              <IconButton size="small" onClick={() => onDeleteStep(step.id)} color="error">
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Box>
        }
      >
        <Tooltip title="Drag to reorder">
          <Box
            {...attributes}
            {...listeners}
            sx={{ cursor: 'grab', display: 'flex', alignItems: 'center', color: 'text.secondary', mr: 0.5 }}
          >
            <DragIndicatorIcon fontSize="small" />
          </Box>
        </Tooltip>
        <ListItemIcon sx={{ minWidth: 36 }}>
          <Checkbox
            edge="start"
            checked={step.isCompleted}
            onChange={() => onToggleComplete(step.id, step.isCompleted)}
            size="small"
          />
        </ListItemIcon>
        <ListItemText
          primary={
            <Typography
              variant="body2"
              sx={{ textDecoration: step.isCompleted ? 'line-through' : 'none' }}
            >
              {index + 1}. {step.title}
            </Typography>
          }
          secondary={
            step.isCompleted && step.completedByName
              ? `Completed by ${step.completedByName}`
              : undefined
          }
        />
      </ListItem>
      {step.instructions && (
        <Collapse in={expandedSteps.has(step.id)}>
          <Box
            sx={{
              pl: 7, pr: 2, pb: 1,
              '& img': { maxWidth: '100%', height: 'auto', borderRadius: 1 },
              '& p': { m: 0, mb: 0.5 },
              '& ul, & ol': { mt: 0, mb: 0.5, pl: 2 },
            }}
          >
            <Typography variant="body2" color="text.secondary" component="div" dangerouslySetInnerHTML={{ __html: step.instructions }} />
          </Box>
        </Collapse>
      )}
    </Box>
  );
}

export default function TaskStepList({ steps, onToggleComplete, onAddStep, onDeleteStep, onReorderSteps, addStepError }: TaskStepListProps) {
  const [addingStep, setAddingStep] = useState(false);
  const [newStepTitle, setNewStepTitle] = useState('');
  const [newStepInstructions, setNewStepInstructions] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [expandedSteps, setExpandedSteps] = useState<Set<string>>(new Set());

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (!over || active.id === over.id || !onReorderSteps) return;

    const oldIndex = steps.findIndex((s) => s.id === active.id);
    const newIndex = steps.findIndex((s) => s.id === over.id);
    if (oldIndex === -1 || newIndex === -1) return;

    const reordered = arrayMove(steps, oldIndex, newIndex);
    onReorderSteps(reordered.map((s) => s.id));
  };

  const handleAddStep = async () => {
    if (!newStepTitle.trim()) return;
    setSubmitting(true);
    try {
      await onAddStep(newStepTitle.trim(), newStepInstructions.trim() || undefined);
      setNewStepTitle('');
      setNewStepInstructions('');
      setAddingStep(false);
    } finally {
      setSubmitting(false);
    }
  };

  const toggleExpanded = (stepId: string) => {
    setExpandedSteps(prev => {
      const next = new Set(prev);
      if (next.has(stepId)) next.delete(stepId);
      else next.add(stepId);
      return next;
    });
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
        <Typography variant="subtitle1" fontWeight="bold">
          Steps ({steps.filter(s => s.isCompleted).length}/{steps.length})
        </Typography>
        <Button size="small" startIcon={<AddIcon />} onClick={() => setAddingStep(true)}>
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
          <List disablePadding>
            {steps.map((step, index) => (
              <SortableStepItem
                key={step.id}
                step={step}
                index={index}
                expandedSteps={expandedSteps}
                onToggleExpanded={toggleExpanded}
                onToggleComplete={onToggleComplete}
                onDeleteStep={onDeleteStep}
              />
            ))}
          </List>
        </SortableContext>
      </DndContext>

      {addingStep && (
        <Paper variant="outlined" sx={{ mt: 1.5 }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              px: 2,
              py: 1,
              gap: 1,
              bgcolor: 'grey.50',
              borderBottom: '1px solid',
              borderColor: 'divider',
            }}
          >
            <Typography variant="body2" color="text.secondary" sx={{ minWidth: 30 }}>
              #{steps.length + 1}
            </Typography>
            <TextField
              size="small"
              placeholder="Step title"
              value={newStepTitle}
              onChange={e => setNewStepTitle(e.target.value)}
              sx={{ flexGrow: 1 }}
              variant="standard"
              autoFocus
              InputProps={{ disableUnderline: false }}
            />
            <Tooltip title="Cancel">
              <IconButton size="small" onClick={() => { setAddingStep(false); setNewStepTitle(''); setNewStepInstructions(''); }}>
                <ExpandLessIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Box>
          <Box sx={{ p: 2 }}>
            <Typography variant="caption" color="text.secondary" sx={{ mb: 1, display: 'block' }}>
              Instructions
            </Typography>
            <RichTextEditor
              content={newStepInstructions}
              onChange={setNewStepInstructions}
              placeholder="Add step instructions..."
            />
            <Box sx={{ mb: 1.5 }} />
            {addStepError && (
              <Alert severity="error" sx={{ mb: 1.5 }}>{addStepError}</Alert>
            )}
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button size="small" variant="contained" onClick={handleAddStep} disabled={!newStepTitle.trim() || submitting}>
                {submitting ? 'Adding...' : 'Add Step'}
              </Button>
              <Button size="small" onClick={() => { setAddingStep(false); setNewStepTitle(''); setNewStepInstructions(''); }} disabled={submitting}>
                Cancel
              </Button>
            </Box>
          </Box>
        </Paper>
      )}

      {steps.length === 0 && !addingStep && (
        <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', py: 2 }}>
          No steps yet. Click "Add Step" to get started.
        </Typography>
      )}
    </Box>
  );
}
