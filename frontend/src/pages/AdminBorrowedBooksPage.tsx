import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { BookMarked, BookOpen, BookPlus, LayoutDashboard, LogOut, Users } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { LENDING_API_BASE_URL } from '../config/api';

interface BorrowRecord {
    id: number;
    userName: string;
    bookTitle: string;
    checkoutDate: string;
    dueDate: string;
    isOverdue: boolean;
}

const timestampFormat = new Intl.DateTimeFormat(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit', timeZoneName: 'short',
});

export default function AdminBorrowedBooksPage() {
    const { token, logout } = useAuth();
    const [reservations, setReservations] = useState<BorrowRecord[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [refresh, setRefresh] = useState(0);
    useEffect(() => {
        const controller = new AbortController();
        const fetchReservations = async () => {
            setIsLoading(true);
            setError('');
            try {
                const response = await axios.get<BorrowRecord[]>(`${LENDING_API_BASE_URL}/api/reservations/borrowed`, {
                    headers: { Authorization: `Bearer ${token}` },
                    signal: controller.signal,
                });
                if (!controller.signal.aborted) setReservations(response.data);
            } catch (err) {
                if (!controller.signal.aborted) {
                    setReservations([]);
                    setError(axios.isAxiosError(err) && err.response?.status === 403
                        ? 'You do not have permission to view borrowed books.'
                        : 'Unable to load borrowed books. Please try refreshing.');
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
                    <Link to="/admin/reservations/pending" className="admin-nav-item"><BookMarked size={16} />Pending reservations</Link>
                    <Link to="/admin/borrowed" className="admin-nav-item admin-nav-item-active" aria-current="page"><BookOpen size={16} />Borrowed books</Link>
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
                        <h1>Borrowed books</h1>
                        <p className="admin-users-subtitle">Current loans, earliest due date first. Times are shown in your local timezone.</p>
                    </div>
                    <button type="button" className="btn-outline" disabled={isLoading} onClick={() => setRefresh(value => value + 1)}>
                        {isLoading ? 'Loading…' : 'Refresh'}
                    </button>
                </header>
                <section className="admin-users-panel glass-panel" aria-label="Current borrowed books" aria-busy={isLoading}>
                    <div className="admin-users-panel-heading">
                        <h2>Currently borrowed</h2>
                        {!isLoading && !error && <span className="admin-users-count">{reservations.length} borrowed</span>}
                    </div>


                    {error ? <p role="alert" className="error-message admin-users-error">{error}</p>
                        : isLoading ? <p role="status">Loading borrowed books…</p>
                        : reservations.length === 0 ? <p role="status">No books are currently borrowed.</p>
                        : <div className="admin-users-table-wrap" style={{ overflowX: 'auto' }}>
                            <table className="admin-users-table admin-reservations-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                                <thead><tr><th scope="col">User name</th><th scope="col">Book title</th><th scope="col">Checkout date</th><th scope="col" aria-sort="ascending">Due date · earliest first</th><th scope="col">Status</th></tr></thead>
                                <tbody>{reservations.map(reservation => (
                                    <tr key={reservation.id}>
                                        <td>{reservation.userName}</td>
                                        <td>{reservation.bookTitle}</td>
                                        <td><time dateTime={reservation.checkoutDate}>{timestampFormat.format(new Date(reservation.checkoutDate))}</time></td>
                                        <td><time dateTime={reservation.dueDate}>{timestampFormat.format(new Date(reservation.dueDate))}</time></td>
                                        <td><span className={`borrow-status ${reservation.isOverdue ? 'borrow-status-overdue' : ''}`}>
                                            {reservation.isOverdue ? 'Overdue' : 'Borrowed'}
                                        </span></td>
                                    </tr>
                                ))}</tbody>
                            </table>
                        </div>}
                </section>
            </main>
        </div>
    );
}
