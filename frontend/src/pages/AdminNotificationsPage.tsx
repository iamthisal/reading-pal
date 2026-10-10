import { Link } from 'react-router-dom';
import { Bell, BookMarked, BookOpen, BookPlus, History, LayoutDashboard, LogOut, Users } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import NotificationBell from '../components/NotificationBell';
import NotificationList from '../components/NotificationList';

const AdminNotificationsPage = () => {
    const { logout } = useAuth();

    return <div className="admin-shell">
        <aside className="admin-sidebar">
            <Link to="/admin/dashboard" className="admin-brand"><BookMarked size={23} /><span>Reading Pal</span></Link>
            <nav className="admin-nav" aria-label="Administration navigation">
                <span className="admin-nav-label">Workspace</span>
                <Link to="/admin/dashboard" className="admin-nav-item"><LayoutDashboard size={16} />Overview</Link>
                <Link to="/admin/users/pending" className="admin-nav-item"><Users size={16} />User approvals</Link>
                <Link to="/admin/users/active" className="admin-nav-item"><Users size={16} />Active users</Link>
                <Link to="/admin/books" className="admin-nav-item"><BookPlus size={16} />Book inventory</Link>
                <Link to="/admin/reservations/pending" className="admin-nav-item"><BookMarked size={16} />Pending reservations</Link>
                <Link to="/admin/borrowed" className="admin-nav-item"><BookOpen size={16} />Borrowed books</Link>
                <Link to="/admin/reservations/history" className="admin-nav-item"><History size={16} />Reservation history</Link>
                <Link to="/admin/notifications" className="admin-nav-item admin-nav-item-active" aria-current="page"><Bell size={16} />Notifications</Link>
                <Link to="/home" className="admin-nav-item"><BookOpen size={16} />Public catalogue</Link>
            </nav>
            <div className="admin-sidebar-bottom"><button type="button" onClick={logout} className="admin-nav-item admin-nav-button"><LogOut size={16} />Log out</button></div>
        </aside>
        <main className="admin-main admin-users-main">
            <header className="admin-topbar admin-users-topbar">
                <div>
                    <p className="admin-eyebrow">Library operations</p>
                    <h1>Notifications</h1>
                    <p className="admin-users-subtitle">Customer reservation activity and other admins’ catalogue changes, newest first. Your read status is your own.</p>
                </div>
                <div className="admin-topbar-actions"><NotificationBell /></div>
            </header>
            <NotificationList />
        </main>
    </div>;
};

export default AdminNotificationsPage;
