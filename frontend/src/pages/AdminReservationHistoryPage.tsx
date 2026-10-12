import AdminSidebar from '../components/layout/AdminSidebar';
import { refreshWhileVisible } from '../utils/refreshWhileVisible';
import { useEffect, useState } from 'react';
import axios from 'axios';
import { useAuth } from '../contexts/AuthContext';
import { LENDING_API_BASE_URL } from '../config/api';
import NotificationBell from '../components/NotificationBell';

interface HistoryRecord {
    id: number; userName: string; bookTitle: string; status: string; reservationDate: string;
    checkoutDate: string | null; dueDate: string | null; returnDate: string | null;
    daysOverdue: number | null; fineAmount: number | null; fineStatus: string | null;
}
const format = (value: string | null) => value ? new Date(value).toLocaleString() : '—';

export default function AdminReservationHistoryPage() {
    const { token } = useAuth();
    const [records, setRecords] = useState<HistoryRecord[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [refresh, setRefresh] = useState(0);
    const [filter, setFilter] = useState('All');
    useEffect(() => {
        const controller = new AbortController();
        const load = async (background = false) => {
            if (!background) setLoading(true); setError('');
            try {
                const response = await axios.get<HistoryRecord[]>(`${LENDING_API_BASE_URL}/api/reservations/history`, {
                    headers: { Authorization: `Bearer ${token}` }, signal: controller.signal,
                });
                if (!controller.signal.aborted) setRecords(response.data);
            } catch (err) {
                if (!controller.signal.aborted) {
                    setRecords([]);
                    setError(axios.isAxiosError(err) ? err.response?.data?.message || 'Unable to load reservation history. Please refresh.' : 'Unable to load reservation history.');
                }
            } finally { if (!controller.signal.aborted && !background) setLoading(false); }
        };
        void load();
        const stopRefresh = refreshWhileVisible(() => load(true));
        return () => { stopRefresh(); controller.abort(); };
    }, [token, refresh]);
    const visible = records.filter(record => filter === 'All' || record.status === filter);
    return <div className="admin-shell">
        <AdminSidebar active="history" />
        <main className="admin-main admin-users-main">
            <header className="admin-topbar admin-users-topbar"><div><p className="admin-eyebrow">Counter operations</p><h1>Reservation history</h1>
                <p className="admin-users-subtitle">Returned books and cancelled reservations, newest reservation first. Times are shown in your local timezone.</p></div>
                <div className="admin-topbar-actions"><button type="button" className="btn-outline" disabled={loading} onClick={() => setRefresh(value => value + 1)}>{loading ? 'Loading…' : 'Refresh'}</button><NotificationBell /></div></header>
            <section className="admin-users-panel glass-panel" aria-label="Reservation history" aria-busy={loading}>
                <div className="admin-users-panel-heading"><h2>Past records</h2><label>Status <select value={filter} onChange={event => setFilter(event.target.value)}>
                    <option value="All">All</option><option value="Returned">Returned</option><option value="Cancelled">Cancelled</option></select></label></div>
                <p className="admin-users-subtitle">Cancelled includes admin rejections and user cancellations. The cancellation date and who cancelled are not recorded.</p>
                {error ? <p role="alert" className="error-message">{error}</p> : loading ? <p role="status">Loading history…</p> : visible.length === 0 ? <p role="status">No records match this status.</p> :
                    <div className="admin-users-table-wrap" style={{ overflowX: 'auto' }} tabIndex={0} role="region" aria-label="Past reservation records">
                        <table className="admin-users-table admin-reservations-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                            <thead><tr>{['Reservation', 'User name', 'Book title', 'Status', 'Reserved', 'Checkout', 'Due date', 'Returned', 'Days overdue', 'Fine (Rs.)', 'Fine status'].map(title => <th scope="col" key={title}>{title}</th>)}</tr></thead>
                            <tbody>{visible.map(record => <tr key={record.id}>
                                <td>#{record.id}</td><td>{record.userName}</td><td>{record.bookTitle}</td>
                                <td><span className="borrow-status">{record.status}</span></td><td>{format(record.reservationDate)}</td>
                                <td>{format(record.checkoutDate)}</td><td>{format(record.dueDate)}</td><td>{format(record.returnDate)}</td>
                                <td>{record.daysOverdue ?? '—'}</td><td>{record.fineAmount?.toFixed(2) ?? '—'}</td><td>{record.fineStatus ?? '—'}</td>
                            </tr>)}</tbody>
                        </table>
                    </div>}
            </section>
        </main>
    </div>;
}
