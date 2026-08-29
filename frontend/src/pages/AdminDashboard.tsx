import { useAuth } from '../contexts/AuthContext';
import { LogOut, Users, ShieldAlert } from 'lucide-react';
import { useEffect, useState } from 'react';
import axios from 'axios';

const AdminDashboard = () => {
    const { logout, token } = useAuth();
    const [adminMessage, setAdminMessage] = useState<string>('');

    useEffect(() => {
        const fetchAdminMessage = async () => {
            try {
                const response = await axios.get('http://localhost:5000/api/admin/message', {
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
