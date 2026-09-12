import { useAuth } from '../contexts/AuthContext';
import { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import {
    Bell,
    BookMarked,
    BookOpen,
    ChevronDown,
    Grid3X3,
    Heart,
    LayoutDashboard,
    Library,
    LogOut,
    RefreshCw,
    Search,
    User,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import { INVENTORY_API_BASE_URL } from '../config/api';

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
    const { logout, user } = useAuth();
    const [books, setBooks] = useState<Book[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState('');
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedGenre, setSelectedGenre] = useState('All Categories');
    const [failedCoverIds, setFailedCoverIds] = useState<Set<number>>(new Set());

    const fetchBooks = async () => {
        setIsLoading(true);
        try {
            const response = await axios.get<Book[]>(`${INVENTORY_API_BASE_URL}/api/books`);
            setBooks(response.data);
            setErrorMessage('');
        } catch (err) {
            console.error('Failed to fetch catalogue:', err);
            setErrorMessage('Unable to load the book catalogue. Please ensure the Inventory Service is running.');
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        fetchBooks();
    }, []);

    const displayBooks = useMemo(() => {
        const titles = new Set(books.map(book => book.title.trim().toLowerCase()));
        const fillers = mockBooks.filter(book => !titles.has(book.title.toLowerCase()));
        return books.length >= 5 ? books : [...books, ...fillers].slice(0, 8);
    }, [books]);

    const genres = useMemo(() => {
        const uniqueGenres = Array.from(new Set(displayBooks.map(book => book.genre))).filter(Boolean);
        return ['All Categories', ...uniqueGenres];
    }, [displayBooks]);

    const filteredBooks = useMemo(() => {
        const query = searchQuery.trim().toLowerCase();

        return displayBooks.filter(book => {
            const matchesGenre = selectedGenre === 'All Categories' || book.genre === selectedGenre;
            const matchesQuery = !query ||
                book.title.toLowerCase().includes(query) ||
                book.author.toLowerCase().includes(query) ||
                book.isbn.toLowerCase().includes(query) ||
                book.genre.toLowerCase().includes(query);

            return matchesGenre && matchesQuery;
        });
    }, [displayBooks, searchQuery, selectedGenre]);

    const availableBooks = filteredBooks.filter(book => book.isAvailable ?? book.availableCopies > 0);
    const unavailableBooks = filteredBooks.filter(book => !(book.isAvailable ?? book.availableCopies > 0));
    const recommendationBooks = availableBooks.slice(0, 5);
    const categoryBooks = [...availableBooks.slice(5), ...unavailableBooks].slice(0, 8);
    const availableCount = books.filter(book => (book.isAvailable ?? book.availableCopies > 0)).length;
    const emptyMessage = books.length === 0 ? 'No books have been added yet.' : 'No books match your search.';

    const markCoverFailed = (bookId: number) => {
        setFailedCoverIds(prev => new Set(prev).add(bookId));
    };

    const showSearchResults = () => {
        document.getElementById('recommendations')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
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
                    <a className="discover-nav-item" href="#categories">
                        <Grid3X3 size={16} />
                        Category
                    </a>
                    <a className="discover-nav-item" href="#recommendations">
                        <Library size={16} />
                        My Library
                    </a>
                    <a className="discover-nav-item" href="#categories">
                        <Heart size={16} />
                        Favorite
                    </a>
                </nav>

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
                        <button type="button" className="discover-icon-button" title="Refresh books" onClick={fetchBooks} disabled={isLoading}>
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
                            value={selectedGenre}
                            onChange={e => setSelectedGenre(e.target.value)}
                            className="discover-category-select"
                            aria-label="Book category"
                        >
                            {genres.map(genre => (
                                <option key={genre} value={genre}>{genre}</option>
                            ))}
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
                            {recommendationBooks.map(book => (
                                <article key={book.id} className="discover-featured-book">
                                    {renderCover(book, 'large')}
                                    <div className="discover-book-meta">
                                        <h3>{book.title}</h3>
                                        <p>{book.author}</p>
                                        <span>{book.availableCopies} of {book.totalCopies} copies</span>
                                    </div>
                                </article>
                            ))}
                        </div>
                    )}
                </section>

                <section id="categories" className="discover-section">
                    <div className="discover-section-header">
                        <h2>Book Category</h2>
                        <button type="button" className="discover-icon-button" title="Refresh categories" onClick={fetchBooks} disabled={isLoading}>
                            <Grid3X3 size={17} />
                        </button>
                    </div>

                    <div className="discover-category-grid">
                        {(categoryBooks.length > 0 ? categoryBooks : recommendationBooks).map(book => {
                            const isAvailable = book.isAvailable ?? book.availableCopies > 0;
                            const statusText = book.availabilityStatus || (isAvailable ? 'Available' : 'Not available now');

                            return (
                                <article key={book.id} className="discover-category-book">
                                    {renderCover(book, 'small')}
                                    <h3>{book.genre}</h3>
                                    <p>{book.title}</p>
                                    <span className={isAvailable ? 'discover-status-available' : 'discover-status-unavailable'}>
                                        {statusText}
                                    </span>
                                </article>
                            );
                        })}
                    </div>
                </section>
            </main>
        </div>
    );
};

export default HomePage;
