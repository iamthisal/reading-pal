import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { BookOpen, ChevronDown, LayoutDashboard, LogOut, Search, User } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import NotificationBell from '../NotificationBell';
import './AppShell.css';

type AppTopbarProps = {
    /** What the search box starts with: the Search page passes its current query. */
    query?: string;
};

// The profile button and its menu: who is signed in, their profile (or the admin dashboard), and log out.
const UserMenu = () => {
    const { user, logout } = useAuth();
    const [open, setOpen] = useState(false);
    const ref = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!open) return;
        const close = (event: MouseEvent) => {
            if (ref.current && !ref.current.contains(event.target as Node)) setOpen(false);
        };
        const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false); };
        document.addEventListener('mousedown', close);
        document.addEventListener('keydown', closeOnEscape);
        return () => {
            document.removeEventListener('mousedown', close);
            document.removeEventListener('keydown', closeOnEscape);
        };
    }, [open]);

    const isAdmin = user?.role === 'Admin';
    return (
        <div className="app-user" ref={ref}>
            <button type="button" className="app-user-button" onClick={() => setOpen(value => !value)}
                aria-haspopup="menu" aria-expanded={open} aria-label="Account menu" title={user?.email || 'Account'}>
                <span className="app-avatar">{user?.email?.slice(0, 1).toUpperCase() || 'R'}</span>
                <ChevronDown size={15} />
            </button>
            {open && (
                <div className="app-user-menu" role="menu">
                    <div className="app-user-identity">
                        <small>Signed in as</small>
                        <span>{user?.email || 'Reader'}</span>
                    </div>
                    {isAdmin
                        ? <Link to="/admin/dashboard" className="app-user-menu-item" role="menuitem" onClick={() => setOpen(false)}><LayoutDashboard size={16} />Admin dashboard</Link>
                        : <Link to="/profile" className="app-user-menu-item" role="menuitem" onClick={() => setOpen(false)}><User size={16} />My profile</Link>}
                    <button type="button" className="app-user-menu-item" role="menuitem" onClick={logout}><LogOut size={16} />Log out</button>
                </div>
            )}
        </div>
    );
};

const AppTopbar = ({ query = '' }: AppTopbarProps) => {
    const navigate = useNavigate();
    const [text, setText] = useState(query);
    // Follow the page when its query changes (back button, a cleared search), without fighting the reader's typing.
    const [lastQuery, setLastQuery] = useState(query);
    if (query !== lastQuery) {
        setLastQuery(query);
        setText(query);
    }

    // Searching from anywhere opens the Search page. An empty box simply browses everything.
    const submit = (event: FormEvent) => {
        event.preventDefault();
        const trimmed = text.trim();
        navigate(trimmed ? `/search?q=${encodeURIComponent(trimmed)}` : '/search');
    };

    return (
        <header className="app-topbar">
            <Link to="/home" className="app-brand">
                <span className="app-brand-mark"><BookOpen size={18} /></span>
                <span>Reading Pal</span>
            </Link>
            <form className="app-search" role="search" onSubmit={submit}>
                <Search size={16} />
                <input type="search" value={text} onChange={event => setText(event.target.value)}
                    placeholder="Search books, authors, ISBN" aria-label="Search the catalogue" />
            </form>
            <div className="app-topbar-actions">
                <NotificationBell />
                <UserMenu />
            </div>
        </header>
    );
};

export default AppTopbar;
