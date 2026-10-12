import { Link } from 'react-router-dom';
import { Bell, BookMarked, BookOpen, Heart, History, Library, LogOut, User } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import NotificationBell from '../components/NotificationBell';
import NotificationList from '../components/NotificationList';

const MyNotificationsPage = () => {
    const { user, logout } = useAuth();

    return <div className="discover-shell">
        <aside className="discover-sidebar">
            <Link to="/home" className="discover-brand"><BookMarked size={24} /><span>Reading Pal</span></Link>
            <nav className="discover-nav" aria-label="Library navigation">
                <span className="discover-nav-label">Menu</span>
                <Link to="/home" className="discover-nav-item"><BookOpen size={16} />Discover</Link>
                <Link to="/home#recommendations" className="discover-nav-item"><Library size={16} />My Library</Link>
                <Link to="/home#recommendations" className="discover-nav-item"><Heart size={16} />Favorite</Link>
                <Link to="/my-borrowings" className="discover-nav-item"><History size={16} />My borrowings &amp; fines</Link>
                <Link to="/notifications" className="discover-nav-item discover-nav-item-active" aria-current="page"><Bell size={16} />My notifications</Link>
            </nav>
            <div className="discover-sidebar-bottom">
                <Link to="/profile" className="discover-nav-item"><User size={16} />My Profile</Link>
                <button type="button" onClick={logout} className="discover-nav-item discover-nav-button"><LogOut size={16} />Log out</button>
            </div>
        </aside>
        <main className="discover-main profile-main">
            <header className="profile-topbar"><div className="profile-user-chip"><span className="discover-avatar">{user?.email?.slice(0, 1).toUpperCase() || 'R'}</span><span>{user?.email || 'Reader'}</span></div><NotificationBell /></header>
            <section className="profile-hero">
                <div><p className="discover-eyebrow">Stay up to date</p><h1>My notifications</h1><p>Reservation updates, reminders, fines and new books, newest first.</p></div>
                <div className="profile-hero-mark" aria-hidden="true"><Bell size={38} /></div>
            </section>
            <NotificationList />
        </main>
    </div>;
};

export default MyNotificationsPage;
