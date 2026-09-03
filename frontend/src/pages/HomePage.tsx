import { useAuth } from '../contexts/AuthContext';
import { LogOut, BookMarked, User } from 'lucide-react';
import { Link } from 'react-router-dom';

const HomePage = () => {
    const { logout, user } = useAuth();

    return (
        <div className="page-container">
            <header className="dashboard-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    <BookMarked size={32} color="var(--accent-color)" />
                    <h1 style={{ fontSize: '1.5rem', fontWeight: 600 }}>Reading Pal</h1>
                </div>
                <div style={{ display: 'flex', gap: '1rem' }}>
                    <Link to="/profile" className="btn-outline" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', textDecoration: 'none' }}>
                        <User size={16} />
                        My Profile
                    </Link>
                    <button onClick={logout} className="btn-outline" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <LogOut size={16} />
                        Sign Out
                    </button>
                </div>
            </header>
            
            <main>
                <div className="glass-panel" style={{ padding: '2rem' }}>
                    <h2 style={{ marginBottom: '1rem' }}>Welcome to the Library</h2>
                    
                    {!user?.isValidated && (
                        <div className="error-message" style={{ background: 'rgba(234, 179, 8, 0.1)', borderColor: '#eab308', color: '#fde047' }}>
                            <strong>Account Pending Approval:</strong> Your registration has been received but must be validated by an administrator before you can reserve physical books.
                        </div>
                    )}
                    
                    <p style={{ color: 'var(--text-secondary)' }}>
                        Browse our physical catalog, manage reservations, and track your Reading Pal account here.
                    </p>
                </div>
            </main>
        </div>
    );
};

export default HomePage;
