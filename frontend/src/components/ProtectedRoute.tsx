import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

interface ProtectedRouteProps {
    allowedRoles?: ('Admin' | 'User')[];
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ allowedRoles }) => {
    const { isAuthenticated, user } = useAuth();

    if (!isAuthenticated || !user) {
        return <Navigate to="/login" replace />;
    }

    if (allowedRoles && !allowedRoles.includes(user.role)) {
        // Redirect to a default home page depending on role if they try to access an unauthorized route
        if (user.role === 'Admin') {
            return <Navigate to="/admin/dashboard" replace />;
        }
        return <Navigate to="/home" replace />;
    }

    return <Outlet />;
};

export default ProtectedRoute;
