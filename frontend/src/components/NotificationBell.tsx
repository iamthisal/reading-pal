import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { AlarmClock, Bell, BookCheck, BookPlus, BookX, CalendarCheck, Sparkles, Trash2, Wallet } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { NOTIFICATION_API_BASE_URL } from '../config/api';
import { refreshWhileVisible } from '../utils/refreshWhileVisible';
import './NotificationBell.css';

type NotificationItem = {
    id: number;
    type: string;
    bookTitle: string;
    message: string;
    isRead: boolean;
    createdAtUtc: string;
    // Admin notifications point to the page where the request is handled.
    link?: string;
};

const typeIcon: Record<string, ReactNode> = {
    ReservationAccepted: <BookCheck size={16} />,
    ReservationCancelled: <BookX size={16} />,
    BookReturned: <CalendarCheck size={16} />,
    NewReservation: <BookPlus size={16} />,
    CustomerCancelledReservation: <BookX size={16} />,
    FineRecorded: <Wallet size={16} />,
    DueDateReminder: <AlarmClock size={16} />,
    NewBook: <Sparkles size={16} />,
    // No link: a deleted book has no page; the saved title in the message identifies it.
    BookDeleted: <Trash2 size={16} />
};

// Shown in the warning colour: something the reader should notice.
const warningTypes = new Set(['ReservationCancelled', 'CustomerCancelledReservation', 'FineRecorded', 'BookDeleted']);

const formatTime = (value: string) => new Date(value).toLocaleString('en-GB', {
    timeZone: 'Asia/Colombo', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit'
});

// Borrowers see updates about their own reservations; admins see customer activity on the pending queue.
const NotificationBell = () => {
    const { token, user } = useAuth();
    const navigate = useNavigate();
    const [unread, setUnread] = useState(0);
    const [open, setOpen] = useState(false);
    const [items, setItems] = useState<NotificationItem[] | null>(null);
    const [error, setError] = useState('');
    const panelRef = useRef<HTMLDivElement>(null);
    const isAdmin = user?.role === 'Admin';
    const enabled = Boolean(token) && (user?.role === 'User' || isAdmin);
    const baseUrl = `${NOTIFICATION_API_BASE_URL}/api/${isAdmin ? 'admin/' : ''}notifications`;
    const headers = { Authorization: `Bearer ${token}` };

    const loadCount = useCallback(async () => {
        try {
            const response = await axios.get<{ count: number }>(`${baseUrl}/unread-count`, { headers: { Authorization: `Bearer ${token}` } });
            setUnread(response.data.count);
        } catch {
            // The badge is a hint; keep the last known count if the service is briefly unavailable.
        }
    }, [baseUrl, token]);

    const loadList = useCallback(async () => {
        try {
            const response = await axios.get<NotificationItem[]>(`${baseUrl}?pageSize=10`, { headers: { Authorization: `Bearer ${token}` } });
            setItems(response.data);
            setError('');
        } catch {
            setError('Notifications are unavailable right now.');
        }
    }, [baseUrl, token]);

    // Read through a ref so opening or closing the panel does not restart polling (a restart would
    // fetch the count immediately and could overwrite a read that is still being saved).
    const openRef = useRef(open);
    useEffect(() => { openRef.current = open; }, [open]);

    useEffect(() => {
        if (!enabled) return;
        void loadCount();
        return refreshWhileVisible(async () => {
            await loadCount();
            if (openRef.current) await loadList();
        });
    }, [enabled, loadCount, loadList]);

    useEffect(() => {
        if (!open) return;
        const close = (event: MouseEvent) => {
            if (panelRef.current && !panelRef.current.contains(event.target as Node)) setOpen(false);
        };
        const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false); };
        document.addEventListener('mousedown', close);
        document.addEventListener('keydown', closeOnEscape);
        return () => {
            document.removeEventListener('mousedown', close);
            document.removeEventListener('keydown', closeOnEscape);
        };
    }, [open]);

    if (!enabled) return null;

    const toggle = () => {
        const next = !open;
        setOpen(next);
        if (next) void loadList();
    };

    const markRead = async (item: NotificationItem) => {
        if (item.isRead) return;
        setItems(current => current?.map(n => n.id === item.id ? { ...n, isRead: true } : n) ?? null);
        setUnread(count => Math.max(0, count - 1));
        try {
            await axios.post(`${baseUrl}/${item.id}/read`, {}, { headers });
            void loadCount();
        } catch {
            void loadCount();
            void loadList();
        }
    };

    const select = async (item: NotificationItem) => {
        if (!item.link) {
            void markRead(item);
            return;
        }
        setOpen(false);
        // Save the read state before leaving, so the next page's bell loads the updated count.
        await markRead(item);
        // The notification carries no actions: it opens the live list, so an outdated
        // request is simply no longer there and cannot be acted on.
        navigate(item.link);
    };

    const markAllRead = async () => {
        setItems(current => current?.map(n => ({ ...n, isRead: true })) ?? null);
        setUnread(0);
        try {
            await axios.post(`${baseUrl}/read-all`, {}, { headers });
            void loadCount();
        } catch {
            void loadCount();
            void loadList();
        }
    };

    const emptyText = isAdmin
        ? 'No notifications yet. New and cancelled customer reservations will appear here.'
        : 'No notifications yet. Updates about your reservations and returns will appear here.';

    return (
        <div className="notification-bell" ref={panelRef}>
            <button type="button" className="discover-icon-button notification-bell-button" onClick={toggle}
                aria-label={unread > 0 ? `Notifications, ${unread} unread` : 'Notifications'} aria-expanded={open} title="Notifications">
                <Bell size={17} />
                {unread > 0 && <span className="notification-badge">{unread > 9 ? '9+' : unread}</span>}
            </button>
            {open && (
                <div className="notification-panel" role="dialog" aria-label="Notifications">
                    <div className="notification-panel-header">
                        <strong>Notifications</strong>
                        {unread > 0 && <button type="button" className="notification-link" onClick={() => void markAllRead()}>Mark all as read</button>}
                    </div>
                    {error ? <p className="notification-empty">{error}</p>
                        : items === null ? <p className="notification-empty">Loading…</p>
                        : items.length === 0 ? <p className="notification-empty">{emptyText}</p>
                        : <ul className="notification-list">
                            {items.map(item => (
                                <li key={item.id}>
                                    <button type="button" className={`notification-item${item.isRead ? '' : ' notification-item-unread'}`} onClick={() => void select(item)}>
                                        <span className={`notification-icon${warningTypes.has(item.type) ? ' notification-icon-cancelled' : ''}`}>{typeIcon[item.type] ?? <Bell size={16} />}</span>
                                        <span className="notification-text">
                                            <span>{item.message}</span>
                                            <time dateTime={item.createdAtUtc}>{formatTime(item.createdAtUtc)}</time>
                                            {item.link && <span className="notification-cta">View pending reservations →</span>}
                                        </span>
                                        {!item.isRead && <span className="notification-dot" aria-label="Unread" />}
                                    </button>
                                </li>
                            ))}
                        </ul>}
                </div>
            )}
        </div>
    );
};

export default NotificationBell;
