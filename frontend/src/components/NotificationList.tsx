import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { CheckCheck, RefreshCw } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { NOTIFICATION_API_BASE_URL } from '../config/api';
import { announceNotificationsChanged, isWarningType, notificationApiPath, notificationIcon, notificationLinkLabel, NOTIFICATIONS_CHANGED_EVENT } from './notificationTypes';
import type { NotificationItem } from './notificationTypes';
import './NotificationBell.css';
import './NotificationList.css';

const PAGE_SIZE = 20;

const formatDateTime = (value: string) => new Date(value).toLocaleString('en-GB', {
    timeZone: 'Asia/Colombo', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'
});

/**
 * The full, paged list behind "My Notifications" for customers and admins: newest first, with each
 * notification's message, date and read status. The server only returns notifications this user may see.
 */
const NotificationList = () => {
    const { token, user } = useAuth();
    const isAdmin = user?.role === 'Admin';
    const baseUrl = `${NOTIFICATION_API_BASE_URL}${notificationApiPath(isAdmin)}`;
    const [items, setItems] = useState<NotificationItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);
    const [error, setError] = useState('');
    const [actionError, setActionError] = useState('');
    const [hasMore, setHasMore] = useState(false);
    const [page, setPage] = useState(1);
    // The server's total, not just the unread items on the loaded pages.
    const [unreadTotal, setUnreadTotal] = useState(0);

    const fetchPage = useCallback(async (pageNumber: number) => {
        const response = await axios.get<NotificationItem[]>(`${baseUrl}?page=${pageNumber}&pageSize=${PAGE_SIZE}`,
            { headers: { Authorization: `Bearer ${token}` } });
        return response.data;
    }, [baseUrl, token]);

    const loadUnreadTotal = useCallback(async () => {
        try {
            const response = await axios.get<{ count: number }>(`${baseUrl}/unread-count`, { headers: { Authorization: `Bearer ${token}` } });
            setUnreadTotal(response.data.count);
        } catch {
            // Keep the last known total; the list itself shows each item's status.
        }
    }, [baseUrl, token]);

    // Reads made from the bell show up here too.
    useEffect(() => {
        const onChanged = () => { void loadUnreadTotal(); };
        window.addEventListener(NOTIFICATIONS_CHANGED_EVENT, onChanged);
        return () => window.removeEventListener(NOTIFICATIONS_CHANGED_EVENT, onChanged);
    }, [loadUnreadTotal]);

    const load = useCallback(async () => {
        setLoading(true);
        setError('');
        try {
            const first = await fetchPage(1);
            setItems(first);
            setPage(1);
            setHasMore(first.length === PAGE_SIZE);
            void loadUnreadTotal();
        } catch {
            setError('Your notifications could not be loaded.');
        } finally {
            setLoading(false);
        }
    }, [fetchPage, loadUnreadTotal]);

    useEffect(() => { void load(); }, [load]);

    const loadMore = async () => {
        setLoadingMore(true);
        setActionError('');
        try {
            const next = await fetchPage(page + 1);
            // Notifications that arrived meanwhile shift the pages; skip any already shown.
            setItems(current => [...current, ...next.filter(n => !current.some(c => c.id === n.id))]);
            setPage(page + 1);
            setHasMore(next.length === PAGE_SIZE);
        } catch {
            setActionError('More notifications could not be loaded. Try again.');
        } finally {
            setLoadingMore(false);
        }
    };

    const markRead = async (item: NotificationItem) => {
        setActionError('');
        try {
            await axios.post(`${baseUrl}/${item.id}/read`, {}, { headers: { Authorization: `Bearer ${token}` } });
            setItems(current => current.map(n => n.id === item.id ? { ...n, isRead: true } : n));
            void loadUnreadTotal();
            announceNotificationsChanged();
        } catch {
            setActionError('That notification could not be marked as read. Try again.');
        }
    };

    const markAllRead = async () => {
        setActionError('');
        try {
            await axios.post(`${baseUrl}/read-all`, {}, { headers: { Authorization: `Bearer ${token}` } });
            setItems(current => current.map(n => ({ ...n, isRead: true })));
            setUnreadTotal(0);
            announceNotificationsChanged();
        } catch {
            setActionError('Notifications could not be marked as read. Try again.');
        }
    };

    if (loading) return <section className="notification-list-card"><p className="notification-list-state">Loading notifications…</p></section>;

    if (error) {
        return <section className="notification-list-card" role="alert">
            <p className="notification-list-state notification-list-error">{error}</p>
            <button type="button" className="btn-outline" onClick={() => void load()}><RefreshCw size={15} /> Retry</button>
        </section>;
    }

    return <section className="notification-list-card">
        <div className="notification-list-toolbar">
            <span>{unreadTotal > 0 ? `${unreadTotal} unread` : 'All caught up'}</span>
            <div className="notification-list-actions">
                <button type="button" className="btn-outline" onClick={() => void load()}><RefreshCw size={15} /> Refresh</button>
                <button type="button" className="btn-outline" onClick={() => void markAllRead()} disabled={items.length === 0}><CheckCheck size={15} /> Mark all as read</button>
            </div>
        </div>
        {actionError && <p className="notification-list-error" role="alert">{actionError}</p>}
        {items.length === 0
            ? <p className="notification-list-state">You have no notifications yet. {isAdmin
                ? 'New and cancelled customer reservations and other admins’ catalogue changes will appear here.'
                : 'Updates about your reservations, returns, fines and new books will appear here.'}</p>
            : <ul className="notification-list-items">
                {items.map(item => (
                    <li key={item.id} className={`notification-list-item${item.isRead ? '' : ' notification-list-item-unread'}`}>
                        <span className={`notification-icon${isWarningType(item.type) ? ' notification-icon-cancelled' : ''}`}>{notificationIcon(item.type)}</span>
                        <div className="notification-list-body">
                            <p>{item.message}</p>
                            <div className="notification-list-meta">
                                <time dateTime={item.createdAtUtc}>{formatDateTime(item.createdAtUtc)}</time>
                                <span className={`notification-list-status${item.isRead ? '' : ' notification-list-status-unread'}`}>{item.isRead ? 'Read' : 'Unread'}</span>
                                {item.link && <Link to={item.link} className="notification-list-link">{notificationLinkLabel(item.link)}</Link>}
                            </div>
                        </div>
                        {!item.isRead && <button type="button" className="notification-link" onClick={() => void markRead(item)}>Mark as read</button>}
                    </li>
                ))}
            </ul>}
        {hasMore && <div className="notification-list-more">
            <button type="button" className="btn-outline" onClick={() => void loadMore()} disabled={loadingMore}>{loadingMore ? 'Loading…' : 'Load more'}</button>
        </div>}
    </section>;
};

export default NotificationList;
