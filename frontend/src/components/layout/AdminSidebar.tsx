import { Link } from 'react-router-dom';
import { Bell, BookMarked, BookOpen, BookPlus, History, LayoutDashboard, ListOrdered, LogOut, PenLine, Users } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';

export type AdminPage =
    | 'dashboard' | 'users-pending' | 'users-active' | 'books' | 'authors' | 'lists'
    | 'reservations-pending' | 'borrowed' | 'history' | 'notifications';

const items: { page: AdminPage | 'catalogue'; to: string; label: string; icon: typeof Bell }[] = [
    { page: 'dashboard', to: '/admin/dashboard', label: 'Overview', icon: LayoutDashboard },
    { page: 'users-pending', to: '/admin/users/pending', label: 'User approvals', icon: Users },
    { page: 'users-active', to: '/admin/users/active', label: 'Active users', icon: Users },
    { page: 'books', to: '/admin/books', label: 'Book inventory', icon: BookPlus },
    { page: 'authors', to: '/admin/authors', label: 'Authors', icon: PenLine },
    { page: 'lists', to: '/admin/lists', label: 'Curated lists', icon: ListOrdered },
    { page: 'reservations-pending', to: '/admin/reservations/pending', label: 'Pending reservations', icon: BookMarked },
    { page: 'borrowed', to: '/admin/borrowed', label: 'Borrowed books', icon: BookOpen },
    { page: 'history', to: '/admin/reservations/history', label: 'Reservation history', icon: History },
    { page: 'notifications', to: '/admin/notifications', label: 'Notifications', icon: Bell },
    { page: 'catalogue', to: '/home', label: 'Public catalogue', icon: BookOpen },
];

// The administration menu, shared by every admin page.
const AdminSidebar = ({ active }: { active: AdminPage }) => {
    const { logout } = useAuth();

    return (
        <aside className="admin-sidebar">
            <Link to="/admin/dashboard" className="admin-brand"><BookMarked size={23} /><span>Reading Pal</span></Link>
            <nav className="admin-nav" aria-label="Administration navigation">
                <span className="admin-nav-label">Workspace</span>
                {items.map(({ page, to, label, icon: Icon }) => (
                    <Link key={page} to={to} className={`admin-nav-item${page === active ? ' admin-nav-item-active' : ''}`}
                        aria-current={page === active ? 'page' : undefined}>
                        <Icon size={16} />{label}
                    </Link>
                ))}
            </nav>
            <div className="admin-sidebar-bottom">
                <button type="button" onClick={logout} className="admin-nav-item admin-nav-button"><LogOut size={16} />Log out</button>
            </div>
        </aside>
    );
};

export default AdminSidebar;
