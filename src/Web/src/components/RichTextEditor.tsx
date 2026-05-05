import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Placeholder from '@tiptap/extension-placeholder';
import {
  Box,
  IconButton,
  Divider,
  Tooltip,
  Paper,
} from '@mui/material';
import FormatBoldIcon from '@mui/icons-material/FormatBold';
import FormatItalicIcon from '@mui/icons-material/FormatItalic';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import ImageIcon from '@mui/icons-material/Image';
import UndoIcon from '@mui/icons-material/Undo';
import RedoIcon from '@mui/icons-material/Redo';
import { useRef, useCallback } from 'react';
import { filesApi } from '../api/templates';

interface RichTextEditorProps {
  content: string;
  onChange: (html: string) => void;
  placeholder?: string;
}

export default function RichTextEditor({ content, onChange, placeholder }: RichTextEditorProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);

  const editor = useEditor({
    extensions: [
      StarterKit,
      Image.configure({ inline: false, allowBase64: false }),
      Placeholder.configure({ placeholder: placeholder ?? 'Write instructions...' }),
    ],
    content,
    onUpdate: ({ editor }) => {
      onChange(editor.getHTML());
    },
  });

  const handleImageUpload = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file || !editor) return;

      try {
        const { data } = await filesApi.uploadImage(file);
        editor.chain().focus().setImage({ src: data.url }).run();
      } catch {
        // Upload failed - could show a snackbar, but keeping it simple
      }

      // Reset input so the same file can be re-selected
      if (fileInputRef.current) fileInputRef.current.value = '';
    },
    [editor],
  );

  if (!editor) return null;

  return (
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      {/* Toolbar */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 0.25,
          px: 1,
          py: 0.5,
          borderBottom: '1px solid',
          borderColor: 'divider',
          flexWrap: 'wrap',
        }}
      >
        <Tooltip title="Bold">
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleBold().run()}
            color={editor.isActive('bold') ? 'primary' : 'default'}
          >
            <FormatBoldIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Italic">
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleItalic().run()}
            color={editor.isActive('italic') ? 'primary' : 'default'}
          >
            <FormatItalicIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Divider orientation="vertical" flexItem sx={{ mx: 0.5 }} />
        <Tooltip title="Bullet List">
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleBulletList().run()}
            color={editor.isActive('bulletList') ? 'primary' : 'default'}
          >
            <FormatListBulletedIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Numbered List">
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleOrderedList().run()}
            color={editor.isActive('orderedList') ? 'primary' : 'default'}
          >
            <FormatListNumberedIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Divider orientation="vertical" flexItem sx={{ mx: 0.5 }} />
        <Tooltip title="Insert Image">
          <IconButton size="small" onClick={() => fileInputRef.current?.click()}>
            <ImageIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <input
          type="file"
          ref={fileInputRef}
          hidden
          accept="image/jpeg,image/png,image/gif,image/webp"
          onChange={handleImageUpload}
        />
        <Box sx={{ flexGrow: 1 }} />
        <Tooltip title="Undo">
          <span>
            <IconButton
              size="small"
              onClick={() => editor.chain().focus().undo().run()}
              disabled={!editor.can().undo()}
            >
              <UndoIcon fontSize="small" />
            </IconButton>
          </span>
        </Tooltip>
        <Tooltip title="Redo">
          <span>
            <IconButton
              size="small"
              onClick={() => editor.chain().focus().redo().run()}
              disabled={!editor.can().redo()}
            >
              <RedoIcon fontSize="small" />
            </IconButton>
          </span>
        </Tooltip>
      </Box>

      {/* Editor Content */}
      <Box
        sx={{
          px: 2,
          py: 1.5,
          minHeight: 120,
          '& .tiptap': {
            outline: 'none',
            minHeight: 100,
          },
          '& .tiptap p.is-editor-empty:first-of-type::before': {
            content: 'attr(data-placeholder)',
            float: 'left',
            color: 'text.disabled',
            pointerEvents: 'none',
            height: 0,
          },
          '& .tiptap img': {
            maxWidth: '100%',
            height: 'auto',
            borderRadius: 1,
            my: 1,
          },
        }}
      >
        <EditorContent editor={editor} />
      </Box>
    </Paper>
  );
}
