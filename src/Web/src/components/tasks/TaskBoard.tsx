import { useEffect, useMemo, useRef, useState } from 'react';
import { Box, Paper, Typography } from '@mui/material';
import {
  DndContext,
  DragOverlay,
  rectIntersection,
  PointerSensor,
  useSensor,
  useSensors,
  useDroppable,
  useDraggable,
  type DragStartEvent,
  type DragEndEvent,
} from '@dnd-kit/core';
import TaskCard from './TaskCard';
import type { TaskItemDto, TaskStatus } from '../../types/task';

const columns: { status: TaskStatus; label: string; color: string }[] = [
  { status: 'ToDo', label: 'To Do', color: '#9e9e9e' },
  { status: 'InProgress', label: 'In Progress', color: '#1976d2' },
  { status: 'Done', label: 'Done', color: '#2e7d32' },
  { status: 'Blocked', label: 'Blocked', color: '#d32f2f' },
];

// Map of valid status transitions
const validTransitions: Record<TaskStatus, TaskStatus[]> = {
  ToDo: ['InProgress', 'Blocked'],
  InProgress: ['ToDo', 'Done', 'Blocked'],
  Blocked: ['ToDo', 'InProgress'],
  Done: ['InProgress'],
};

interface TaskBoardProps {
  tasks: TaskItemDto[];
  onTaskClick: (task: TaskItemDto) => void;
  onStatusChange: (taskId: string, newStatus: TaskStatus) => void;
}

function DraggableTaskCard({ task, onClick, hidden }: { task: TaskItemDto; onClick: (task: TaskItemDto) => void; hidden: boolean }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id: task.id });
  const style = {
    transform: transform ? `translate(${transform.x}px, ${transform.y}px)` : undefined,
    opacity: isDragging || hidden ? 0 : 1,
    cursor: 'grab',
  };

  return (
    <div ref={setNodeRef} style={style} {...attributes} {...listeners}>
      <TaskCard task={task} onClick={onClick} />
    </div>
  );
}

function DroppableColumn({
  status,
  label,
  color,
  tasks,
  onTaskClick,
  isValidTarget,
  hiddenIds,
}: {
  status: TaskStatus;
  label: string;
  color: string;
  tasks: TaskItemDto[];
  onTaskClick: (task: TaskItemDto) => void;
  isValidTarget: boolean | null; // null = no drag active
  hiddenIds: Set<string>;
}) {
  const { setNodeRef, isOver } = useDroppable({ id: status });

  const highlight =
    isOver && isValidTarget
      ? 'rgba(46, 125, 50, 0.08)'
      : isOver && isValidTarget === false
        ? 'rgba(211, 47, 47, 0.06)'
        : 'grey.50';

  const borderStyle =
    isOver && isValidTarget
      ? '2px dashed #2e7d32'
      : isOver && isValidTarget === false
        ? '2px dashed #d32f2f'
        : '1px solid';

  return (
    <Paper
      ref={setNodeRef}
      variant="outlined"
      sx={{
        minWidth: 280,
        flex: 1,
        bgcolor: highlight,
        display: 'flex',
        flexDirection: 'column',
        border: borderStyle,
        borderColor: isOver ? undefined : 'divider',
        transition: 'background-color 0.2s, border 0.2s',
      }}
    >
      <Box
        sx={{
          p: 1.5,
          borderBottom: `3px solid ${color}`,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <Typography variant="subtitle1" fontWeight="bold">
          {label}
        </Typography>
        <Typography
          variant="caption"
          sx={{
            bgcolor: color,
            color: 'white',
            borderRadius: '12px',
            px: 1,
            py: 0.25,
            minWidth: 24,
            textAlign: 'center',
          }}
        >
          {tasks.length}
        </Typography>
      </Box>
      <Box sx={{ p: 1, flex: 1, overflowY: 'auto' }}>
        {tasks.map(task => (
          <DraggableTaskCard key={task.id} task={task} onClick={onTaskClick} hidden={hiddenIds.has(task.id)} />
        ))}
        {tasks.length === 0 && (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 4 }}>
            No tasks
          </Typography>
        )}
      </Box>
    </Paper>
  );
}

export default function TaskBoard({ tasks, onTaskClick, onStatusChange }: TaskBoardProps) {
  const [activeTask, setActiveTask] = useState<TaskItemDto | null>(null);
  const validDropRef = useRef(false);
  const [hiddenIds, setHiddenIds] = useState<Set<string>>(new Set());

  // Clear hidden IDs when tasks data changes (optimistic update has propagated)
  useEffect(() => {
    if (hiddenIds.size > 0) setHiddenIds(new Set());
  }, [tasks]); // eslint-disable-line react-hooks/exhaustive-deps

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } })
  );

  const tasksByStatus = useMemo(() => {
    const grouped: Record<TaskStatus, TaskItemDto[]> = {
      ToDo: [],
      InProgress: [],
      Done: [],
      Blocked: [],
    };
    for (const task of tasks) {
      grouped[task.status]?.push(task);
    }
    // Sort within columns: Critical first, then by due date
    const priorityOrder = { Critical: 0, High: 1, Medium: 2, Low: 3 };
    for (const status of Object.keys(grouped) as TaskStatus[]) {
      grouped[status].sort((a, b) => {
        const pDiff = (priorityOrder[a.priority] ?? 2) - (priorityOrder[b.priority] ?? 2);
        if (pDiff !== 0) return pDiff;
        if (a.dueDate && b.dueDate) return new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime();
        if (a.dueDate) return -1;
        if (b.dueDate) return 1;
        return 0;
      });
    }
    return grouped;
  }, [tasks]);

  const handleDragStart = (event: DragStartEvent) => {
    const task = tasks.find(t => t.id === event.active.id);
    setActiveTask(task ?? null);
  };

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    validDropRef.current = false;

    if (over) {
      const taskId = active.id as string;
      const task = tasks.find(t => t.id === taskId);
      if (task) {
        const targetStatus = over.id as TaskStatus;
        if (targetStatus !== task.status && validTransitions[task.status]?.includes(targetStatus)) {
          validDropRef.current = true;
          setHiddenIds(new Set([taskId]));
          onStatusChange(taskId, targetStatus);
        }
      }
    }

    setActiveTask(null);
  };

  // Determine which columns are valid targets for the currently dragged task
  const getIsValidTarget = (colStatus: TaskStatus): boolean | null => {
    if (!activeTask) return null;
    if (colStatus === activeTask.status) return null;
    return validTransitions[activeTask.status]?.includes(colStatus) ?? false;
  };

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={rectIntersection}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
    >
      <Box sx={{ display: 'flex', gap: 2, overflow: 'auto', pb: 2, minHeight: 400 }}>
        {columns.map(col => (
          <DroppableColumn
            key={col.status}
            status={col.status}
            label={col.label}
            color={col.color}
            tasks={tasksByStatus[col.status]}
            onTaskClick={onTaskClick}
            isValidTarget={getIsValidTarget(col.status)}
            hiddenIds={hiddenIds}
          />
        ))}
      </Box>
      <DragOverlay dropAnimation={validDropRef.current ? null : undefined}>
        {activeTask ? <TaskCard task={activeTask} onClick={() => {}} /> : null}
      </DragOverlay>
    </DndContext>
  );
}
