import { useAuth } from '../contexts/AuthContext';
import { LogOut, Users } from 'lucide-react';

const AdminDashboard = () => {
    const { logout } = useAuth();

    return (
        <div className="page-container">
            <header className="dashboard-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    <Users size={32} color="var(--accent-color)" />
                    <h1 style={{ fontSize: '1.5rem', fontWeight: 600 }}>Admin Dashboard</h1>
                </div>
                <button onClick={logout} className="btn-outline" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <LogOut size={16} />
                    Sign Out
                </button>
            </header>
            
            <main>
                <div className="glass-panel" style={{ padding: '2rem' }}>
                    <h2 style={{ marginBottom: '1rem' }}>Welcome, Admin</h2>
                    <p style={{ color: 'var(--text-secondary)' }}>
                        This is the protected administration area. From here, you will be able to view registered users and manage the library.
                    </p>
                </div>
            </main>
        </div>
    );
};

export default AdminDashboard;
