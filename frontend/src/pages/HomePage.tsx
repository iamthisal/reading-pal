import { useAuth } from '../contexts/AuthContext';
import { useCallback, useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import {
    Bell,
    BookMarked,
    BookOpen,
    ChevronDown,
    Check,
    Filter,
    Heart,
    LayoutDashboard,
    Library,
    LogOut,
    RefreshCw,
    Search,
    User,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import { INVENTORY_API_BASE_URL, LENDING_API_BASE_URL } from '../config/api';

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
    const { logout, user, token } = useAuth();
    const [books, setBooks] = useState<Book[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState('');
    const [reservationSuccess, setReservationSuccess] = useState('');
    const [reservationError, setReservationError] = useState('');
    const [isReserving, setIsReserving] = useState<Record<number, boolean>>({});
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedGenres, setSelectedGenres] = useState<string[]>([]);
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

    const handleReserve = async (bookId: number) => {
        if (!user || !token) return;
        setIsReserving(prev => ({ ...prev, [bookId]: true }));
        setReservationError('');
        setReservationSuccess('');

        try {
            await axios.post(`${LENDING_API_BASE_URL}/api/Reservations`, { bookId }, {
                headers: {
                    Authorization: `Bearer ${token}`
                }
            });
            setReservationSuccess('Book reserved successfully! It is now pending admin approval.');
            // Refresh catalogue to reflect any changes if needed
            fetchBooks();
        } catch (err: any) {
            console.error('Failed to reserve book:', err);
            setReservationError(
                err.response?.data?.message ||
                err.response?.data?.title ||
                (typeof err.response?.data === 'string' ? err.response.data : 'Failed to reserve book. Please try again.')
            );
        } finally {
            setIsReserving(prev => ({ ...prev, [bookId]: false }));
        }
    };

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

    const genreOptions = useMemo(() => {
        const counts = new Map<string, number>();
        displayBooks.forEach(book => {
            if (book.genre) counts.set(book.genre, (counts.get(book.genre) || 0) + 1);
        });
        return Array.from(counts.entries())
            .map(([name, count]) => ({ name, count }))
            .sort((first, second) => first.name.localeCompare(second.name));
    }, [displayBooks]);

    const authors = useMemo(() => {
        const uniqueAuthors = Array.from(new Set(displayBooks.map(book => book.author))).filter(Boolean);
        return ['All Authors', ...uniqueAuthors.sort((first, second) => first.localeCompare(second))];
    }, [displayBooks]);

    const filteredBooks = useMemo(() => {
        const query = searchQuery.trim().toLowerCase();

        return displayBooks.filter(book => {
            const matchesGenre = selectedGenres.length === 0 || selectedGenres.includes(book.genre);
            const matchesAuthor = selectedAuthor === 'All Authors' || book.author === selectedAuthor;
            const isAvailable = book.isAvailable ?? book.availableCopies > 0;
            const matchesStatus = selectedStatus === 'All Statuses' ||
                (selectedStatus === 'Available' ? isAvailable : !isAvailable);
            const matchesQuery = !query ||
                book.title.toLowerCase().includes(query) ||
                book.author.toLowerCase().includes(query) ||
                book.isbn.toLowerCase().includes(query) ||
                book.genre.toLowerCase().includes(query);

            return matchesGenre && matchesAuthor && matchesStatus && matchesQuery;
        });
    }, [displayBooks, searchQuery, selectedGenres, selectedAuthor, selectedStatus]);

    const recommendationBooks = filteredBooks;
    const availableCount = books.filter(book => (book.isAvailable ?? book.availableCopies > 0)).length;
    const emptyMessage = books.length === 0 ? 'No books have been added yet.' : 'No books match your search.';

    const markCoverFailed = (bookId: number) => {
        setFailedCoverIds(prev => new Set(prev).add(bookId));
    };

    const showSearchResults = () => {
        document.getElementById('recommendations')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };

    const resetFilters = () => {
        setSearchQuery('');
        setSelectedGenres([]);
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

    return (
        <div className="discover-shell">
            <aside className="discover-sidebar">
                <div className="discover-brand">
                    <BookMarked size={24} />
                    <span>Reading Pal</span>
                </div>

                <nav className="discover-nav" aria-label="Library navigation">
                    <span className="discover-nav-label">Menu</span>
                    <a className="discover-nav-item discover-nav-item-active" href="#discover">
                        <BookOpen size={16} />
                        Discover
                    </a>
                    <a className="discover-nav-item" href="#recommendations">
                        <Library size={16} />
                        My Library
                    </a>
                    <a className="discover-nav-item" href="#recommendations">
                        <Heart size={16} />
                        Favorite
                    </a>
                </nav>

                <aside className="discover-filter-rail" aria-label="Book filters">
                    <div className="discover-filter-heading">
                        <Filter size={16} />
                        <span>Filters</span>
                        {(selectedGenres.length > 0 || selectedStatus === 'Available') && (
                            <button type="button" className="discover-filter-reset" onClick={resetFilters}>Clear</button>
                        )}
                    </div>

                    <fieldset className="discover-filter-group">
                        <legend>Stock status</legend>
                        <label className="discover-check-row">
                            <input
                                type="checkbox"
                                checked={selectedStatus === 'Available'}
                                onChange={e => setSelectedStatus(e.target.checked ? 'Available' : 'All Statuses')}
                            />
                            <span className="discover-custom-checkbox"><Check size={12} /></span>
                            <span>Available</span>
                            <small>{availableCount}</small>
                        </label>
                    </fieldset>

                    <fieldset className="discover-filter-group">
                        <legend>Categories</legend>
                        {genreOptions.map(option => (
                            <label className="discover-check-row" key={option.name}>
                                <input
                                    type="checkbox"
                                    checked={selectedGenres.includes(option.name)}
                                    onChange={e => setSelectedGenres(current => e.target.checked
                                        ? [...current, option.name]
                                        : current.filter(genre => genre !== option.name))}
                                />
                                <span className="discover-custom-checkbox"><Check size={12} /></span>
                                <span>{option.name}</span>
                                <small>{option.count}</small>
                            </label>
                        ))}
                        {genreOptions.length === 0 && <p className="discover-filter-empty">No categories yet</p>}
                    </fieldset>
                </aside>

                <div className="discover-sidebar-bottom">
                    {user?.role === 'Admin' ? (
                        <Link to="/admin/dashboard" className="discover-nav-item">
                            <LayoutDashboard size={16} />
                            Dashboard
                        </Link>
                    ) : (
                        <Link to="/profile" className="discover-nav-item">
                            <User size={16} />
                            My Profile
                        </Link>
                    )}
                    <button type="button" onClick={logout} className="discover-nav-item discover-nav-button">
                        <LogOut size={16} />
                        Log out
                    </button>
                </div>
            </aside>

            <main id="discover" className="discover-main">
                <section className="discover-hero">
                    <div className="discover-user-strip">
                        <div className="discover-user-chip">
                            <span className="discover-avatar">{user?.email?.slice(0, 1).toUpperCase() || 'R'}</span>
                            <span>{user?.email || 'Reader'}</span>
                            <ChevronDown size={14} />
                        </div>
                        <button type="button" className="discover-icon-button" title="Refresh books" onClick={() => void fetchBooks()} disabled={isLoading}>
                            <RefreshCw size={17} />
                        </button>
                        <button type="button" className="discover-icon-button" title="Notifications">
                            <Bell size={17} />
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
                        {(searchQuery || selectedGenres.length > 0 || selectedAuthor !== 'All Authors' || selectedStatus !== 'All Statuses') && (
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
                    {reservationError && <div className="discover-error" style={{ marginTop: '10px' }}>{reservationError}</div>}
                    {reservationSuccess && (
                        <div style={{ marginTop: '10px', padding: '12px', background: '#e6f4ea', color: '#137333', borderRadius: '8px', fontSize: '0.875rem', border: '1px solid #ceead6' }}>
                            {reservationSuccess}
                        </div>
                    )}

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
                                        {renderCover(book, 'large')}
                                        <div className="discover-book-meta">
                                            <h3>{book.title}</h3>
                                            <p>{book.author}</p>
                                            <span>{book.availableCopies} of {book.totalCopies} copies</span>
                                            <span className={isAvailable ? 'discover-status-available' : 'discover-status-unavailable'}>
                                                {statusText}
                                            </span>
                                            {user?.isValidated && isAvailable && (
                                                <button
                                                    onClick={() => handleReserve(book.id)}
                                                    disabled={isReserving[book.id]}
                                                    style={{
                                                        marginTop: '12px',
                                                        padding: '8px 12px',
                                                        background: 'var(--primary-color, #2563eb)',
                                                        color: 'white',
                                                        border: 'none',
                                                        borderRadius: '6px',
                                                        cursor: isReserving[book.id] ? 'not-allowed' : 'pointer',
                                                        fontSize: '0.875rem',
                                                        fontWeight: '500',
                                                        opacity: isReserving[book.id] ? 0.7 : 1,
                                                        width: '100%'
                                                    }}
                                                >
                                                    {isReserving[book.id] ? 'Reserving...' : 'Reserve Book'}
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
