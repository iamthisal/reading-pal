import { Users as UsersIcon } from 'lucide-react';
import AppTopbar from '../components/layout/AppTopbar';
import PageBanner from '../components/layout/PageBanner';
import Modal from '../components/Modal/Modal';
import AdminSidebar from '../components/layout/AdminSidebar';
import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../contexts/AuthContext';
import { Users } from 'lucide-react';
import { API_BASE_URL } from '../config/api';

interface UserSummary {
    id: number;
    firstName: string;
    lastName: string;
    email: string;
    isValidated: boolean;
    createdAt: string;
}

export interface BorrowingRecord {
    bookId: string;
    bookTitle: string;
    borrowedDate: string;
    returnedDate: string | null;
}

const generateMockBorrowings = (userId: number): BorrowingRecord[] => {
    // Generate deterministic mock data based on the user's ID
    const mockBooks = [
        "The Great Gatsby", "1984", "To Kill a Mockingbird", "Pride and Prejudice", 
        "The Catcher in the Rye", "Moby Dick", "The Lord of the Rings", "Jane Eyre"
    ];
    
    const borrowings: BorrowingRecord[] = [];
    
    // Add 2 current borrowings
    borrowings.push({
        bookId: `B-${(userId * 7) % 999}`,
        bookTitle: mockBooks[userId % mockBooks.length],
        borrowedDate: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(), // 7 days ago
        returnedDate: null
    });
    borrowings.push({
        bookId: `B-${(userId * 13) % 999}`,
        bookTitle: mockBooks[(userId + 1) % mockBooks.length],
        borrowedDate: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(), // 2 days ago
        returnedDate: null
    });
    
    // Add 3 past borrowings
    borrowings.push({
        bookId: `B-${(userId * 3) % 999}`,
        bookTitle: mockBooks[(userId + 2) % mockBooks.length],
        borrowedDate: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString(), // 60 days ago
        returnedDate: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString() // 45 days ago
    });
    borrowings.push({
        bookId: `B-${(userId * 11) % 999}`,
        bookTitle: mockBooks[(userId + 3) % mockBooks.length],
        borrowedDate: new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString(),
        returnedDate: new Date(Date.now() - 75 * 24 * 60 * 60 * 1000).toISOString()
    });
    borrowings.push({
        bookId: `B-${(userId * 5) % 999}`,
        bookTitle: mockBooks[(userId + 4) % mockBooks.length],
        borrowedDate: new Date(Date.now() - 120 * 24 * 60 * 60 * 1000).toISOString(),
        returnedDate: new Date(Date.now() - 110 * 24 * 60 * 60 * 1000).toISOString()
    });
    
    return borrowings;
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

    const handleRevoke = async () => {
        if (!selectedUser) return;
        const confirmRevoke = window.confirm(`Are you sure you want to revoke access for ${selectedUser.firstName} ${selectedUser.lastName}? They will be moved back to the pending requests list.`);
        if (!confirmRevoke) return;
        
        setActionLoading(true);
        try {
            await axios.post(`http://localhost:5000/api/admin/users/${selectedUser.id}/revoke`, {}, {
                headers: { Authorization: `Bearer ${token}` }
            });
            setUsers(prev => prev.filter(u => u.id !== selectedUser.id));
            closeModal();
        } catch (err) {
            console.error('Failed to revoke user:', err);
            alert('Failed to revoke user access. Please try again.');
        } finally {
            setActionLoading(false);
        }
    };

    return (
        <div className="app-shell">
        <AppTopbar />
            <AdminSidebar active={isPending ? 'users-pending' : 'users-active'} />

            <main className="app-main app-main-banner admin-main admin-users-main">
                <PageBanner eyebrow="Member operations" title={pageTitle} icon={<UsersIcon size={38} />} text={pageDescription}>
                    <div className="page-banner-actions"><div className={`admin-users-mode ${isPending ? 'admin-users-mode-pending' : 'admin-users-mode-active'}`}>
                            <Users size={17} />
                            {isPending ? 'Approval queue' : 'Member directory'}
                        </div></div>
                </PageBanner>

                <div className="admin-users-panel glass-panel">
                    <div className="admin-users-panel-heading">
                        <div>
                            <p className="admin-eyebrow">Workspace list</p>
                            <h2>{isPending ? 'Members awaiting review' : 'Validated members'}</h2>
                        </div>
                        <span className="admin-users-count">{users.length} {users.length === 1 ? 'member' : 'members'}</span>
                    </div>
                    
                    {error && <div className="error-message admin-users-error" style={{ marginBottom: '1rem' }}>{error}</div>}
                    
                    {isLoading ? (
                        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)' }}>Loading users...</p>
                    ) : users.length === 0 ? (
                        <div style={{ padding: '3rem', textAlign: 'center', backgroundColor: 'rgba(0,0,0,0.2)', borderRadius: '8px' }}>
                            <p style={{ color: 'var(--text-secondary)' }}>No users found.</p>
                        </div>
                    ) : (
                        <div className="admin-users-table-wrap" style={{ overflowX: 'auto' }}>
                            <table className="admin-users-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
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
                                                style={{ padding: '1rem', fontWeight: 500, cursor: 'pointer', color: 'var(--accent-color)', textDecoration: 'underline' }}
                                                onClick={() => openModal(user)}
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

            {isModalOpen && selectedUser && (
                <Modal title={`${selectedUser.firstName} ${selectedUser.lastName}`} size={isPending ? 'default' : 'wide'} busy={actionLoading} onClose={closeModal}
                    description={isPending ? 'This member has registered and is waiting for approval.' : 'An approved member of the library.'}>
                    <dl className="modal-facts">
                        <div><dt>Name</dt><dd>{selectedUser.firstName} {selectedUser.lastName}</dd></div>
                        <div><dt>Email address</dt><dd>{selectedUser.email}</dd></div>
                        <div><dt>Registered</dt><dd>{new Date(selectedUser.createdAt).toLocaleDateString()} at {new Date(selectedUser.createdAt).toLocaleTimeString()}</dd></div>
                    </dl>

                    {!isPending && (
                        <section className="modal-section">
                            <h3>Borrowing History</h3>
                            <div className="modal-columns">
                                <div className="modal-tile">
                                    <h4>Current Borrowings</h4>
                                    {generateMockBorrowings(selectedUser.id).filter(b => b.returnedDate === null).length === 0
                                        ? <p>No current borrowings.</p>
                                        : generateMockBorrowings(selectedUser.id).filter(b => b.returnedDate === null).map((record, idx) => (
                                            <div key={idx}>
                                                <strong>{record.bookTitle}</strong>
                                                <small>ID: {record.bookId}</small>
                                                <small>Borrowed: {new Date(record.borrowedDate).toLocaleDateString()}</small>
                                            </div>
                                        ))}
                                </div>
                                <div className="modal-tile">
                                    <h4>Past Borrowings</h4>
                                    {generateMockBorrowings(selectedUser.id).filter(b => b.returnedDate !== null).length === 0
                                        ? <p>No past borrowings.</p>
                                        : generateMockBorrowings(selectedUser.id).filter(b => b.returnedDate !== null).map((record, idx) => (
                                            <div key={idx}>
                                                <strong>{record.bookTitle}</strong>
                                                <small>ID: {record.bookId}</small>
                                                <small>Returned: {new Date(record.returnedDate!).toLocaleDateString()}</small>
                                            </div>
                                        ))}
                                </div>
                            </div>
                        </section>
                    )}

                    <div className="modal-actions">
                        <button type="button" className="modal-button" onClick={closeModal} disabled={actionLoading}>Close</button>
                        {isPending ? <>
                            <button type="button" className="modal-button modal-button-danger" onClick={handleReject} disabled={actionLoading}>Reject</button>
                            <button type="button" className="modal-button modal-button-primary" onClick={handleAccept} disabled={actionLoading}>
                                {actionLoading ? 'Processing...' : 'Accept User'}
                            </button>
                        </> : (
                            <button type="button" className="modal-button modal-button-danger" onClick={handleRevoke} disabled={actionLoading}>
                                {actionLoading ? 'Processing...' : 'Revoke Access'}
                            </button>
                        )}
                    </div>
                </Modal>
            )}
        </div>
    );
};

export default AdminUsersPage;
