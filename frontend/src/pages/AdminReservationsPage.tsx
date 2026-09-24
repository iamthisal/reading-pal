import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { BookMarked, BookOpen, BookPlus, LayoutDashboard, LogOut, Users } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { LENDING_API_BASE_URL } from '../config/api';

interface PendingReservation {
    id: number;
    userName: string;
    bookTitle: string;
    reservationDate: string;
}

const timestampFormat = new Intl.DateTimeFormat(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit', timeZoneName: 'short',
});

export default function AdminReservationsPage() {
    const { token, logout } = useAuth();
    const [reservations, setReservations] = useState<PendingReservation[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [refresh, setRefresh] = useState(0);
    const [processingId, setProcessingId] = useState<number | null>(null);
    const [processingAction, setProcessingAction] = useState<'accept' | 'reject' | null>(null);
    const [actionError, setActionError] = useState('');
    const [success, setSuccess] = useState('');

    const processReservation = async (reservation: PendingReservation, action: 'accept' | 'reject') => {
        if (processingId !== null) return;
        setProcessingId(reservation.id);
        setProcessingAction(action);
        setActionError('');
        setSuccess('');
        try {
            await axios.post(`${LENDING_API_BASE_URL}/api/reservations/${reservation.id}/${action}`, {}, {
                headers: { Authorization: `Bearer ${token}` },
            });
            setReservations(current => current.filter(row => row.id !== reservation.id));
            setSuccess(action === 'accept'
                ? `${reservation.bookTitle} is now borrowed by ${reservation.userName} for 14 days. Notification event queued.`
                : `Reservation for ${reservation.bookTitle} by ${reservation.userName} was cancelled. Notification event queued.`);
        } catch (err) {
            const status = axios.isAxiosError(err) ? err.response?.status : undefined;
            if (status === 409 || status === 404) {
                setActionError('This reservation is no longer pending. The queue has been refreshed.');
                setRefresh(value => value + 1);
            } else {
                setActionError('Could not confirm the action. Refresh the queue before trying again.');
            }
        } finally {
            setProcessingId(null);
            setProcessingAction(null);
        }
    };

    useEffect(() => {
        const controller = new AbortController();
        const fetchReservations = async () => {
            setIsLoading(true);
            setError('');
            try {
                const response = await axios.get<PendingReservation[]>(`${LENDING_API_BASE_URL}/api/reservations/pending`, {
                    headers: { Authorization: `Bearer ${token}` },
                    signal: controller.signal,
                });
                if (!controller.signal.aborted) setReservations(response.data);
            } catch (err) {
                if (!controller.signal.aborted) {
                    setReservations([]);
                    setError(axios.isAxiosError(err) && err.response?.status === 403
                        ? 'You do not have permission to view reservations.'
                        : 'Unable to load pending reservations. Please try refreshing.');
                }
            } finally {
                if (!controller.signal.aborted) setIsLoading(false);
            }
        };
        if (token) void fetchReservations();
        return () => controller.abort();
    }, [token, refresh]);

    return (
        <div className="admin-shell">
            <aside className="admin-sidebar">
                <Link to="/admin/dashboard" className="admin-brand"><BookMarked size={23} /><span>Reading Pal</span></Link>
                <nav className="admin-nav" aria-label="Administration navigation">
                    <span className="admin-nav-label">Workspace</span>
                    <Link to="/admin/dashboard" className="admin-nav-item"><LayoutDashboard size={16} />Overview</Link>
                    <Link to="/admin/users/pending" className="admin-nav-item"><Users size={16} />User approvals</Link>
                    <Link to="/admin/users/active" className="admin-nav-item"><Users size={16} />Active users</Link>
                    <Link to="/admin/books" className="admin-nav-item"><BookPlus size={16} />Book inventory</Link>
                    <Link to="/admin/reservations/pending" className="admin-nav-item admin-nav-item-active" aria-current="page"><BookMarked size={16} />Pending reservations</Link>
                    <Link to="/admin/borrowed" className="admin-nav-item"><BookOpen size={16} />Borrowed books</Link>
                    <Link to="/home" className="admin-nav-item"><BookOpen size={16} />Public catalogue</Link>
                </nav>
                <div className="admin-sidebar-bottom">
                    <button type="button" onClick={logout} className="admin-nav-item admin-nav-button"><LogOut size={16} />Log out</button>
                </div>
            </aside>
            <main className="admin-main admin-users-main">
                <header className="admin-topbar admin-users-topbar">
                    <div>
                        <p className="admin-eyebrow">Counter operations</p>
                        <h1>Pending reservations</h1>
                        <p className="admin-users-subtitle">Reservations awaiting pickup, oldest first. Times are shown in your local timezone.</p>
                    </div>
                    <button type="button" className="btn-outline" disabled={isLoading || processingId !== null} onClick={() => setRefresh(value => value + 1)}>
                        {isLoading ? 'Loading…' : 'Refresh'}
                    </button>
                </header>
                <section className="admin-users-panel glass-panel" aria-label="Pending reservation queue" aria-busy={isLoading}>
                    <div className="admin-users-panel-heading">
                        <h2>Awaiting pickup</h2>
                        {!isLoading && !error && <span className="admin-users-count">{reservations.length} pending</span>}
                    </div>
                    {actionError && <p role="alert" className="error-message admin-users-error">{actionError}</p>}
                    {success && <p role="status">{success}</p>}
                    {error ? <p role="alert" className="error-message admin-users-error">{error}</p>
                        : isLoading ? <p role="status">Loading pending reservations…</p>
                        : reservations.length === 0 ? <p role="status">No pending reservations.</p>
                        : <div className="admin-users-table-wrap" style={{ overflowX: 'auto' }}>
                            <table className="admin-users-table admin-reservations-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                                <thead><tr><th scope="col">User name</th><th scope="col">Book title</th><th scope="col" aria-sort="ascending">Reserved at · oldest first</th><th scope="col">Action</th></tr></thead>
                                <tbody>{reservations.map(reservation => (
                                    <tr key={reservation.id}>
                                        <td>{reservation.userName}</td>
                                        <td>{reservation.bookTitle}</td>
                                        <td><time dateTime={reservation.reservationDate}>{timestampFormat.format(new Date(reservation.reservationDate))}</time></td>
                                        <td><div className="reservation-actions"><button type="button" className="reservation-accept-button" disabled={processingId !== null}
                                            aria-label={`Accept ${reservation.bookTitle} for ${reservation.userName}`}
                                            onClick={() => void processReservation(reservation, 'accept')}>
                                            {processingId === reservation.id && processingAction === 'accept' ? 'Accepting…' : 'Accept'}
                                        </button>
                                        <button type="button" className="reservation-accept-button reservation-reject-button" disabled={processingId !== null}
                                            aria-label={`Reject ${reservation.bookTitle} for ${reservation.userName}`}
                                            onClick={() => void processReservation(reservation, 'reject')}>
                                            {processingId === reservation.id && processingAction === 'reject' ? 'Rejecting…' : 'Reject'}
                                        </button></div></td>
                                    </tr>
                                ))}</tbody>
                            </table>
                        </div>}
                </section>
            </main>
        </div>
    );
}
