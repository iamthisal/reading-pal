import { useAuth } from '../contexts/AuthContext';
import { LogOut, Users, ShieldAlert } from 'lucide-react';
import { useEffect, useState } from 'react';
import axios from 'axios';
import { Link } from 'react-router-dom';
import { API_BASE_URL } from '../config/api';

const AdminDashboard = () => {
    const { logout, token } = useAuth();
    const [adminMessage, setAdminMessage] = useState<string>('');

    useEffect(() => {
        const fetchAdminMessage = async () => {
            try {
                const response = await axios.get(`${API_BASE_URL}/api/admin/message`, {
                    headers: {
                        Authorization: `Bearer ${token}`
                    }
                });
                setAdminMessage(response.data.message);
            } catch (error) {
                console.error("Failed to fetch admin message:", error);
                setAdminMessage("Failed to load classified message.");
            }
        };

        if (token) {
            fetchAdminMessage();
        }
    }, [token]);

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
                    <p style={{ color: 'var(--text-secondary)', marginBottom: '2rem' }}>
                        This is the protected administration area. From here, you will be able to view registered users and manage the library.
                    </p>
                    
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.5rem', marginBottom: '2rem' }}>
                        <Link to="/admin/users/pending" className="glass-panel" style={{ padding: '1.5rem', textDecoration: 'none', color: 'inherit', display: 'flex', flexDirection: 'column', alignItems: 'center', transition: 'transform 0.2s', border: '1px solid var(--border-color)', cursor: 'pointer' }} onMouseOver={e => e.currentTarget.style.transform = 'translateY(-2px)'} onMouseOut={e => e.currentTarget.style.transform = 'translateY(0)'}>
                            <h3 style={{ marginBottom: '0.5rem', color: 'var(--accent-color)' }}>Pending Requests</h3>
                            <p style={{ textAlign: 'center', color: 'var(--text-secondary)' }}>View and manage newly registered users awaiting approval</p>
                        </Link>
                        
                        <Link to="/admin/users/active" className="glass-panel" style={{ padding: '1.5rem', textDecoration: 'none', color: 'inherit', display: 'flex', flexDirection: 'column', alignItems: 'center', transition: 'transform 0.2s', border: '1px solid var(--border-color)', cursor: 'pointer' }} onMouseOver={e => e.currentTarget.style.transform = 'translateY(-2px)'} onMouseOut={e => e.currentTarget.style.transform = 'translateY(0)'}>
                            <h3 style={{ marginBottom: '0.5rem', color: 'var(--success-color, #10b981)' }}>Active Users</h3>
                            <p style={{ textAlign: 'center', color: 'var(--text-secondary)' }}>View all registered and approved users</p>
                        </Link>
                    </div>
                    
                    <div style={{ padding: '1.5rem', backgroundColor: 'rgba(239, 68, 68, 0.1)', border: '1px solid rgba(239, 68, 68, 0.2)', borderRadius: '8px', display: 'flex', gap: '1rem', alignItems: 'flex-start' }}>
                        <ShieldAlert size={24} color="#ef4444" style={{ flexShrink: 0 }} />
                        <div>
                            <h3 style={{ color: '#ef4444', fontSize: '1.1rem', marginBottom: '0.5rem' }}>Classified Admin Message</h3>
                            <p style={{ color: 'var(--text-primary)', fontStyle: 'italic' }}>
                                {adminMessage ? adminMessage : "Loading secret message..."}
                            </p>
                        </div>
                    </div>
                </div>
            </main>
        </div>
    );
};

export default AdminDashboard;
