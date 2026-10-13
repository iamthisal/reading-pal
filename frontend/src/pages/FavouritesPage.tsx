import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { Heart } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { useFavourites } from '../contexts/FavouritesContext';
import { useBorrow } from '../hooks/useBorrow';
import { INVENTORY_API_BASE_URL } from '../config/api';
import PageBanner from '../components/layout/PageBanner';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import BorrowToast from '../components/BorrowToast';
import { BookListRow } from '../components/BookList/BookList';
import { BookListSkeleton } from '../components/Skeleton/Skeleton';
import type { ListBook } from '../components/BookList/BookList';
import './FavouritesPage.css';

const FavouritesPage = () => {
    const { token } = useAuth();
    const { isFavourite } = useFavourites();
    const [books, setBooks] = useState<ListBook[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [reload, setReload] = useState(0);
    const borrowing = useBorrow(() => setReload(value => value + 1));

    useEffect(() => {
        const controller = new AbortController();
        axios.get<ListBook[]>(`${INVENTORY_API_BASE_URL}/api/favourites`, { headers: { Authorization: `Bearer ${token}` }, signal: controller.signal })
            .then(response => { setBooks(response.data); setError(''); })
            .catch(() => { if (!controller.signal.aborted) setError('Unable to load your favourites. Please try again.'); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [token, reload]);

    // A book disappears from the list as soon as its heart is cleared.
    const visible = books.filter(book => isFavourite(book.id));

    return (
        <div className="app-shell">
            <AppTopbar />
            <LibrarySidenav active="favourites" />
            <BorrowToast success={borrowing.success} error={borrowing.error} onDismiss={borrowing.dismiss} />
            <main className="app-main app-main-banner favourites-main">
                <PageBanner eyebrow="Your shortlist" title="Favourites" icon={<Heart size={38} />}
                    text="Books you have saved with the heart, most recently added first.">
                    {!error && (
                        <div className="discover-stats" aria-busy={loading}>
                            {loading ? <span className="skeleton skeleton-stat" /> : <span>{visible.length} {visible.length === 1 ? 'book' : 'books'}</span>}
                        </div>
                    )}
                </PageBanner>

                {error && <p role="alert" className="favourites-error">{error}</p>}
                {loading && <BookListSkeleton rows={4} label="Loading your favourites…" />}

                {!loading && !error && visible.length === 0 && (
                    <div className="favourites-empty">
                        <Heart size={30} aria-hidden="true" />
                        <h2>No favourites yet</h2>
                        <p>Press the heart on any book to keep it here for later.</p>
                        <Link to="/home" className="shelf-borrow">Browse the catalogue</Link>
                    </div>
                )}

                {visible.length > 0 && (
                    <ul className="book-list" aria-label="Favourite books">
                        {visible.map(book => <BookListRow key={book.id} book={book} canBorrow={borrowing.canBorrow}
                            pending={borrowing.pending} onBorrow={bookId => void borrowing.borrow(bookId)} />)}
                    </ul>
                )}
            </main>
        </div>
    );
};

export default FavouritesPage;
