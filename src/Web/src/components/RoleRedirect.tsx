import { Navigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';

export default function RoleRedirect() {
  const user = useAuthStore((s) => s.user);

  if (user?.role === 'Admin') {
    return <Navigate to="/companies" replace />;
  }

  return <Navigate to="/tasks" replace />;
}
