import { useCallback, useEffect, useRef, useState } from 'react';
import axios from 'axios';
import { Bell, BookCheck, BookX, CalendarCheck } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { NOTIFICATION_API_BASE_URL } from '../config/api';
import { refreshWhileVisible } from '../utils/refreshWhileVisible';
import './NotificationBell.css';

type NotificationItem = {
    id: number;
    type: 'ReservationAccepted' | 'ReservationCancelled' | 'BookReturned';
    bookTitle: string;
    message: string;
    isRead: boolean;
    createdAtUtc: string;
};

const typeIcon = {
    ReservationAccepted: <BookCheck size={16} />,
    ReservationCancelled: <BookX size={16} />,
    BookReturned: <CalendarCheck size={16} />
};

const formatTime = (value: string) => new Date(value).toLocaleString('en-GB', {
    timeZone: 'Asia/Colombo', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit'
});

// Notifications exist only for borrowers; the admin account has no user ID to receive them.
const NotificationBell = () => {
    const { token, user } = useAuth();
    const [unread, setUnread] = useState(0);
    const [open, setOpen] = useState(false);
    const [items, setItems] = useState<NotificationItem[] | null>(null);
    const [error, setError] = useState('');
    const panelRef = useRef<HTMLDivElement>(null);
    const headers = { Authorization: `Bearer ${token}` };
    const enabled = Boolean(token) && user?.role === 'User';

    const loadCount = useCallback(async () => {
        try {
            const response = await axios.get<{ count: number }>(`${NOTIFICATION_API_BASE_URL}/api/notifications/unread-count`, { headers: { Authorization: `Bearer ${token}` } });
            setUnread(response.data.count);
        } catch {
            // The badge is a hint; keep the last known count if the service is briefly unavailable.
        }
    }, [token]);

    const loadList = useCallback(async () => {
        try {
            const response = await axios.get<NotificationItem[]>(`${NOTIFICATION_API_BASE_URL}/api/notifications?pageSize=10`, { headers: { Authorization: `Bearer ${token}` } });
            setItems(response.data);
            setError('');
        } catch {
            setError('Notifications are unavailable right now.');
        }
    }, [token]);

    useEffect(() => {
        if (!enabled) return;
        void loadCount();
        return refreshWhileVisible(async () => {
            await loadCount();
            if (open) await loadList();
        });
    }, [enabled, open, loadCount, loadList]);

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
            await axios.post(`${NOTIFICATION_API_BASE_URL}/api/notifications/${item.id}/read`, {}, { headers });
        } catch {
            void loadCount();
            void loadList();
        }
    };

    const markAllRead = async () => {
        setItems(current => current?.map(n => ({ ...n, isRead: true })) ?? null);
        setUnread(0);
        try {
            await axios.post(`${NOTIFICATION_API_BASE_URL}/api/notifications/read-all`, {}, { headers });
        } catch {
            void loadCount();
            void loadList();
        }
    };

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
                        : items.length === 0 ? <p className="notification-empty">No notifications yet. Updates about your reservations and returns will appear here.</p>
                        : <ul className="notification-list">
                            {items.map(item => (
                                <li key={item.id}>
                                    <button type="button" className={`notification-item${item.isRead ? '' : ' notification-item-unread'}`} onClick={() => void markRead(item)}>
                                        <span className={`notification-icon notification-icon-${item.type}`}>{typeIcon[item.type] ?? <Bell size={16} />}</span>
                                        <span className="notification-text">
                                            <span>{item.message}</span>
                                            <time dateTime={item.createdAtUtc}>{formatTime(item.createdAtUtc)}</time>
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
