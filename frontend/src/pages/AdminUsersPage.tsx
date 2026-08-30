import { useEffect, useState } from 'react';
import { useLocation, Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../contexts/AuthContext';
import { Users, ArrowLeft } from 'lucide-react';

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

    useEffect(() => {
        const fetchUsers = async () => {
            setIsLoading(true);
            try {
                const response = await axios.get(`http://localhost:5000${endpoint}`, {
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
                                        <tr key={user.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                                            <td style={{ padding: '1rem' }}>{user.id}</td>
                                            <td style={{ padding: '1rem', fontWeight: 500 }}>{user.firstName} {user.lastName}</td>
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
        </div>
    );
};

export default AdminUsersPage;
