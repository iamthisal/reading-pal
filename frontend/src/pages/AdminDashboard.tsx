import { useAuth } from '../contexts/AuthContext';
import { ArrowUpRight, BookMarked, BookOpen, BookPlus, LayoutDashboard, LogOut, ShieldAlert, Users } from 'lucide-react';
import { useEffect, useState } from 'react';
import axios from 'axios';
import { Link } from 'react-router-dom';
import { API_BASE_URL } from '../config/api';

const AdminDashboard = () => {
    const { logout, token, user } = useAuth();
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
        <div className="admin-shell">
            <aside className="admin-sidebar">
                <Link to="/admin/dashboard" className="admin-brand">
                    <BookMarked size={23} />
                    <span>Reading Pal</span>
                </Link>

                <nav className="admin-nav" aria-label="Administration navigation">
                    <span className="admin-nav-label">Workspace</span>
                    <Link to="/admin/dashboard" className="admin-nav-item admin-nav-item-active">
                        <LayoutDashboard size={16} />
                        Overview
                    </Link>
                    <Link to="/admin/users/pending" className="admin-nav-item">
                        <Users size={16} />
                        User approvals
                    </Link>
                    <Link to="/admin/users/active" className="admin-nav-item">
                        <Users size={16} />
                        Active users
                    </Link>
                    <Link to="/admin/books" className="admin-nav-item">
                        <BookPlus size={16} />
                        Book inventory
                    </Link>
                    <Link to="/home" className="admin-nav-item">
                        <BookOpen size={16} />
                        Public catalogue
                    </Link>
                </nav>

                <div className="admin-sidebar-bottom">
                    <button type="button" onClick={logout} className="admin-nav-item admin-nav-button">
                        <LogOut size={16} />
                        Log out
                    </button>
                </div>
            </aside>

            <main className="admin-main">
                <header className="admin-topbar">
                    <div>
                        <p className="admin-eyebrow">Library operations</p>
                        <h1>Admin dashboard</h1>
                    </div>
                    <div className="admin-user-chip">
                        <span className="admin-avatar">{user?.email?.slice(0, 1).toUpperCase() || 'A'}</span>
                        <span>{user?.email || 'Administrator'}</span>
                    </div>
                </header>

                <section className="admin-welcome">
                    <div>
                        <p className="admin-kicker">Good to see you</p>
                        <h2>Keep the library moving.</h2>
                        <p>Review member activity, maintain the catalogue, and keep every shelf ready for its next reader.</p>
                    </div>
                    <div className="admin-welcome-mark"><BookMarked size={42} /></div>
                </section>

                <section className="admin-section" aria-labelledby="admin-actions-heading">
                    <div className="admin-section-heading">
                        <div>
                            <p className="admin-eyebrow">Quick access</p>
                            <h2 id="admin-actions-heading">What needs your attention?</h2>
                        </div>
                        <span className="admin-section-note">4 tools available</span>
                    </div>

                    <div className="admin-action-grid">
                        <Link to="/admin/users/pending" className="admin-action-card admin-action-card-coral">
                            <span className="admin-action-icon"><Users size={21} /></span>
                            <span className="admin-action-copy"><strong>Pending requests</strong><small>Manage new members awaiting approval</small></span>
                            <ArrowUpRight className="admin-action-arrow" size={18} />
                        </Link>
                        <Link to="/admin/users/active" className="admin-action-card admin-action-card-yellow">
                            <span className="admin-action-icon"><Users size={21} /></span>
                            <span className="admin-action-copy"><strong>Active users</strong><small>Browse approved library members</small></span>
                            <ArrowUpRight className="admin-action-arrow" size={18} />
                        </Link>
                        <Link to="/admin/books" className="admin-action-card admin-action-card-green">
                            <span className="admin-action-icon"><BookPlus size={21} /></span>
                            <span className="admin-action-copy"><strong>Book inventory</strong><small>Add titles and track availability</small></span>
                            <ArrowUpRight className="admin-action-arrow" size={18} />
                        </Link>
                        <Link to="/home" className="admin-action-card admin-action-card-ivory">
                            <span className="admin-action-icon"><BookOpen size={21} /></span>
                            <span className="admin-action-copy"><strong>Public catalogue</strong><small>See the experience members use</small></span>
                            <ArrowUpRight className="admin-action-arrow" size={18} />
                        </Link>
                    </div>
                </section>

                <section className="admin-message" aria-labelledby="admin-message-heading">
                    <ShieldAlert size={24} />
                    <div>
                        <p className="admin-eyebrow">Private channel</p>
                        <h2 id="admin-message-heading">Classified admin message</h2>
                        <p>{adminMessage || 'Loading secret message...'}</p>
                    </div>
                </section>
            </main>
        </div>
    );
};

export default AdminDashboard;
