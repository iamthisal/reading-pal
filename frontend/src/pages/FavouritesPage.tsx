import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { Heart } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { useFavourites } from '../contexts/FavouritesContext';
import { useBorrow } from '../hooks/useBorrow';
import { INVENTORY_API_BASE_URL } from '../config/api';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import BorrowToast from '../components/BorrowToast';
import FavouriteButton from '../components/Favourites/FavouriteButton';
import '../components/BookShelf/BookShelf.css';
import './FavouritesPage.css';

interface Book {
    id: number; title: string; author: string; genre: string; coverImageUrl?: string | null;
    totalCopies: number; availableCopies: number;
}

const Cover = ({ book }: { book: Book }) => {
    const [failed, setFailed] = useState(false);
    return book.coverImageUrl && !failed
        ? <img className="favourites-cover" src={book.coverImageUrl} alt={`${book.title} cover`} loading="lazy" onError={() => setFailed(true)} />
        : <div className="favourites-cover favourites-cover-fallback" aria-hidden="true">{book.title}</div>;
};

const FavouritesPage = () => {
    const { token } = useAuth();
    const { isFavourite } = useFavourites();
    const [books, setBooks] = useState<Book[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [reload, setReload] = useState(0);
    const borrowing = useBorrow(() => setReload(value => value + 1));

    useEffect(() => {
        const controller = new AbortController();
        axios.get<Book[]>(`${INVENTORY_API_BASE_URL}/api/favourites`, { headers: { Authorization: `Bearer ${token}` }, signal: controller.signal })
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
            <main className="app-main favourites-main">
                <header className="favourites-header">
                    <div>
                        <p className="favourites-eyebrow">Your shortlist</p>
                        <h1>Favourites</h1>
                        <p>Books you have saved with the heart, most recently added first.</p>
                    </div>
                    {!loading && visible.length > 0 && <span className="favourites-count">{visible.length} {visible.length === 1 ? 'book' : 'books'}</span>}
                </header>

                {error && <p role="alert" className="favourites-error">{error}</p>}
                {loading && <p role="status">Loading your favourites…</p>}

                {!loading && !error && visible.length === 0 && (
                    <div className="favourites-empty">
                        <Heart size={30} aria-hidden="true" />
                        <h2>No favourites yet</h2>
                        <p>Press the heart on any book to keep it here for later.</p>
                        <Link to="/home" className="shelf-borrow">Browse the catalogue</Link>
                    </div>
                )}

                {visible.length > 0 && (
                    <ul className="favourites-list" aria-label="Favourite books">
                        {visible.map(book => {
                            const available = book.availableCopies > 0;
                            const busy = Boolean(borrowing.pending[book.id]);
                            return (
                                <li key={book.id} className="favourites-row">
                                    <Cover book={book} />
                                    <div className="favourites-info">
                                        <h2>{book.title}</h2>
                                        <p>{book.author}{book.genre && <><span aria-hidden="true"> • </span>{book.genre}</>}</p>
                                        <div className="favourites-stock">
                                            <span className={`shelf-status${available ? '' : ' shelf-status-out'}`}>{available ? 'Available' : 'Not available now'}</span>
                                            <span>{book.availableCopies} of {book.totalCopies} {book.totalCopies === 1 ? 'copy' : 'copies'} on the shelf</span>
                                        </div>
                                    </div>
                                    <div className="favourites-actions">
                                        {borrowing.canBorrow && available && (
                                            <button type="button" className="shelf-borrow" disabled={busy} aria-label={`Borrow ${book.title}`}
                                                onClick={() => void borrowing.borrow(book.id)}>{busy ? 'Borrowing…' : 'Borrow'}</button>
                                        )}
                                        <FavouriteButton bookId={book.id} title={book.title} variant="plain" />
                                    </div>
                                </li>
                            );
                        })}
                    </ul>
                )}
            </main>
        </div>
    );
};

export default FavouritesPage;
