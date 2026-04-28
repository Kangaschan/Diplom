import { Navigate, Outlet } from "react-router-dom";
import { useAppSelector } from "../../shared/lib/hooks";

export function PrivateRoute() {
  const isAuthenticated = useAppSelector((state) => state.auth.isAuthenticated);

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
