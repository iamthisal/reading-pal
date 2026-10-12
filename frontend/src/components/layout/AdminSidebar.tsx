import { Bell, BookMarked, BookOpen, BookPlus, History, LayoutDashboard, ListOrdered, PenLine, UserCheck, Users } from 'lucide-react';
import AppSidenav from './AppSidenav';
import type { AppNavItem } from './AppSidenav';

export type AdminPage =
    | 'dashboard' | 'users-pending' | 'users-active' | 'books' | 'authors' | 'lists'
    | 'reservations-pending' | 'borrowed' | 'history' | 'notifications';

const pages: { page: AdminPage | 'catalogue'; to: string; label: string; icon: typeof Bell }[] = [
    { page: 'dashboard', to: '/admin/dashboard', label: 'Overview', icon: LayoutDashboard },
    { page: 'users-pending', to: '/admin/users/pending', label: 'User approvals', icon: UserCheck },
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

// The administration menu, shared by every admin page. It uses the same panel as the reader pages;
// the brand, notifications and log out live in the top bar.
const AdminSidebar = ({ active }: { active: AdminPage }) => {
    const items: AppNavItem[] = pages.map(({ page, to, label, icon: Icon }) =>
        ({ label, to, icon: <Icon size={18} />, active: page === active }));

    return <AppSidenav items={items} label="Administration navigation" />;
};

export default AdminSidebar;
