import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import axios from 'axios';
import { Heart } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { useFavourites } from '../contexts/FavouritesContext';
import { useBorrow } from '../hooks/useBorrow';
import { INVENTORY_API_BASE_URL } from '../config/api';
import { authorPath } from '../utils/links';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import BackButton from '../components/BackButton';
import BorrowToast from '../components/BorrowToast';
import { BookShelf } from '../components/BookShelf/BookShelf';
import { DetailSkeleton, LazyImage } from '../components/Skeleton/Skeleton';
import './DetailPage.css';

interface Book {
    id: number; title: string; author: string; isbn: string; genre: string; coverImageUrl?: string | null;
    description?: string | null; totalCopies: number; availableCopies: number; createdAt: string;
}

const BookPage = () => {
    const { bookId } = useParams();
    const { user } = useAuth();
    const { isFavourite, toggle } = useFavourites();
    const [book, setBook] = useState<Book | null>(null);
    const [moreByAuthor, setMoreByAuthor] = useState<Book[]>([]);
    const [status, setStatus] = useState<'loading' | 'ready' | 'missing' | 'error'>('loading');
    const [reload, setReload] = useState(0);
    const borrowing = useBorrow(() => setReload(value => value + 1));

    useEffect(() => {
        const controller = new AbortController();
        const { signal } = controller;
        void (async () => {
            try {
                const found = (await axios.get<Book>(`${INVENTORY_API_BASE_URL}/api/books/${bookId}`, { signal })).data;
                setBook(found);
                setStatus('ready');
                // The author's other books are a bonus: the page still works if they cannot be loaded.
                try {
                    const profile = (await axios.get<{ books: Book[] }>(`${INVENTORY_API_BASE_URL}/api/authors/profile`, { params: { name: found.author }, signal })).data;
                    setMoreByAuthor(profile.books.filter(other => other.id !== found.id));
                } catch {
                    if (!signal.aborted) setMoreByAuthor([]);
                }
            } catch (err) {
                if (signal.aborted) return;
                setStatus(axios.isAxiosError(err) && [400, 404].includes(err.response?.status ?? 0) ? 'missing' : 'error');
            }
        })();
        return () => controller.abort();
    }, [bookId, reload]);

    // Moving from one book to another reuses this page, so start each book from a clean slate.
    const [shownId, setShownId] = useState(bookId);
    if (bookId !== shownId) {
        setShownId(bookId);
        setBook(null);
        setMoreByAuthor([]);
        setStatus('loading');
    }
    useEffect(() => { window.scrollTo({ top: 0 }); }, [bookId]);

    const available = (book?.availableCopies ?? 0) > 0;
    const favourite = book ? isFavourite(book.id) : false;
    const busy = book ? Boolean(borrowing.pending[book.id]) : false;

    return (
        <div className="app-shell">
            <AppTopbar />
            <LibrarySidenav />
            <BorrowToast success={borrowing.success} error={borrowing.error} onDismiss={borrowing.dismiss} />
            <main className="app-main detail-main">
                <BackButton />

                {status === 'loading' && <DetailSkeleton kind="book" />}
                {status === 'error' && <p role="alert" className="detail-status">Unable to load this book. Please try again.</p>}
                {status === 'missing' && (
                    <div className="detail-missing">
                        <h1>Book not found</h1>
                        <p>It may have been removed from the catalogue.</p>
                    </div>
                )}

                {book && status === 'ready' && <>
                    <article className="detail-card">
                        <LazyImage className="detail-cover" src={book.coverImageUrl ?? undefined} alt={`${book.title} cover`}
                            fallback={<div className="detail-cover detail-cover-fallback" aria-hidden="true">{book.title}</div>} />
                        <div className="detail-body">
                            <p className="detail-eyebrow"><Link to={`/search?genre=${encodeURIComponent(book.genre)}`}>{book.genre}</Link></p>
                            <h1>{book.title}</h1>
                            <p className="detail-byline">by <Link to={authorPath(book.author)}>{book.author}</Link></p>

                            <div className="detail-stock">
                                <span className={`shelf-status${available ? '' : ' shelf-status-out'}`}>{available ? 'Available' : 'Not available now'}</span>
                                <span>{book.availableCopies} of {book.totalCopies} {book.totalCopies === 1 ? 'copy' : 'copies'} on the shelf</span>
                            </div>

                            <div className="detail-actions">
                                {borrowing.canBorrow && available && (
                                    <button type="button" className="shelf-borrow" disabled={busy} onClick={() => void borrowing.borrow(book.id)}>
                                        {busy ? 'Borrowing…' : 'Borrow'}
                                    </button>
                                )}
                                <button type="button" className="detail-favourite" aria-pressed={favourite} onClick={() => void toggle(book.id)}>
                                    <Heart size={18} fill={favourite ? 'currentColor' : 'none'} aria-hidden="true" />
                                    {favourite ? 'In your favourites' : 'Add to favourites'}
                                </button>
                            </div>
                            {!available && <p className="detail-note">Every copy is out at the moment. Save it to your favourites and check back.</p>}
                            {available && user?.role === 'User' && !borrowing.canBorrow && <p className="detail-note">You can borrow once an admin has approved your account.</p>}

                            {book.description && (
                                <section className="detail-about" aria-label="About this book">
                                    <h2>About this book</h2>
                                    <p>{book.description}</p>
                                </section>
                            )}

                            <dl className="detail-facts">
                                <div><dt>Author</dt><dd><Link to={authorPath(book.author)}>{book.author}</Link></dd></div>
                                <div><dt>Category</dt><dd><Link to={`/search?genre=${encodeURIComponent(book.genre)}`}>{book.genre}</Link></dd></div>
                                <div><dt>ISBN</dt><dd>{book.isbn}</dd></div>
                                <div><dt>Copies</dt><dd>{book.totalCopies}</dd></div>
                                <div><dt>In the catalogue since</dt><dd>{new Date(book.createdAt).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' })}</dd></div>
                            </dl>
                        </div>
                    </article>

                    {/* Shown only when the author has other books here. */}
                    <BookShelf title={`More books by ${book.author}`} books={moreByAuthor}
                        action={{ label: 'View all', to: authorPath(book.author) }}
                        borrow={{ canBorrow: borrowing.canBorrow, pending: borrowing.pending, onBorrow: id => void borrowing.borrow(id) }} />
                </>}
            </main>
        </div>
    );
};

export default BookPage;
