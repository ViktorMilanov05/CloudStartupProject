import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CssBaseline, ThemeProvider, createTheme } from '@mui/material';
import LoginPage from './pages/LoginPage';
import SetupPage from './pages/SetupPage';
import TasksPage from './pages/TasksPage';
import TaskDetailPage from './pages/TaskDetailPage';
import TemplatesPage from './pages/TemplatesPage';
import TemplateViewPage from './pages/TemplateViewPage';
import TemplateEditorPage from './pages/TemplateEditorPage';
import UsersPage from './pages/UsersPage';
import CompaniesPage from './pages/CompaniesPage';
import NotificationsPage from './pages/NotificationsPage';
import AppLayout from './components/AppLayout';
import ProtectedRoute from './components/ProtectedRoute';
import RoleRedirect from './components/RoleRedirect';
import { SetupRoute, LoginGuard } from './components/SetupGuard';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
});

const theme = createTheme({
  palette: {
    primary: { main: '#1976d2' },
    secondary: { main: '#dc004e' },
  },
});

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <BrowserRouter>
          <Routes>
            <Route
              path="/setup"
              element={
                <SetupRoute>
                  <SetupPage />
                </SetupRoute>
              }
            />
            <Route
              path="/login"
              element={
                <LoginGuard>
                  <LoginPage />
                </LoginGuard>
              }
            />
            <Route
              element={
                <ProtectedRoute>
                  <AppLayout />
                </ProtectedRoute>
              }
            >
              <Route path="/" element={<RoleRedirect />} />
              <Route path="/tasks" element={<TasksPage />} />
              <Route path="/tasks/:id" element={<TaskDetailPage />} />
              <Route path="/notifications" element={<NotificationsPage />} />
              <Route path="/templates" element={<TemplatesPage />} />
              <Route path="/template/:id" element={<TemplateViewPage/>} />
              <Route
                path="/templates/new"
                element={
                  <ProtectedRoute requiredRole={['Admin', 'Manager']}>
                    <TemplateEditorPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/templates/:id/edit"
                element={
                  <ProtectedRoute requiredRole={['Admin', 'Manager']}>
                    <TemplateEditorPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/users"
                element={
                  <ProtectedRoute requiredRole="Manager">
                    <UsersPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/companies"
                element={
                  <ProtectedRoute requiredRole="Admin">
                    <CompaniesPage />
                  </ProtectedRoute>
                }
              />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

export default App;
