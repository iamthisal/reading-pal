import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { BookMarked, BookOpen, Clock3, Heart, History, Library, LogOut, RefreshCw, User, Wallet } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { LENDING_API_BASE_URL } from '../config/api';
import './MyBorrowingsPage.css';

interface Loan {
    id: number; bookTitle: string; checkoutDate: string; dueDate: string; returnDate: string | null;
    status: string; daysOverdue: number; fineAmount: number; fineStatus: string;
}
interface PendingReservation { id: number; bookTitle: string; reservationDate: string; status: string; canCancel: boolean }
interface Borrowings { cancelled?: Omit<PendingReservation, 'canCancel'>[]; pending?: PendingReservation[]; active: Loan[]; history: Loan[]; totalUnpaid: number; estimatedActiveFines: number }
const date = (value: string) => new Intl.DateTimeFormat('en-LK', {
    year: 'numeric', month: 'short', day: 'numeric', timeZone: 'Asia/Colombo',
}).format(new Date(value));
const money = (value: number) => `Rs. ${value.toFixed(2)}`;

export default function MyBorrowingsPage() {
    const { token, user, logout } = useAuth();
    const [data, setData] = useState<Borrowings | null>(null);
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(true);
    const [refresh, setRefresh] = useState(0);
    const [cancellingId, setCancellingId] = useState<number | null>(null);
    const [actionMessage, setActionMessage] = useState('');
    const [actionError, setActionError] = useState('');
    useEffect(() => {
        if (!actionMessage) return;
        const timeout = window.setTimeout(() => setActionMessage(''), 5000);
        return () => window.clearTimeout(timeout);
    }, [actionMessage]);
    const cancelReservation = async (reservation: PendingReservation) => {
        if (cancellingId !== null || !reservation.canCancel) return;
        if (!window.confirm(`Cancel your reservation for "${reservation.bookTitle}"?`)) return;
        setCancellingId(reservation.id);
        setActionMessage('');
        setActionError('');
        try {
            await axios.post(`${LENDING_API_BASE_URL}/api/my-borrowings/reservations/${reservation.id}/cancel`, {},
                { headers: { Authorization: `Bearer ${token}` } });
            setActionMessage(`Your reservation for "${reservation.bookTitle}" has been cancelled.`);
            setRefresh(value => value + 1);
        } catch (err) {
            setActionError(axios.isAxiosError(err) ? err.response?.data?.message || 'Cancellation could not be confirmed. Refresh and try again.' : 'Unable to cancel this reservation.');
            setRefresh(value => value + 1);
        } finally { setCancellingId(null); }
    };
    useEffect(() => {
        const controller = new AbortController();
        setLoading(true);
        setError('');
        setData(null);
        void axios.get<Borrowings>(`${LENDING_API_BASE_URL}/api/my-borrowings`, {
            headers: { Authorization: `Bearer ${token}` }, signal: controller.signal,
        }).then(response => { if (!controller.signal.aborted) setData(response.data); })
            .catch(err => { if (!controller.signal.aborted) setError(axios.isAxiosError(err) && err.response?.status === 401
                ? 'Your session has expired. Please log in again.' : 'Unable to load your borrowing records. Please try again.'); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [token, refresh]);

    const table = (records: Loan[], past: boolean) => <div className="my-loans-table-wrap" tabIndex={0} role="region" aria-label={past ? 'Borrowing history' : 'Current loans'}><table>
        <caption>{past ? 'Returned books and recorded fines' : 'Current loans and estimated fines'}</caption>
        <thead><tr><th>Book</th><th>Borrowed</th><th>Due date</th>{past && <th>Returned</th>}
            <th>Days overdue</th><th>{past ? 'Fine' : 'Estimated fine'}</th><th>Status</th></tr></thead>
        <tbody>{records.map(loan => <tr key={loan.id}>
            <td className="my-loans-title">{loan.bookTitle}</td><td>{date(loan.checkoutDate)}</td><td>{date(loan.dueDate)}</td>
            {past && <td>{loan.returnDate ? date(loan.returnDate) : '—'}</td>}
            <td>{loan.daysOverdue}</td><td className="my-loans-money">{money(loan.fineAmount)}</td>
            <td><span className={`my-loans-badge ${(past ? loan.fineStatus === 'Unpaid' : loan.daysOverdue > 0) ? 'my-loans-badge-warning' : ''}`}>{past ? `Returned · ${loan.fineStatus}` : loan.status === 'Return pending' ? loan.status : loan.daysOverdue > 0 ? 'Overdue' : loan.status}</span></td>
        </tr>)}</tbody>
    </table></div>;

    return <div className="discover-shell my-loans-shell">
        <aside className="discover-sidebar">
            <Link to="/home" className="discover-brand"><BookMarked size={24} /><span>Reading Pal</span></Link>
            <nav className="discover-nav" aria-label="Library navigation">
                <span className="discover-nav-label">Menu</span>
                <Link to="/home" className="discover-nav-item"><BookOpen size={16} />Discover</Link>
                <Link to="/home#recommendations" className="discover-nav-item"><Library size={16} />My Library</Link>
                <Link to="/home#recommendations" className="discover-nav-item"><Heart size={16} />Favorite</Link>
                <Link to="/my-borrowings" className="discover-nav-item discover-nav-item-active" aria-current="page"><History size={16} />My borrowings &amp; fines</Link>
            </nav>
            <div className="discover-sidebar-bottom">
                <Link to="/profile" className="discover-nav-item"><User size={16} />My Profile</Link>
                <button type="button" onClick={logout} className="discover-nav-item discover-nav-button"><LogOut size={16} />Log out</button>
            </div>
        </aside>
        <main className="discover-main profile-main my-loans-page">
        <header className="profile-topbar"><div className="profile-user-chip"><span className="discover-avatar">{user?.email?.slice(0, 1).toUpperCase() || 'R'}</span><span>{user?.email || 'Reader'}</span></div></header>
        <section className="profile-hero my-loans-hero">
            <div><p className="discover-eyebrow">Your reading journey</p><h1>My borrowings<br />&amp; fines</h1><p>Keep track of your next return and look back at the books you’ve borrowed.</p></div>
            <div className="profile-hero-mark" aria-hidden="true"><BookOpen size={38} /></div>
        </section>
        <div className="my-loans-toolbar"><p><Clock3 size={16} aria-hidden="true" />Dates and overdue days use Sri Lanka time.</p>
            <button type="button" className="btn-primary profile-save-button my-loans-refresh" disabled={loading} onClick={() => setRefresh(value => value + 1)}><RefreshCw size={16} />{loading ? 'Refreshing…' : 'Refresh'}</button></div>
        <div className="my-loans-note"><strong>Late returns: Rs. 10 per day after the due date.</strong> Current fines are estimates until your return is processed.</div>
        {loading && <div className="my-loans-panel my-loans-empty" role="status"><BookOpen size={28} aria-hidden="true" /><p>Loading your borrowing records…</p></div>}
        {error && <div role="alert" className="my-loans-error">{error}</div>}
        {actionError && <div role="alert" className="my-loans-error">{actionError}</div>}
        {actionMessage && <div role="status" className="my-loans-note my-loans-success">{actionMessage}</div>}
        {data && <>
            <section className="my-loans-panel"><div className="my-loans-panel-heading"><div><p className="discover-eyebrow">Awaiting pickup</p><h2>Pending reservations</h2></div><Clock3 size={22} aria-hidden="true" /></div>
                <p>You can cancel until an admin starts accepting your reservation.</p>
                {!data.pending ? <p role="status">Restart Lending with the latest changes to load your reservations.</p> : data.pending.length === 0 ? <div className="my-loans-empty"><p>You have no pending reservations.</p></div> :
                    <div className="my-loans-table-wrap" tabIndex={0} role="region" aria-label="Pending reservations"><table>
                        <caption>Your reservations, oldest first</caption>
                        <thead><tr><th scope="col">Book</th><th scope="col">Reserved</th><th scope="col">Status</th><th scope="col">Action</th></tr></thead>
                        <tbody>{data.pending.map(reservation => <tr key={reservation.id}>
                            <td className="my-loans-title">{reservation.bookTitle}</td><td>{date(reservation.reservationDate)}</td>
                            <td><span className="my-loans-badge">{reservation.status === 'Accepting' ? 'Being accepted' : 'Pending'}</span></td>
                            <td>{reservation.canCancel ? <button type="button" className="my-loans-cancel" disabled={cancellingId !== null}
                                aria-label={`Cancel reservation for ${reservation.bookTitle}`} onClick={() => void cancelReservation(reservation)}>
                                {cancellingId === reservation.id ? 'Cancelling…' : 'Cancel reservation'}</button> : 'Cancellation unavailable'}</td>
                        </tr>)}</tbody>
                    </table></div>}
            </section>
            <div className="my-loans-summary">
                <section><BookOpen size={22} aria-hidden="true" /><h2>Current loans</h2><strong>{data.active.length}</strong><p>Books awaiting return</p></section>
                <section><Wallet size={22} aria-hidden="true" /><h2>Unpaid fines</h2><strong>{money(data.totalUnpaid)}</strong><p>Recorded on returned books</p></section>
                <section><Clock3 size={22} aria-hidden="true" /><h2>Estimated current fines</h2><strong>{money(data.estimatedActiveFines)}</strong><p>For books still on loan</p></section>
            </div>
            <section className="my-loans-panel"><div className="my-loans-panel-heading"><div><p className="discover-eyebrow">On your bookshelf</p><h2>Current loans</h2></div><span className="profile-account-label">{data.active.length} active</span></div>
                {data.active.length ? table(data.active, false) : <div className="my-loans-empty"><BookOpen size={28} aria-hidden="true" /><h3>No current loans</h3><p>Your next chapter is waiting in the library.</p><Link to="/home" className="my-loans-link">Explore books →</Link></div>}</section>
            <section className="my-loans-panel"><div className="my-loans-panel-heading"><div><p className="discover-eyebrow">Your past reads</p><h2>Borrowing history</h2></div><History size={22} aria-hidden="true" /></div>
                {data.history.length ? table(data.history, true) : <div className="my-loans-empty"><History size={28} aria-hidden="true" /><h3>No returned books yet</h3><p>Your completed loans and recorded fines will appear here.</p></div>}
                <h3 className="my-loans-cancelled-heading">Cancelled reservations</h3>
                <p className="my-loans-history-note">Includes reservations cancelled by you or declined by an admin. Sorted by reservation date, newest first; cancellation dates were not recorded.</p>
                {data.cancelled === undefined ? <p>Cancelled history is unavailable. Restart Lending with the latest changes and refresh.</p> : data.cancelled.length === 0 ? <p className="my-loans-empty">No cancelled reservations.</p> :
                    <div className="my-loans-table-wrap" tabIndex={0} role="region" aria-label="Cancelled reservation history"><table>
                        <thead><tr><th scope="col">Book</th><th scope="col">Reserved</th><th scope="col">Status</th></tr></thead>
                        <tbody>{data.cancelled.map(record => <tr key={record.id}><td className="my-loans-title">{record.bookTitle}</td>
                            <td>{date(record.reservationDate)}</td><td><span className="my-loans-badge my-loans-badge-warning">Cancelled</span></td></tr>)}</tbody>
                    </table></div>}
            </section>
        </>}
    </main></div>;
}
