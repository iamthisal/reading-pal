import { useEffect, useState } from 'react';
import { useLocation, Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../contexts/AuthContext';
import { Users, ArrowLeft } from 'lucide-react';
import { API_BASE_URL } from '../config/api';

interface UserSummary {
    id: number;
    firstName: string;
    lastName: string;
    email: string;
    isValidated: boolean;
    createdAt: string;
}

const AdminUsersPage = () => {
    const { token } = useAuth();
    const location = useLocation();
    
    // Determine mode based on URL
    const isPending = location.pathname.includes('pending');
    const endpoint = isPending ? '/api/admin/users/pending' : '/api/admin/users/active';
    const pageTitle = isPending ? 'Pending Approvals' : 'Active Users';
    const pageDescription = isPending 
        ? 'These users have registered but their accounts have not yet been validated by an administrator.'
        : 'These are all the users who have been successfully registered and validated.';

    const [users, setUsers] = useState<UserSummary[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [selectedUser, setSelectedUser] = useState<UserSummary | null>(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [actionLoading, setActionLoading] = useState(false);

    useEffect(() => {
        const fetchUsers = async () => {
            setIsLoading(true);
            try {
                const response = await axios.get(`${API_BASE_URL}${endpoint}`, {
                    headers: { Authorization: `Bearer ${token}` }
                });
                setUsers(response.data);
                setError('');
            } catch (err) {
                console.error('Failed to fetch users:', err);
                setError('Failed to load user list.');
            } finally {
                setIsLoading(false);
            }
        };

        if (token) {
            fetchUsers();
        }
    }, [endpoint, token]);

    const openModal = (user: UserSummary) => {
        setSelectedUser(user);
        setIsModalOpen(true);
    };

    const closeModal = () => {
        setSelectedUser(null);
        setIsModalOpen(false);
    };

    const handleAccept = async () => {
        if (!selectedUser) return;
        setActionLoading(true);
        try {
            await axios.post(`${API_BASE_URL}/api/admin/users/${selectedUser.id}/accept`, {}, {
                headers: { Authorization: `Bearer ${token}` }
            });
            // Remove user from list
            setUsers(prev => prev.filter(u => u.id !== selectedUser.id));
            closeModal();
        } catch (err) {
            console.error('Failed to accept user:', err);
            alert('Failed to accept user. Please try again.');
        } finally {
            setActionLoading(false);
        }
    };

    const handleReject = async () => {
        if (!selectedUser) return;
        setActionLoading(true);
        try {
            await axios.post(`${API_BASE_URL}/api/admin/users/${selectedUser.id}/reject`, {}, {
                headers: { Authorization: `Bearer ${token}` }
            });
            // Remove user from list
            setUsers(prev => prev.filter(u => u.id !== selectedUser.id));
            closeModal();
        } catch (err) {
            console.error('Failed to reject user:', err);
            alert('Failed to reject user. Please try again.');
        } finally {
            setActionLoading(false);
        }
    };

    return (
        <div className="page-container">
            <header className="dashboard-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    <Users size={32} color="var(--accent-color)" />
                    <h1 style={{ fontSize: '1.5rem', fontWeight: 600 }}>{pageTitle}</h1>
                </div>
                <Link to="/admin/dashboard" className="btn-outline" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', textDecoration: 'none' }}>
                    <ArrowLeft size={16} />
                    Back to Dashboard
                </Link>
            </header>
            
            <main style={{ marginTop: '2rem' }}>
                <div className="glass-panel" style={{ padding: '2rem' }}>
                    <p style={{ color: 'var(--text-secondary)', marginBottom: '1.5rem' }}>{pageDescription}</p>
                    
                    {error && <div className="error-message" style={{ marginBottom: '1rem' }}>{error}</div>}
                    
                    {isLoading ? (
                        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)' }}>Loading users...</p>
                    ) : users.length === 0 ? (
                        <div style={{ padding: '3rem', textAlign: 'center', backgroundColor: 'rgba(0,0,0,0.2)', borderRadius: '8px' }}>
                            <p style={{ color: 'var(--text-secondary)' }}>No users found.</p>
                        </div>
                    ) : (
                        <div style={{ overflowX: 'auto' }}>
                            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                                <thead>
                                    <tr style={{ borderBottom: '1px solid var(--border-color)' }}>
                                        <th style={{ padding: '1rem', color: 'var(--text-secondary)', fontWeight: 500 }}>ID</th>
                                        <th style={{ padding: '1rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Name</th>
                                        <th style={{ padding: '1rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Email</th>
                                        <th style={{ padding: '1rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Status</th>
                                        <th style={{ padding: '1rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Registered Date</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {users.map(user => (
                                        <tr key={user.id} style={{ borderBottom: '1px solid var(--border-color)', transition: 'background-color 0.2s' }}>
                                            <td style={{ padding: '1rem' }}>{user.id}</td>
                                            <td 
                                                style={{ padding: '1rem', fontWeight: 500, cursor: isPending ? 'pointer' : 'default', color: isPending ? 'var(--accent-color)' : 'inherit', textDecoration: isPending ? 'underline' : 'none' }}
                                                onClick={() => isPending ? openModal(user) : null}
                                            >
                                                {user.firstName} {user.lastName}
                                            </td>
                                            <td style={{ padding: '1rem' }}>{user.email}</td>
                                            <td style={{ padding: '1rem' }}>
                                                {user.isValidated ? (
                                                    <span style={{ padding: '0.25rem 0.75rem', borderRadius: '9999px', fontSize: '0.875rem', backgroundColor: 'rgba(16, 185, 129, 0.1)', color: '#10b981' }}>Validated</span>
                                                ) : (
                                                    <span style={{ padding: '0.25rem 0.75rem', borderRadius: '9999px', fontSize: '0.875rem', backgroundColor: 'rgba(245, 158, 11, 0.1)', color: '#f59e0b' }}>Pending</span>
                                                )}
                                            </td>
                                            <td style={{ padding: '1rem', color: 'var(--text-secondary)' }}>
                                                {new Date(user.createdAt).toLocaleDateString()}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            </main>

            {/* Modal Overlay */}
            {isModalOpen && selectedUser && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
                    <div className="glass-panel" style={{ width: '90%', maxWidth: '500px', padding: '2rem', display: 'flex', flexDirection: 'column', gap: '1.5rem', boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.5)' }}>
                        <div>
                            <h2 style={{ fontSize: '1.5rem', marginBottom: '0.5rem', color: 'var(--text-primary)' }}>User Details</h2>
                            <p style={{ color: 'var(--text-secondary)' }}>Review the pending registration details.</p>
                        </div>
                        
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', backgroundColor: 'rgba(0,0,0,0.2)', padding: '1.5rem', borderRadius: '8px' }}>
                            <div>
                                <span style={{ color: 'var(--text-secondary)', fontSize: '0.875rem' }}>Name</span>
                                <div style={{ fontSize: '1.1rem', fontWeight: 500 }}>{selectedUser.firstName} {selectedUser.lastName}</div>
                            </div>
                            <div>
                                <span style={{ color: 'var(--text-secondary)', fontSize: '0.875rem' }}>Email Address</span>
                                <div style={{ fontSize: '1.1rem' }}>{selectedUser.email}</div>
                            </div>
                            <div>
                                <span style={{ color: 'var(--text-secondary)', fontSize: '0.875rem' }}>Registration Date</span>
                                <div style={{ fontSize: '1.1rem' }}>{new Date(selectedUser.createdAt).toLocaleDateString()} at {new Date(selectedUser.createdAt).toLocaleTimeString()}</div>
                            </div>
                        </div>

                        <div style={{ display: 'flex', gap: '1rem', marginTop: '1rem', justifyContent: 'flex-end' }}>
                            <button className="btn-outline" onClick={closeModal} disabled={actionLoading}>Cancel</button>
                            <button 
                                onClick={handleReject} 
                                disabled={actionLoading}
                                style={{ padding: '0.75rem 1.5rem', borderRadius: '8px', border: 'none', backgroundColor: '#ef4444', color: 'white', fontWeight: 600, cursor: actionLoading ? 'not-allowed' : 'pointer', opacity: actionLoading ? 0.7 : 1 }}
                            >
                                Reject
                            </button>
                            <button 
                                onClick={handleAccept} 
                                disabled={actionLoading}
                                className="btn-primary" 
                            >
                                {actionLoading ? 'Processing...' : 'Accept User'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default AdminUsersPage;
