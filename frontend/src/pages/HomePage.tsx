import { useAuth } from '../contexts/AuthContext';
import { useCallback, useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import {
    RefreshCw,
    Search,
} from 'lucide-react';
import { useLocation } from 'react-router-dom';
import { INVENTORY_API_BASE_URL } from '../config/api';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import PosterSlider from '../components/PosterSlider/PosterSlider';
import type { Poster } from '../components/PosterSlider/PosterSlider';
import BorrowToast from '../components/BorrowToast';
import FavouriteButton from '../components/Favourites/FavouriteButton';
import { useBorrow } from '../hooks/useBorrow';
import CatalogueSections from '../components/CatalogueSections/CatalogueSections';

interface Book {
    id: number;
    title: string;
    author: string;
    isbn: string;
    genre: string;
    coverImageUrl?: string | null;
    totalCopies: number;
    availableCopies: number;
    isAvailable?: boolean;
    availabilityStatus?: string;
}

const mockBooks: Book[] = [
    {
        id: -1,
        title: 'The Psychology of Money',
        author: 'Morgan Housel',
        isbn: '9780857197689',
        genre: 'Money',
        coverImageUrl: 'https://covers.openlibrary.org/b/isbn/9780857197689-L.jpg',
        totalCopies: 4,
        availableCopies: 3,
        isAvailable: true,
        availabilityStatus: 'Available',
    },
    {
        id: -2,
        title: 'Company of One',
        author: 'Paul Jarvis',
        isbn: '9781328972354',
        genre: 'Business',
        coverImageUrl: 'https://covers.openlibrary.org/b/isbn/9781328972354-L.jpg',
        totalCopies: 2,
        availableCopies: 1,
        isAvailable: true,
        availabilityStatus: 'Available',
    },
    {
        id: -3,
        title: 'How Innovation Works',
        author: 'Matt Ridley',
        isbn: '9780062916594',
        genre: 'Business',
        coverImageUrl: 'https://covers.openlibrary.org/b/isbn/9780062916594-L.jpg',
        totalCopies: 5,
        availableCopies: 2,
        isAvailable: true,
        availabilityStatus: 'Available',
    },
    {
        id: -4,
        title: 'The Picture of Dorian Gray',
        author: 'Oscar Wilde',
        isbn: '9780141439570',
        genre: 'Fiction',
        coverImageUrl: 'https://covers.openlibrary.org/b/isbn/9780141439570-L.jpg',
        totalCopies: 3,
        availableCopies: 1,
        isAvailable: true,
        availabilityStatus: 'Available',
    },
    {
        id: -5,
        title: 'The Two Towers',
        author: 'J. R. R. Tolkien',
        isbn: '9780261103580',
        genre: 'Fantasy',
        coverImageUrl: 'https://covers.openlibrary.org/b/isbn/9780261103580-L.jpg',
        totalCopies: 2,
        availableCopies: 1,
        isAvailable: true,
        availabilityStatus: 'Available',
    },
];

const HomePage = () => {
    const location = useLocation();
    useEffect(() => {
        const target = location.hash.slice(1);
        if (target === 'recommendations' || target === 'discover') {
            document.getElementById(target)?.scrollIntoView({ block: 'start' });
        }
    }, [location.hash, location.key]);
    const { user } = useAuth();
    const [books, setBooks] = useState<Book[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState('');
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedAuthor, setSelectedAuthor] = useState('All Authors');
    const [selectedStatus, setSelectedStatus] = useState('All Statuses');
    const [failedCoverIds, setFailedCoverIds] = useState<Set<number>>(new Set());

    const fetchBooks = useCallback(async (background = false) => {
        if (!background) setIsLoading(true);
        try {
            const response = await axios.get<Book[]>(`${INVENTORY_API_BASE_URL}/api/books`);
            setBooks(response.data);
            setErrorMessage('');
        } catch (err) {
            console.error('Failed to fetch catalogue:', err);
            setErrorMessage('Unable to load the book catalogue. Please ensure the Inventory Service is running.');
        } finally {
            if (!background) setIsLoading(false);
        }
    }, []);

    const borrowing = useBorrow(() => void fetchBooks());

    useEffect(() => {
        void fetchBooks();
        const refresh = () => { if (!document.hidden) void fetchBooks(true); };
        const timer = window.setInterval(refresh, 10000);
        window.addEventListener('focus', refresh);
        return () => { window.clearInterval(timer); window.removeEventListener('focus', refresh); };
    }, [fetchBooks]);

    const displayBooks = useMemo(() => {
        const titles = new Set(books.map(book => book.title.trim().toLowerCase()));
        const fillers = mockBooks.filter(book => !titles.has(book.title.toLowerCase()));
        return books.length >= 5 ? books : [...books, ...fillers].slice(0, 8);
    }, [books]);

    const authors = useMemo(() => {
        const uniqueAuthors = Array.from(new Set(displayBooks.map(book => book.author))).filter(Boolean);
        return ['All Authors', ...uniqueAuthors.sort((first, second) => first.localeCompare(second))];
    }, [displayBooks]);

    const filteredBooks = useMemo(() => {
        const query = searchQuery.trim().toLowerCase();

        return displayBooks.filter(book => {
            const matchesAuthor = selectedAuthor === 'All Authors' || book.author === selectedAuthor;
            const isAvailable = book.isAvailable ?? book.availableCopies > 0;
            const matchesStatus = selectedStatus === 'All Statuses' ||
                (selectedStatus === 'Available' ? isAvailable : !isAvailable);
            const matchesQuery = !query ||
                book.title.toLowerCase().includes(query) ||
                book.author.toLowerCase().includes(query) ||
                book.isbn.toLowerCase().includes(query) ||
                book.genre.toLowerCase().includes(query);

            return matchesAuthor && matchesStatus && matchesQuery;
        });
    }, [displayBooks, searchQuery, selectedAuthor, selectedStatus]);

    const recommendationBooks = filteredBooks;
    const availableCount = books.filter(book => (book.isAvailable ?? book.availableCopies > 0)).length;
    const emptyMessage = books.length === 0 ? 'No books have been added yet.' : 'No books match your search.';

    const markCoverFailed = (bookId: number) => {
        setFailedCoverIds(prev => new Set(prev).add(bookId));
    };

    // Picking an author on a shelf filters the catalogue to their books.
    const showAuthor = (name: string) => {
        setSelectedAuthor(authors.find(author => author.trim().toLowerCase() === name.trim().toLowerCase()) ?? name);
        document.getElementById('recommendations')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };

    const showSearchResults = () => {
        document.getElementById('recommendations')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };

    const resetFilters = () => {
        setSearchQuery('');
        setSelectedAuthor('All Authors');
        setSelectedStatus('All Statuses');
    };

    const renderCover = (book: Book, size: 'large' | 'small') => {
        const hasCover = book.coverImageUrl && !failedCoverIds.has(book.id);

        if (hasCover) {
            return (
                <img
                    src={book.coverImageUrl ?? ''}
                    alt={`${book.title} cover`}
                    loading="lazy"
                    onError={() => markCoverFailed(book.id)}
                    className={`discover-cover discover-cover-${size}`}
                />
            );
        }

        return (
            <div className={`discover-cover discover-cover-${size} discover-cover-fallback`}>
                <span>{book.title}</span>
            </div>
        );
    };

    const posters: Poster[] = [
        { id: 'new', theme: 'forest', eyebrow: 'Fresh on the shelves', title: 'New arrivals', text: 'See the latest books added to the catalogue and reserve yours before they go.', action: { label: 'Browse books', to: '#recommendations' } },
        ...(user?.role === 'Admin' ? [
            { id: 'manage', theme: 'clay' as const, eyebrow: 'For librarians', title: 'Keep the catalogue up to date', text: 'Add new titles, adjust copies and review what is out on loan.', action: { label: 'Manage books', to: '/admin/books' } },
        ] : [
            { id: 'loans', theme: 'clay' as const, eyebrow: 'Your reading journey', title: 'Know when your books are due', text: 'Check due dates, pending reservations and any fines in one place.', action: { label: 'My borrowings', to: '/my-borrowings' } },
            { id: 'updates', theme: 'sand' as const, eyebrow: 'Stay up to date', title: 'Never miss a pickup', text: 'We let you know when a reservation is ready or a return is coming up.', action: { label: 'View notifications', to: '/notifications' } },
        ]),
    ];

    return (
        <div className="app-shell">
            <AppTopbar />

            <LibrarySidenav active="discover" />

            <BorrowToast success={borrowing.success} error={borrowing.error} onDismiss={borrowing.dismiss} />

            <main id="discover" className="app-main discover-main">
                <PosterSlider posters={posters} label="Library highlights" />
                <CatalogueSections liveBooks={books} onSelectAuthor={showAuthor}
                    borrow={{ canBorrow: borrowing.canBorrow, pending: borrowing.pending, onBorrow: bookId => void borrowing.borrow(bookId) }} />
                <section className="discover-hero">
                    <div className="discover-user-strip">
                        <button type="button" className="discover-icon-button" title="Refresh books" aria-label="Refresh books" onClick={() => void fetchBooks()} disabled={isLoading}>
                            <RefreshCw size={17} />
                        </button>
                    </div>

                    <div className="discover-copy">
                        <p className="discover-eyebrow">Available Books</p>
                        <h1>Discover</h1>
                    </div>

                    <div className="discover-search-panel">
                        <select
                            value={selectedAuthor}
                            onChange={e => setSelectedAuthor(e.target.value)}
                            className="discover-filter-select"
                            aria-label="Book author"
                        >
                            {authors.map(author => (
                                <option key={author} value={author}>{author}</option>
                            ))}
                        </select>
                        <select
                            value={selectedStatus}
                            onChange={e => setSelectedStatus(e.target.value)}
                            className="discover-filter-select"
                            aria-label="Book availability status"
                        >
                            <option value="All Statuses">All Statuses</option>
                            <option value="Available">Available</option>
                            <option value="Unavailable">Unavailable</option>
                        </select>
                        <div className="discover-search-input">
                            <Search size={16} />
                            <input
                                type="text"
                                value={searchQuery}
                                onChange={e => setSearchQuery(e.target.value)}
                                onKeyDown={e => {
                                    if (e.key === 'Enter') showSearchResults();
                                }}
                                placeholder="Find the book you like..."
                            />
                        </div>
                        <button type="button" className="discover-search-button" onClick={showSearchResults}>Search</button>
                    </div>

                    <div className="discover-filter-summary">
                        <span>{filteredBooks.length} matching books</span>
                        {(searchQuery || selectedAuthor !== 'All Authors' || selectedStatus !== 'All Statuses') && (
                            <button type="button" className="discover-clear-button" onClick={resetFilters}>Clear filters</button>
                        )}
                    </div>

                    {!user?.isValidated && (
                        <div className="discover-warning">
                            <strong>Account Pending Approval:</strong> Your registration must be validated before reserving physical books.
                        </div>
                    )}

                    <div className="discover-stats">
                        <span>{books.length} real books</span>
                        <span>{availableCount} available now</span>
                    </div>
                </section>

                <section id="recommendations" className="discover-section">
                    <div className="discover-section-header">
                        <h2>Book Recommendation</h2>
                        <button type="button" className="discover-view-button" onClick={showSearchResults}>
                            View all
                        </button>
                    </div>

                    {errorMessage && <div className="discover-error">{errorMessage}</div>}
                    {isLoading && (
                        <div className="discover-empty">Loading books...</div>
                    )}

                    {!isLoading && recommendationBooks.length === 0 && (
                        <div className="discover-empty">{emptyMessage}</div>
                    )}

                    {!isLoading && recommendationBooks.length > 0 && (
                        <div className="discover-book-row">
                            {recommendationBooks.map(book => {
                                const isAvailable = book.isAvailable ?? book.availableCopies > 0;
                                const statusText = book.availabilityStatus || (isAvailable ? 'Available' : 'Not available now');

                                return (
                                    <article key={book.id} className="discover-featured-book">
                                        <div className="favourite-anchor">
                                            {renderCover(book, 'large')}
                                            <FavouriteButton bookId={book.id} title={book.title} />
                                        </div>
                                        <div className="discover-book-meta">
                                            <h3>{book.title}</h3>
                                            <p>{book.author}</p>
                                            <div className="discover-book-stock">
                                                <span>{book.availableCopies} of {book.totalCopies} copies</span>
                                                <span className={isAvailable ? 'discover-status-available' : 'discover-status-unavailable'}>
                                                    {statusText}
                                                </span>
                                            </div>
                                            {borrowing.canBorrow && isAvailable && (
                                                <button type="button" onClick={() => void borrowing.borrow(book.id)} disabled={borrowing.pending[book.id]}
                                                    className="shelf-borrow discover-reserve-button" aria-label={`Borrow ${book.title}`}>
                                                    {borrowing.pending[book.id] ? 'Borrowing…' : 'Borrow'}
                                                </button>
                                            )}
                                        </div>
                                    </article>
                                );
                            })}
                        </div>
                    )}
                </section>

            </main>
        </div>
    );
};

export default HomePage;
