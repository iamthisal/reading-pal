import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import ProtectedRoute from './components/ProtectedRoute';

// Placeholders for Pages
const LoginPage = () => <div className="p-8 text-center text-white">Login Page Placeholder</div>;
const HomePage = () => <div className="p-8 text-center text-white">Home Page (Regular User)</div>;
const AdminDashboard = () => <div className="p-8 text-center text-white">Admin Dashboard</div>;

const RootRedirect = () => {
  const { isAuthenticated, user } = useAuth();
  if (!isAuthenticated || !user) return <Navigate to="/login" replace />;
  return user.role === 'Admin' ? <Navigate to="/admin/dashboard" replace /> : <Navigate to="/home" replace />;
};

function App() {
  return (
    <AuthProvider>
      <Router>
        <Routes>
          <Route path="/" element={<RootRedirect />} />
          <Route path="/login" element={<LoginPage />} />
          
          <Route element={<ProtectedRoute allowedRoles={['User']} />}>
            <Route path="/home" element={<HomePage />} />
          </Route>
          
          <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
            <Route path="/admin/dashboard" element={<AdminDashboard />} />
          </Route>
          
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Router>
    </AuthProvider>
  );
}

export default App;
