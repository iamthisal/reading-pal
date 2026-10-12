import type { ReactNode } from 'react';
import { Bell, BookOpen, Heart, History, LayoutDashboard, Search, User } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import AppSidenav from './AppSidenav';
import type { AppNavItem } from './AppSidenav';

type LibrarySidenavProps = {
    /** The page being shown, so its menu entry is highlighted. Leave out on pages that are not in the menu, such as a book's own page. */
    active?: 'discover' | 'search' | 'favourites' | 'borrowings' | 'notifications' | 'profile';
    children?: ReactNode;
};

// The reader-facing menu, shared by the catalogue, borrowings, notifications and profile pages.
const LibrarySidenav = ({ active, children }: LibrarySidenavProps) => {
    const { user } = useAuth();
    const items: AppNavItem[] = [
        { label: 'Discover', icon: <BookOpen size={18} />, to: '/home', active: active === 'discover' },
        { label: 'Search', icon: <Search size={18} />, to: '/search', active: active === 'search' },
        { label: 'Favourites', icon: <Heart size={18} />, to: '/favourites', active: active === 'favourites' },
        ...(user?.role === 'User' ? [
            { label: 'My borrowings', icon: <History size={18} />, to: '/my-borrowings', active: active === 'borrowings' },
            { label: 'Notifications', icon: <Bell size={18} />, to: '/notifications', active: active === 'notifications' },
            { label: 'My profile', icon: <User size={18} />, to: '/profile', active: active === 'profile' },
        ] : []),
        ...(user?.role === 'Admin' ? [
            { label: 'Admin dashboard', icon: <LayoutDashboard size={18} />, to: '/admin/dashboard' },
        ] : []),
    ];

    return <AppSidenav items={items} label="Library navigation">{children}</AppSidenav>;
};

export default LibrarySidenav;
