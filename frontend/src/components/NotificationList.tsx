import { useCallback, useEffect, useState, useRef } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { CheckCheck, Eye, EyeOff, History, RefreshCw } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { NOTIFICATION_API_BASE_URL } from '../config/api';
import { announceNotificationsChanged, isWarningType, notificationApiPath, notificationIcon, notificationLinkLabel, NOTIFICATIONS_CHANGED_EVENT } from './notificationTypes';
import type { NotificationItem } from './notificationTypes';
import './NotificationBell.css';
import './NotificationList.css';

const PAGE_SIZE = 20;
// The server's largest page; unread items are fetched in these until a short page says there are no more.
const UNREAD_PAGE_SIZE = 50;
const MAX_UNREAD_PAGES = 10;
// A history page can be all unread items (they are already shown above), so a chunk may read a few
// pages before it finds something new to show.
const MAX_PAGES_PER_CHUNK = 5;

const formatDateTime = (value: string) => new Date(value).toLocaleString('en-GB', {
    timeZone: 'Asia/Colombo', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'
});

const withoutDuplicates = (items: NotificationItem[]) => {
    const seen = new Set<number>();
    return items.filter(item => !seen.has(item.id) && seen.add(item.id));
};

/**
 * The list behind "Notifications" for customers and admins. It opens on the unread notifications only;
 * already-read ones stay out of the way until the reader asks for them with "Load history", which
 * brings them in a chunk at a time, newest first. The server only returns notifications this user may see.
 */
const NotificationList = () => {
    const { token, user } = useAuth();
    const isAdmin = user?.role === 'Admin';
    const baseUrl = `${NOTIFICATION_API_BASE_URL}${notificationApiPath(isAdmin)}`;
    const [unread, setUnread] = useState<NotificationItem[]>([]);
    const [history, setHistory] = useState<NotificationItem[]>([]);
    // How many pages of the full list the history has read so far; 0 until it is first asked for.
    const [historyPages, setHistoryPages] = useState(0);
    const [historyHasMore, setHistoryHasMore] = useState(true);
    // Whether the read notifications are on screen; hiding them keeps what was loaded for the next show.
    const [historyShown, setHistoryShown] = useState(false);
    const [loading, setLoading] = useState(true);
    const [loadingHistory, setLoadingHistory] = useState(false);
    const [error, setError] = useState('');
    const [actionError, setActionError] = useState('');
    // The server's total, which can be more than the unread items on screen.
    const [unreadTotal, setUnreadTotal] = useState(0);

    const fetchPage = useCallback(async (pageNumber: number, unreadOnly: boolean) => {
        const pageSize = unreadOnly ? UNREAD_PAGE_SIZE : PAGE_SIZE;
        const response = await axios.get<NotificationItem[]>(`${baseUrl}?unreadOnly=${unreadOnly}&page=${pageNumber}&pageSize=${pageSize}`,
            { headers: { Authorization: `Bearer ${token}` } });
        return response.data;
    }, [baseUrl, token]);

    const fetchUnread = useCallback(async () => {
        const all: NotificationItem[] = [];
        for (let page = 1; page <= MAX_UNREAD_PAGES; page++) {
            const next = await fetchPage(page, true);
            all.push(...next);
            if (next.length < UNREAD_PAGE_SIZE) break;
        }
        return withoutDuplicates(all);
    }, [fetchPage]);

    const loadUnreadTotal = useCallback(async () => {
        try {
            const response = await axios.get<{ count: number }>(`${baseUrl}/unread-count`, { headers: { Authorization: `Bearer ${token}` } });
            setUnreadTotal(response.data.count);
        } catch {
            // Keep the last known total; the list itself shows what is unread.
        }
    }, [baseUrl, token]);

    // How much history is on screen, readable from the event handler below without re-subscribing.
    const historyPagesRef = useRef(0);
    useEffect(() => { historyPagesRef.current = historyPages; }, [historyPages]);

    // Re-reads what is on screen, quietly: no loading state, and the reader keeps their place. Items read
    // since the last look leave the unread list and, once history is open, show up in it.
    const refreshLoaded = useCallback(async () => {
        try {
            const nextUnread = await fetchUnread();
            setUnread(nextUnread);
            const pages = historyPagesRef.current;
            if (pages > 0) {
                const loaded = await Promise.all(Array.from({ length: pages }, (_, index) => fetchPage(index + 1, false)));
                setHistory(withoutDuplicates(loaded.flat().filter(item => item.isRead)));
                setHistoryHasMore(loaded[loaded.length - 1].length === PAGE_SIZE);
            }
        } catch {
            // Keep what is shown; the next change or a manual refresh will catch up.
        }
    }, [fetchPage, fetchUnread]);

    // Reads made from the bell show up here too.
    useEffect(() => {
        const onChanged = () => { void loadUnreadTotal(); void refreshLoaded(); };
        window.addEventListener(NOTIFICATIONS_CHANGED_EVENT, onChanged);
        return () => window.removeEventListener(NOTIFICATIONS_CHANGED_EVENT, onChanged);
    }, [loadUnreadTotal, refreshLoaded]);

    const load = useCallback(async () => {
        setLoading(true);
        setError('');
        try {
            setUnread(await fetchUnread());
            setHistory([]);
            setHistoryPages(0);
            setHistoryHasMore(true);
            setHistoryShown(false);
            void loadUnreadTotal();
        } catch {
            setError('Your notifications could not be loaded.');
        } finally {
            setLoading(false);
        }
    }, [fetchUnread, loadUnreadTotal]);

    useEffect(() => { void load(); }, [load]);

    const loadHistory = async () => {
        setHistoryShown(true);
        setLoadingHistory(true);
        setActionError('');
        try {
            let page = historyPages;
            let more = true;
            const found: NotificationItem[] = [];
            for (let read = 0; read < MAX_PAGES_PER_CHUNK && more && found.length === 0; read++) {
                page += 1;
                const next = await fetchPage(page, false);
                more = next.length === PAGE_SIZE;
                found.push(...next.filter(item => item.isRead));
            }
            // Notifications that arrived meanwhile shift the pages; skip any already shown.
            setHistory(current => withoutDuplicates([...current, ...found]));
            setHistoryPages(page);
            setHistoryHasMore(more);
        } catch {
            setActionError('Older notifications could not be loaded. Try again.');
        } finally {
            setLoadingHistory(false);
        }
    };

    const markRead = async (item: NotificationItem) => {
        setActionError('');
        try {
            await axios.post(`${baseUrl}/${item.id}/read`, {}, { headers: { Authorization: `Bearer ${token}` } });
            setUnread(current => current.filter(n => n.id !== item.id));
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
            setUnread([]);
            setUnreadTotal(0);
            announceNotificationsChanged();
        } catch {
            setActionError('Notifications could not be marked as read. Try again.');
        }
    };

    const renderItem = (item: NotificationItem) => (
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
    );

    if (loading) return <section className="notification-list-card"><p className="notification-list-state">Loading notifications…</p></section>;

    if (error) {
        return <section className="notification-list-card" role="alert">
            <p className="notification-list-state notification-list-error">{error}</p>
            <button type="button" className="btn-outline" onClick={() => void load()}><RefreshCw size={15} /> Retry</button>
        </section>;
    }

    const historyOpen = historyShown && historyPages > 0;

    // The first show reads a chunk; after that it only brings back what is already loaded.
    const toggleHistory = () => {
        if (historyShown) setHistoryShown(false);
        else if (historyPages > 0) setHistoryShown(true);
        else void loadHistory();
    };

    return <section className="notification-list-card">
        <div className="notification-list-toolbar">
            <span>{unreadTotal > 0 ? `${unreadTotal} unread` : 'All caught up'}</span>
            <div className="notification-list-actions">
                <button type="button" className="btn-outline" onClick={toggleHistory} disabled={loadingHistory} aria-pressed={historyShown}>
                    {historyShown ? <><EyeOff size={15} /> Hide Viewed</> : <><Eye size={15} /> Show Viewed</>}
                </button>
                <button type="button" className="btn-outline" onClick={() => void load()}><RefreshCw size={15} /> Refresh</button>
                <button type="button" className="btn-outline" onClick={() => void markAllRead()} disabled={unread.length === 0}><CheckCheck size={15} /> Mark all as read</button>
            </div>
        </div>
        {actionError && <p className="notification-list-error" role="alert">{actionError}</p>}
        {unread.length === 0
            ? <p className="notification-list-state">No unread notifications. {isAdmin
                ? 'New and cancelled customer reservations and other admins’ catalogue changes will appear here.'
                : 'Updates about your reservations, returns, fines and new books will appear here.'}</p>
            : <ul className="notification-list-items">{unread.map(renderItem)}</ul>}
        {historyOpen && <>
            <h2 className="notification-list-history-heading">History</h2>
            {history.length === 0
                ? <p className="notification-list-state">No earlier notifications.</p>
                : <ul className="notification-list-items notification-list-history">{history.map(renderItem)}</ul>}
        </>}
        {(!historyShown || historyHasMore) && <div className="notification-list-more">
            <button type="button" className="btn-outline" onClick={historyShown ? () => void loadHistory() : toggleHistory} disabled={loadingHistory}>
                <History size={15} /> {loadingHistory ? 'Loading…' : historyOpen ? 'Load more history' : 'Load history'}
            </button>
        </div>}
    </section>;
};

export default NotificationList;
