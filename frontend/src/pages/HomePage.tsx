import { useAuth } from '../contexts/AuthContext';
import { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import { LogOut, BookMarked, User, Search, RefreshCw, Library, LayoutDashboard } from 'lucide-react';
import { Link } from 'react-router-dom';
import { INVENTORY_API_BASE_URL } from '../config/api';

interface Book {
    id: number;
    title: string;
    author: string;
    isbn: string;
    genre: string;
    totalCopies: number;
    availableCopies: number;
    isAvailable?: boolean;
    availabilityStatus?: string;
}

const HomePage = () => {
    const { logout, user } = useAuth();
    const [books, setBooks] = useState<Book[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState('');
    const [searchQuery, setSearchQuery] = useState('');

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

    const filteredBooks = useMemo(() => {
        const query = searchQuery.trim().toLowerCase();
        if (!query) return books;

        return books.filter(book =>
            book.title.toLowerCase().includes(query) ||
            book.author.toLowerCase().includes(query) ||
            book.isbn.toLowerCase().includes(query) ||
            book.genre.toLowerCase().includes(query)
        );
    }, [books, searchQuery]);

    const availableCount = books.filter(book => (book.isAvailable ?? book.availableCopies > 0)).length;
    const emptyMessage = books.length === 0 ? 'No books have been added to the catalogue yet.' : 'No books match your search.';

    return (
        <div className="page-container">
            <header className="dashboard-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    <BookMarked size={32} color="var(--accent-color)" />
                    <h1 style={{ fontSize: '1.5rem', fontWeight: 600 }}>Reading Pal</h1>
                </div>
                <div style={{ display: 'flex', gap: '1rem' }}>
                    {user?.role === 'Admin' ? (
                        <Link to="/admin/dashboard" className="btn-outline" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', textDecoration: 'none' }}>
                            <LayoutDashboard size={16} />
                            Dashboard
                        </Link>
                    ) : (
                        <Link to="/profile" className="btn-outline" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', textDecoration: 'none' }}>
                            <User size={16} />
                            My Profile
                        </Link>
                    )}
                    <button onClick={logout} className="btn-outline" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <LogOut size={16} />
                        Sign Out
                    </button>
                </div>
            </header>
            
            <main>
                <section style={{ marginBottom: '1.5rem' }}>
                    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '1rem', flexWrap: 'wrap' }}>
                        <div>
                            <h2 style={{ marginBottom: '0.5rem' }}>Library Catalogue</h2>
                            <p style={{ color: 'var(--text-secondary)', maxWidth: '680px' }}>
                                Browse available books and check how many copies are ready to borrow.
                            </p>
                        </div>
                        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                            <span style={{ padding: '0.55rem 0.75rem', borderRadius: '6px', backgroundColor: 'rgba(59, 130, 246, 0.15)', color: '#93c5fd', fontWeight: 600 }}>
                                {books.length} Books
                            </span>
                            <span style={{ padding: '0.55rem 0.75rem', borderRadius: '6px', backgroundColor: 'rgba(16, 185, 129, 0.15)', color: '#6ee7b7', fontWeight: 600 }}>
                                {availableCount} Available
                            </span>
                        </div>
                    </div>

                    {!user?.isValidated && (
                        <div className="error-message" style={{ background: 'rgba(234, 179, 8, 0.1)', borderColor: '#eab308', color: '#fde047', marginTop: '1.5rem', marginBottom: 0 }}>
                            <strong>Account Pending Approval:</strong> Your registration has been received but must be validated by an administrator before you can reserve physical books.
                        </div>
                    )}
                </section>

                <section className="glass-panel" style={{ padding: '1.5rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '1rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                            <Library size={20} color="var(--accent-color)" />
                            <h3 style={{ fontSize: '1.15rem' }}>Books</h3>
                        </div>
                        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap' }}>
                            <div style={{ position: 'relative', width: 'min(320px, 80vw)' }}>
                                <Search size={16} color="var(--text-secondary)" style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)' }} />
                                <input
                                    type="text"
                                    className="form-input"
                                    style={{ paddingLeft: '2.25rem', fontSize: '0.9rem' }}
                                    placeholder="Search title, author, genre..."
                                    value={searchQuery}
                                    onChange={e => setSearchQuery(e.target.value)}
                                />
                            </div>
                            <button type="button" onClick={fetchBooks} className="btn-outline" title="Refresh catalogue" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.65rem' }}>
                                <RefreshCw size={16} />
                            </button>
                        </div>
                    </div>

                    {errorMessage && <div className="error-message">{errorMessage}</div>}

                    {isLoading && (
                        <p style={{ color: 'var(--text-secondary)', textAlign: 'center', padding: '2rem' }}>Loading books...</p>
                    )}

                    {!isLoading && filteredBooks.length === 0 && (
                        <p style={{ color: 'var(--text-secondary)', textAlign: 'center', padding: '2rem' }}>{emptyMessage}</p>
                    )}

                    {!isLoading && filteredBooks.length > 0 && (
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1rem' }}>
                            {filteredBooks.map(book => {
                                const isAvailable = book.isAvailable ?? book.availableCopies > 0;
                                const statusText = book.availabilityStatus || (isAvailable ? 'Available' : 'Not available now');

                                return (
                                    <article key={book.id} style={{ minHeight: '210px', display: 'flex', flexDirection: 'column', justifyContent: 'space-between', padding: '1rem', borderRadius: '8px', background: 'rgba(15, 23, 42, 0.52)', border: '1px solid rgba(255, 255, 255, 0.1)' }}>
                                        <div>
                                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '0.75rem', marginBottom: '0.75rem' }}>
                                                <h4 style={{ fontSize: '1.05rem', lineHeight: 1.35 }}>{book.title}</h4>
                                                <span style={{ flexShrink: 0, padding: '0.25rem 0.5rem', borderRadius: '4px', backgroundColor: 'rgba(59, 130, 246, 0.15)', color: '#93c5fd', fontSize: '0.78rem', fontWeight: 600 }}>
                                                    {book.genre}
                                                </span>
                                            </div>
                                            <p style={{ color: 'var(--text-secondary)', marginBottom: '0.75rem' }}>by {book.author}</p>
                                            <p style={{ color: 'var(--text-secondary)', fontFamily: 'monospace', fontSize: '0.82rem' }}>ISBN {book.isbn}</p>
                                        </div>

                                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '0.75rem', marginTop: '1.25rem', flexWrap: 'wrap' }}>
                                            <span style={{ color: 'var(--text-secondary)', fontSize: '0.9rem' }}>
                                                <strong style={{ color: 'var(--text-primary)' }}>{book.availableCopies}</strong> of {book.totalCopies} copies
                                            </span>
                                            <span style={{ padding: '0.45rem 0.7rem', borderRadius: '6px', backgroundColor: isAvailable ? 'rgba(16, 185, 129, 0.16)' : 'rgba(239, 68, 68, 0.18)', border: isAvailable ? '1px solid rgba(16, 185, 129, 0.35)' : '1px solid rgba(239, 68, 68, 0.42)', color: isAvailable ? '#6ee7b7' : '#fca5a5', fontWeight: 700, fontSize: '0.85rem' }}>
                                                {statusText}
                                            </span>
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
