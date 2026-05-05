import { Navigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';

type Role = 'Admin' | 'Manager' | 'User';

interface ProtectedRouteProps {
  children: React.ReactNode;
  requiredRole?: Role | Role[];
}

export default function ProtectedRoute({ children, requiredRole }: ProtectedRouteProps) {
  const { isAuthenticated, user } = useAuthStore();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (requiredRole) {
    const allowed = Array.isArray(requiredRole) ? requiredRole : [requiredRole];
    if (!user?.role || !allowed.includes(user.role)) {
      return <Navigate to="/" replace />;
    }
  }

  return <>{children}</>;
}
