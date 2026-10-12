import { BookOpen as BookOpenIcon } from 'lucide-react';
import AppTopbar from '../components/layout/AppTopbar';
import PageBanner from '../components/layout/PageBanner';
import DateTime from '../components/DateTime';
import AdminSidebar from '../components/layout/AdminSidebar';
import { refreshWhileVisible } from '../utils/refreshWhileVisible';
import { useEffect, useState } from 'react';
import axios from 'axios';
import { useAuth } from '../contexts/AuthContext';
import { LENDING_API_BASE_URL } from '../config/api';

interface BorrowRecord {
    id: number;
    userName: string;
    bookTitle: string;
    checkoutDate: string;
    dueDate: string;
    isOverdue: boolean;
    isReturning: boolean;
    daysOverdue?: number;
    fineAmount?: number;
}

export default function AdminBorrowedBooksPage() {
    const { token } = useAuth();
    const [reservations, setReservations] = useState<BorrowRecord[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [refresh, setRefresh] = useState(0);
    const [processingId, setProcessingId] = useState<number | null>(null);
    const [success, setSuccess] = useState('');
    const [returnError, setReturnError] = useState('');

    const returnBook = async (record: BorrowRecord) => {
        if (processingId !== null) return;
        if (!window.confirm(`Mark "${record.bookTitle}" as returned? Late returns cost Rs. 10 per day after the due date (Sri Lanka time).`)) return;
        setProcessingId(record.id);
        setReturnError('');
        setSuccess('');
        try {
            const { data } = await axios.post<{ fineAmount: number; daysOverdue: number; fineStatus: string | null }>(
                `${LENDING_API_BASE_URL}/api/returns/${record.id}`, {},
                { headers: { Authorization: `Bearer ${token}` } });
            setReservations(current => current.filter(item => item.id !== record.id));
            setSuccess(data.fineAmount > 0
                ? `Book returned. ${data.daysOverdue} day(s) overdue. Fine: Rs. ${data.fineAmount.toFixed(2)} — ${data.fineStatus}.`
                : 'Book returned. No fine is due.');
        } catch (err) {
            setReturnError(axios.isAxiosError(err)
                ? err.response?.data?.message || 'Return could not be confirmed. Refresh and retry Return.'
                : 'Unable to return this book. Please retry.');
            setRefresh(value => value + 1);
        } finally {
            setProcessingId(null);
        }
    };
    useEffect(() => {
        const controller = new AbortController();
        const fetchReservations = async (background = false) => {
            if (!background) setIsLoading(true);
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
                if (!controller.signal.aborted && !background) setIsLoading(false);
            }
        };
        if (token) void fetchReservations();
        const stopRefresh = refreshWhileVisible(() => token ? fetchReservations(true) : Promise.resolve());
        return () => { stopRefresh(); controller.abort(); };
    }, [token, refresh]);

    return (
        <div className="app-shell">
        <AppTopbar />
            <AdminSidebar active="borrowed" />
            <main className="app-main app-main-banner admin-main admin-users-main">
                <PageBanner eyebrow="Counter operations" title="Borrowed books" icon={<BookOpenIcon size={38} />} text={<>Current loans, earliest due date first. Times are shown in your local timezone. Late returns cost Rs. 10 per calendar day after the due date in Sri Lanka.</>}>
                    <div className="page-banner-actions"><button type="button" className="btn-outline" disabled={isLoading || processingId !== null} onClick={() => setRefresh(value => value + 1)}>
                            {isLoading ? 'Loading…' : 'Refresh'}
                        </button></div>
                </PageBanner>
                <section className="admin-users-panel glass-panel" aria-label="Current borrowed books" aria-busy={isLoading}>
                    <div className="admin-users-panel-heading">
                        <h2>Currently borrowed</h2>
                        {!isLoading && !error && <span className="admin-users-count">{reservations.length} borrowed</span>}
                    </div>


                    {success && <p role="status">{success}</p>}
                    {!isLoading && reservations.some(record => !Number.isFinite(record.fineAmount) || !Number.isFinite(record.daysOverdue)) &&
                        <p role="status">Fine details are unavailable from the running Lending service. Restart or rebuild Lending with the latest changes, then refresh.</p>}
                    {returnError && <p role="alert" className="error-message">{returnError}</p>}
                    {error ? <p role="alert" className="error-message admin-users-error">{error}</p>
                        : isLoading ? <p role="status">Loading borrowed books…</p>
                        : reservations.length === 0 ? <p role="status">No books are currently borrowed.</p>
                        : <div className="admin-users-table-wrap" style={{ overflowX: 'auto' }}>
                            <table className="admin-users-table admin-reservations-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                                <thead><tr><th scope="col">User name</th><th scope="col">Book title</th><th scope="col">Checkout date</th><th scope="col" aria-sort="ascending">Due date · earliest first</th><th scope="col">Days overdue</th><th scope="col">Fine (Rs.)</th><th scope="col">Status</th><th scope="col">Action</th></tr></thead>
                                <tbody>{reservations.map(reservation => (
                                    <tr key={reservation.id}>
                                        <td>{reservation.userName}</td>
                                        <td>{reservation.bookTitle}</td>
                                        <td><DateTime value={reservation.checkoutDate} /></td>
                                        <td><DateTime value={reservation.dueDate} /></td>
                                        <td>{typeof reservation.daysOverdue === 'number' && Number.isFinite(reservation.daysOverdue) ? reservation.daysOverdue : 'Unavailable'}</td>
                                        <td>{typeof reservation.fineAmount === 'number' && Number.isFinite(reservation.fineAmount) ? reservation.fineAmount.toFixed(2) : 'Unavailable'}</td>
                                        <td><span className={`borrow-status ${reservation.isOverdue ? 'borrow-status-overdue' : ''}`}>
                                            {reservation.isReturning ? 'Return pending' : reservation.isOverdue ? 'Overdue' : 'Borrowed'}
                                        </span></td>
                                        <td><button type="button" className="btn-primary" disabled={processingId !== null}
                                            onClick={() => void returnBook(reservation)}>
                                            {processingId === reservation.id ? 'Returning…' : reservation.isReturning ? 'Retry Return' : 'Return'}
                                        </button></td>
                                    </tr>
                                ))}</tbody>
                            </table>
                        </div>}
                </section>
            </main>
        </div>
    );
}
