import { useEffect, useState, useCallback } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../contexts/AuthContext';
import { BookMarked, BookOpen, BookPlus, CheckCircle, AlertCircle, Search, Layers, RefreshCw, Pencil, X, Trash2, CircleOff, Tags, LayoutDashboard, LogOut, Users } from 'lucide-react';
import { INVENTORY_API_BASE_URL } from '../config/api';

export interface Book {
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
    createdAt: string;
    updatedAt: string;
}

interface Genre {
    id: number;
    name: string;
    createdAt: string;
    updatedAt: string;
}

const AdminBooksPage = () => {
    const { logout, token } = useAuth();

    // Form state
    const [title, setTitle] = useState('');
    const [author, setAuthor] = useState('');
    const [isbn, setIsbn] = useState('');
    const [genre, setGenre] = useState('');
    const [coverImageUrl, setCoverImageUrl] = useState('');
    const [totalCopies, setTotalCopies] = useState('1');

    // UI state
    const [books, setBooks] = useState<Book[]>([]);
    const [genres, setGenres] = useState<Genre[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [errorMessage, setErrorMessage] = useState('');
    const [successMessage, setSuccessMessage] = useState('');
    const [searchQuery, setSearchQuery] = useState('');
    const [isFormOpen, setIsFormOpen] = useState(true);
    const [editingBook, setEditingBook] = useState<Book | null>(null);
    const [editTitle, setEditTitle] = useState('');
    const [editAuthor, setEditAuthor] = useState('');
    const [editIsbn, setEditIsbn] = useState('');
    const [editGenre, setEditGenre] = useState('');
    const [editCoverImageUrl, setEditCoverImageUrl] = useState('');
    const [editTotalCopies, setEditTotalCopies] = useState('1');
    const [isUpdating, setIsUpdating] = useState(false);
    const [editErrorMessage, setEditErrorMessage] = useState('');
    const [markingUnavailableId, setMarkingUnavailableId] = useState<number | null>(null);
    const [markingAvailableId, setMarkingAvailableId] = useState<number | null>(null);
    const [isGenreManagerOpen, setIsGenreManagerOpen] = useState(false);
    const [newGenreName, setNewGenreName] = useState('');
    const [editingGenreId, setEditingGenreId] = useState<number | null>(null);
    const [editingGenreName, setEditingGenreName] = useState('');
    const [genreErrorMessage, setGenreErrorMessage] = useState('');
    const [isGenreSubmitting, setIsGenreSubmitting] = useState(false);

    const fetchBooks = useCallback(async () => {
        setIsLoading(true);
        try {
            const response = await axios.get<Book[]>(`${INVENTORY_API_BASE_URL}/api/books`, {
                headers: token ? { Authorization: `Bearer ${token}` } : {}
            });
            setBooks(response.data);
            setErrorMessage('');
        } catch (err) {
            console.error('Failed to fetch books:', err);
            setErrorMessage('Unable to connect to the Inventory Service. Please ensure it is running on port 5001.');
        } finally {
            setIsLoading(false);
        }
    }, [token]);

    useEffect(() => {
        let isMounted = true;
        const load = async () => {
            try {
                const [booksResponse, genresResponse] = await Promise.all([
                    axios.get<Book[]>(`${INVENTORY_API_BASE_URL}/api/books`, { headers: token ? { Authorization: `Bearer ${token}` } : {} }),
                    axios.get<Genre[]>(`${INVENTORY_API_BASE_URL}/api/genres`, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
                ]);
                if (isMounted) {
                    setBooks(booksResponse.data);
                    setGenres(genresResponse.data);
                    setErrorMessage('');
                }
            } catch (err) {
                console.error('Failed to fetch books:', err);
                if (isMounted) {
                    setErrorMessage('Unable to connect to the Inventory Service. Please ensure it is running on port 5001.');
                }
            } finally {
                if (isMounted) {
                    setIsLoading(false);
                }
            }
        };

        load();
        return () => {
            isMounted = false;
        };
    }, [token]);

    const handleCreateGenre = async (e: { preventDefault: () => void }) => {
        e.preventDefault();
        const name = newGenreName.trim();
        if (!name) return;

        setIsGenreSubmitting(true);
        setGenreErrorMessage('');
        try {
            const response = await axios.post<Genre>(`${INVENTORY_API_BASE_URL}/api/genres`, { name }, {
                headers: { Authorization: `Bearer ${token}` }
            });
            setGenres(prev => [...prev, response.data].sort((a, b) => a.name.localeCompare(b.name)));
            setNewGenreName('');
            setGenreErrorMessage('');
        } catch (err: unknown) {
            setGenreErrorMessage(axios.isAxiosError(err) ? err.response?.data?.message || 'Failed to add genre.' : 'Failed to add genre.');
        } finally {
            setIsGenreSubmitting(false);
        }
    };

    const handleUpdateGenre = async (genre: Genre) => {
        const name = editingGenreName.trim();
        if (!name) return;

        setIsGenreSubmitting(true);
        setGenreErrorMessage('');
        try {
            const response = await axios.put<Genre>(`${INVENTORY_API_BASE_URL}/api/genres/${genre.id}`, { name }, {
                headers: { Authorization: `Bearer ${token}` }
            });
            setGenres(prev => prev.map(item => item.id === genre.id ? response.data : item).sort((a, b) => a.name.localeCompare(b.name)));
            setBooks(prev => prev.map(book => book.genre.toLowerCase() === genre.name.toLowerCase() ? { ...book, genre: response.data.name } : book));
            setEditingGenreId(null);
            setEditingGenreName('');
        } catch (err: unknown) {
            setGenreErrorMessage(axios.isAxiosError(err) ? err.response?.data?.message || 'Failed to update genre.' : 'Failed to update genre.');
        } finally {
            setIsGenreSubmitting(false);
        }
    };

    const handleDeleteGenre = async (genre: Genre) => {
        if (!window.confirm(`Delete the genre "${genre.name}"?`)) return;

        setIsGenreSubmitting(true);
        setGenreErrorMessage('');
        try {
            await axios.delete(`${INVENTORY_API_BASE_URL}/api/genres/${genre.id}`, { headers: { Authorization: `Bearer ${token}` } });
            setGenres(prev => prev.filter(item => item.id !== genre.id));
        } catch (err: unknown) {
            setGenreErrorMessage(axios.isAxiosError(err) ? err.response?.data?.message || 'Failed to delete genre.' : 'Failed to delete genre.');
        } finally {
            setIsGenreSubmitting(false);
        }
    };

    const handleSubmit = async (e: { preventDefault: () => void }) => {
        e.preventDefault();
        setErrorMessage('');
        setSuccessMessage('');

        const copies = Number.parseInt(totalCopies, 10);
        if (Number.isNaN(copies) || copies < 0) {
            setErrorMessage('Total copies cannot be negative.');
            return;
        }

        if (!title.trim() || !author.trim() || !isbn.trim() || !genre.trim()) {
            setErrorMessage('Please fill in all required fields.');
            return;
        }

        setIsSubmitting(true);

        try {
            const payload = {
                title: title.trim(),
                author: author.trim(),
                isbn: isbn.trim(),
                genre: genre.trim(),
                coverImageUrl: coverImageUrl.trim() || null,
                totalCopies: copies
            };

            const response = await axios.post<Book>(
                `${INVENTORY_API_BASE_URL}/api/books`,
                payload,
                {
                    headers: {
                        Authorization: `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                }
            );

            const createdBook = response.data;
            setSuccessMessage(
                `Successfully added "${createdBook.title}" (ID: ${createdBook.id}, Available Copies: ${createdBook.availableCopies})`
            );

            // Prepend new book to the list
            setBooks(prev => [createdBook, ...prev]);

            // Reset form
            setTitle('');
            setAuthor('');
            setIsbn('');
            setGenre('');
            setCoverImageUrl('');
            setTotalCopies('1');
        } catch (err: unknown) {
            if (axios.isAxiosError(err)) {
                if (err.response?.status === 409) {
                    setErrorMessage(err.response.data?.message || 'A book with this ISBN already exists.');
                } else if (err.response?.status === 403 || err.response?.status === 401) {
                    setErrorMessage('Unauthorized: Only administrators are permitted to add new books.');
                } else if (err.response?.data?.message) {
                    setErrorMessage(err.response.data.message);
                } else {
                    setErrorMessage('Failed to add book. Please verify your inputs and try again.');
                }
            } else {
                setErrorMessage('An unexpected error occurred while adding the book.');
            }
        } finally {
            setIsSubmitting(false);
        }
    };

    const openEditModal = (book: Book) => {
        setEditingBook(book);
        setEditTitle(book.title);
        setEditAuthor(book.author);
        setEditIsbn(book.isbn);
        setEditGenre(book.genre);
        setEditCoverImageUrl(book.coverImageUrl || '');
        setEditTotalCopies(String(book.totalCopies));
        setEditErrorMessage('');
    };

    const closeEditModal = () => {
        if (!isUpdating) {
            setEditingBook(null);
            setEditErrorMessage('');
        }
    };

    const handleUpdateBook = async (e: { preventDefault: () => void }) => {
        e.preventDefault();
        if (!editingBook) return;

        const copies = Number.parseInt(editTotalCopies, 10);
        if (Number.isNaN(copies) || copies < 0) {
            setEditErrorMessage('Total copies cannot be negative.');
            return;
        }

        if (!editTitle.trim() || !editAuthor.trim() || !editIsbn.trim() || !editGenre.trim()) {
            setEditErrorMessage('Please fill in all required fields.');
            return;
        }

        setIsUpdating(true);
        setEditErrorMessage('');
        setErrorMessage('');

        try {
            const response = await axios.put<Book>(
                `${INVENTORY_API_BASE_URL}/api/books/${editingBook.id}`,
                {
                    title: editTitle.trim(),
                    author: editAuthor.trim(),
                    isbn: editIsbn.trim(),
                    genre: editGenre.trim(),
                    coverImageUrl: editCoverImageUrl.trim() || null,
                    totalCopies: copies
                },
                { headers: { Authorization: `Bearer ${token}` } }
            );

            setBooks(prev => prev.map(book => book.id === response.data.id ? response.data : book));
            setEditingBook(null);
            setSuccessMessage(`Successfully updated "${response.data.title}".`);
        } catch (err: unknown) {
            if (axios.isAxiosError(err)) {
                if (err.response?.status === 409) {
                    setEditErrorMessage(err.response.data?.message || 'A book with this ISBN already exists.');
                } else if (err.response?.status === 400) {
                    setEditErrorMessage(err.response.data?.message || 'Please verify the book details and copy count.');
                } else if (err.response?.status === 401 || err.response?.status === 403) {
                    setEditErrorMessage('Unauthorized: Only administrators can update books.');
                } else {
                    setEditErrorMessage('Failed to update book. Please try again.');
                }
            } else {
                setEditErrorMessage('An unexpected error occurred while updating the book.');
            }
        } finally {
            setIsUpdating(false);
        }
    };

    const handleMarkUnavailable = async (book: Book) => {
        if (book.availableCopies === 0) return;
        if (!window.confirm(`Mark "${book.title}" as unavailable now? Available copies will be set to 0.`)) return;

        setErrorMessage('');
        setSuccessMessage('');
        setMarkingUnavailableId(book.id);

        try {
            const response = await axios.patch<Book>(
                `${INVENTORY_API_BASE_URL}/api/books/${book.id}/mark-unavailable`,
                null,
                { headers: { Authorization: `Bearer ${token}` } }
            );

            setBooks(prev => prev.map(item => item.id === response.data.id ? response.data : item));
            setSuccessMessage(`"${response.data.title}" is now marked as unavailable.`);
        } catch (err: unknown) {
            if (axios.isAxiosError(err)) {
                if (err.response?.status === 401 || err.response?.status === 403) {
                    setErrorMessage('Unauthorized: Only administrators can mark books unavailable.');
                } else if (err.response?.data?.message) {
                    setErrorMessage(err.response.data.message);
                } else {
                    setErrorMessage('Failed to mark book unavailable. Please try again.');
                }
            } else {
                setErrorMessage('An unexpected error occurred while marking the book unavailable.');
            }
        } finally {
            setMarkingUnavailableId(null);
        }
    };

    const handleMarkAvailable = async (book: Book) => {
        if (book.availableCopies > 0) return;
        if (!window.confirm(`Make "${book.title}" available again? This will add 1 available copy.`)) return;

        setErrorMessage('');
        setSuccessMessage('');
        setMarkingAvailableId(book.id);

        try {
            const response = await axios.patch<Book>(
                `${INVENTORY_API_BASE_URL}/api/books/${book.id}/mark-available`,
                null,
                { headers: { Authorization: `Bearer ${token}` } }
            );

            setBooks(prev => prev.map(item => item.id === response.data.id ? response.data : item));
            setSuccessMessage(`"${response.data.title}" is available again with ${response.data.availableCopies} copy.`);
        } catch (err: unknown) {
            if (axios.isAxiosError(err)) {
                if (err.response?.status === 401 || err.response?.status === 403) {
                    setErrorMessage('Unauthorized: Only administrators can make books available.');
                } else if (err.response?.data?.message) {
                    setErrorMessage(err.response.data.message);
                } else {
                    setErrorMessage('Failed to make book available. Please try again.');
                }
            } else {
                setErrorMessage('An unexpected error occurred while making the book available.');
            }
        } finally {
            setMarkingAvailableId(null);
        }
    };

    const handleDeleteBook = async (book: Book) => {
        if (!window.confirm(`Remove "${book.title}" from the catalogue? This cannot be undone.`)) return;

        setErrorMessage('');
        setSuccessMessage('');

        try {
            await axios.delete(`${INVENTORY_API_BASE_URL}/api/books/${book.id}`, {
                headers: { Authorization: `Bearer ${token}` }
            });
            setBooks(prev => prev.filter(item => item.id !== book.id));
            setSuccessMessage(`Successfully removed "${book.title}".`);
        } catch (err: unknown) {
            if (axios.isAxiosError(err)) {
                if (err.response?.status === 409) {
                    setErrorMessage(err.response.data?.message || 'This book cannot be removed while copies are borrowed.');
                } else if (err.response?.status === 401 || err.response?.status === 403) {
                    setErrorMessage('Unauthorized: Only administrators can remove books.');
                } else if (err.response?.data?.message) {
                    setErrorMessage(err.response.data.message);
                } else {
                    setErrorMessage('Failed to remove book. Please try again.');
                }
            } else {
                setErrorMessage('An unexpected error occurred while removing the book.');
            }
        }
    };

    const filteredBooks = books.filter(b => {
        const q = searchQuery.toLowerCase();
        return (
            b.title.toLowerCase().includes(q) ||
            b.author.toLowerCase().includes(q) ||
            b.isbn.toLowerCase().includes(q) ||
            b.genre.toLowerCase().includes(q)
        );
    });
    const emptyInventoryMessage = books.length === 0 ? 'No books added to the catalogue yet.' : 'No books match your search query.';

    return (
        <div className="admin-shell">
            <aside className="admin-sidebar">
                <Link to="/admin/dashboard" className="admin-brand">
                    <BookMarked size={23} />
                    <span>Reading Pal</span>
                </Link>

                <nav className="admin-nav" aria-label="Administration navigation">
                    <span className="admin-nav-label">Workspace</span>
                    <Link to="/admin/dashboard" className="admin-nav-item">
                        <LayoutDashboard size={16} />
                        Overview
                    </Link>
                    <Link to="/admin/users/pending" className="admin-nav-item">
                        <Users size={16} />
                        User approvals
                    </Link>
                    <Link to="/admin/books" className="admin-nav-item admin-nav-item-active">
                        <BookPlus size={16} />
                        Book inventory
                    </Link>
                    <Link to="/home" className="admin-nav-item">
                        <BookOpen size={16} />
                        Public catalogue
                    </Link>
                </nav>

                <div className="admin-sidebar-bottom">
                    <button type="button" onClick={logout} className="admin-nav-item admin-nav-button">
                        <LogOut size={16} />
                        Log out
                    </button>
                </div>
            </aside>

            <main className="admin-main admin-inventory-main">
                <header className="admin-topbar admin-inventory-topbar">
                    <div>
                        <p className="admin-eyebrow">Library operations</p>
                        <h1>Book inventory</h1>
                        <p className="admin-inventory-subtitle">Keep the catalogue current, useful, and ready for its next reader.</p>
                    </div>
                    <button
                        onClick={() => setIsFormOpen(prev => !prev)}
                        className="admin-primary-action"
                    >
                        <BookPlus size={17} />
                        {isFormOpen ? 'Hide add form' : 'Add new book'}
                    </button>
                </header>

            {/* Notification Alerts */}
            {successMessage && (
                <div className="admin-inventory-alert admin-inventory-alert-success" style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.75rem',
                    padding: '1rem',
                    marginBottom: '1.5rem',
                    borderRadius: '8px',
                    backgroundColor: 'rgba(16, 185, 129, 0.15)',
                    border: '1px solid rgba(16, 185, 129, 0.3)',
                    color: '#6ee7b7'
                }}>
                    <CheckCircle size={20} color="#10b981" />
                    <span>{successMessage}</span>
                </div>
            )}

            {errorMessage && (
                <div className="admin-inventory-alert admin-inventory-alert-error error-message" style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                    <AlertCircle size={20} color="var(--danger-color)" />
                    <span>{errorMessage}</span>
                </div>
            )}

            {isGenreManagerOpen && (
                <dialog open aria-labelledby="genre-manager-title" style={{ position: 'fixed', inset: 0, zIndex: 10, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem', backgroundColor: 'rgba(0, 0, 0, 0.7)' }}>
                    <div className="glass-panel" style={{ width: '100%', maxWidth: '520px', padding: '2rem', maxHeight: '90vh', overflowY: 'auto' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                            <h2 id="genre-manager-title" style={{ fontSize: '1.25rem' }}>Manage Genres</h2>
                            <button type="button" className="btn-outline" onClick={() => setIsGenreManagerOpen(false)} title="Close genre manager" style={{ padding: '0.5rem' }}>
                                <X size={18} />
                            </button>
                        </div>
                        {genreErrorMessage && <div className="error-message" style={{ marginBottom: '1rem' }}>{genreErrorMessage}</div>}
                        <form onSubmit={handleCreateGenre} style={{ display: 'flex', gap: '0.75rem', marginBottom: '1.5rem' }}>
                            <input id="new-genre-name" type="text" className="form-input" placeholder="e.g. Science Fiction" value={newGenreName} onChange={e => setNewGenreName(e.target.value)} maxLength={100} required />
                            <button type="submit" className="btn-primary" disabled={isGenreSubmitting}>Add</button>
                        </form>
                        <div style={{ display: 'grid', gap: '0.75rem' }}>
                            {genres.map(item => (
                                <div key={item.id} style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.75rem', border: '1px solid var(--border-color)', borderRadius: '6px' }}>
                                    {editingGenreId === item.id ? (
                                        <input className="form-input" value={editingGenreName} onChange={e => setEditingGenreName(e.target.value)} maxLength={100} autoFocus />
                                    ) : (
                                        <span style={{ flex: 1 }}>{item.name}</span>
                                    )}
                                    {editingGenreId === item.id ? (
                                        <>
                                            <button type="button" className="btn-primary" onClick={() => handleUpdateGenre(item)} disabled={isGenreSubmitting}>Save</button>
                                            <button type="button" className="btn-outline" onClick={() => setEditingGenreId(null)} disabled={isGenreSubmitting}>Cancel</button>
                                        </>
                                    ) : (
                                        <>
                                            <button type="button" className="btn-outline" onClick={() => { setEditingGenreId(item.id); setEditingGenreName(item.name); }} title={`Edit ${item.name}`} style={{ padding: '0.5rem' }}><Pencil size={15} /></button>
                                            <button type="button" className="btn-outline" onClick={() => handleDeleteGenre(item)} disabled={isGenreSubmitting} title={`Delete ${item.name}`} style={{ padding: '0.5rem' }}><Trash2 size={15} /></button>
                                        </>
                                    )}
                                </div>
                            ))}
                            {genres.length === 0 && <p style={{ color: 'var(--text-secondary)' }}>No genres have been added yet.</p>}
                        </div>
                    </div>
                </dialog>
            )}

            {editingBook && (
                <dialog
                    open
                    aria-labelledby="edit-book-title"
                    style={{
                        position: 'fixed',
                        inset: 0,
                        zIndex: 10,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        padding: '1rem',
                        backgroundColor: 'rgba(0, 0, 0, 0.7)'
                    }}
                >
                    <div
                        className="glass-panel"
                        style={{ width: '100%', maxWidth: '640px', padding: '2rem', maxHeight: '90vh', overflowY: 'auto' }}
                    >
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                            <h2 id="edit-book-title" style={{ fontSize: '1.25rem' }}>Edit Book</h2>
                            <button type="button" className="btn-outline" onClick={closeEditModal} disabled={isUpdating} title="Close edit dialog" style={{ padding: '0.5rem' }}>
                                <X size={18} />
                            </button>
                        </div>

                        {editErrorMessage && <div className="error-message" style={{ marginBottom: '1rem' }}>{editErrorMessage}</div>}

                        <form onSubmit={handleUpdateBook}>
                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1.25rem' }}>
                                <div className="form-group" style={{ marginBottom: 0 }}>
                                    <label className="form-label" htmlFor="edit-book-title-input">Title</label>
                                    <input id="edit-book-title-input" type="text" className="form-input" value={editTitle} onChange={e => setEditTitle(e.target.value)} maxLength={200} required />
                                </div>
                                <div className="form-group" style={{ marginBottom: 0 }}>
                                    <label className="form-label" htmlFor="edit-book-author">Author</label>
                                    <input id="edit-book-author" type="text" className="form-input" value={editAuthor} onChange={e => setEditAuthor(e.target.value)} maxLength={150} required />
                                </div>
                                <div className="form-group" style={{ marginBottom: 0 }}>
                                    <label className="form-label" htmlFor="edit-book-isbn">ISBN</label>
                                    <input id="edit-book-isbn" type="text" className="form-input" value={editIsbn} onChange={e => setEditIsbn(e.target.value)} maxLength={30} required />
                                </div>
                                <div className="form-group" style={{ marginBottom: 0 }}>
                                    <label className="form-label" htmlFor="edit-book-genre">Genre</label>
                                    <select id="edit-book-genre" className="form-input" value={editGenre} onChange={e => setEditGenre(e.target.value)} required>
                                        {!genres.some(item => item.name === editGenre) && editGenre && <option value={editGenre}>{editGenre}</option>}
                                        <option value="">Select a genre</option>
                                        {genres.map(item => <option key={item.id} value={item.name}>{item.name}</option>)}
                                    </select>
                                </div>
                                <div className="form-group" style={{ marginBottom: 0 }}>
                                    <label className="form-label" htmlFor="edit-book-cover">Cover Image URL</label>
                                    <input id="edit-book-cover" type="url" className="form-input" value={editCoverImageUrl} onChange={e => setEditCoverImageUrl(e.target.value)} maxLength={500} placeholder="https://example.com/book-cover.jpg" />
                                </div>
                                <div className="form-group" style={{ marginBottom: 0 }}>
                                    <label className="form-label" htmlFor="edit-book-copies">Total Copies</label>
                                    <input id="edit-book-copies" type="number" min="0" max="100000" className="form-input" value={editTotalCopies} onChange={e => setEditTotalCopies(e.target.value)} required />
                                </div>
                            </div>
                            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '1.5rem' }}>
                                <button type="button" className="btn-outline" onClick={closeEditModal} disabled={isUpdating}>Cancel</button>
                                <button type="submit" className="btn-primary" disabled={isUpdating}>
                                    {isUpdating ? 'Saving...' : 'Save Changes'}
                                </button>
                            </div>
                        </form>
                    </div>
                </dialog>
            )}

            {/* Add Book Form Panel */}
            {isFormOpen && (
                <div className="admin-inventory-form glass-panel" style={{ padding: '2rem', marginBottom: '2.5rem' }}>
                    <h2 style={{ fontSize: '1.25rem', marginBottom: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <BookPlus size={20} color="var(--accent-color)" />
                        Add New Book to Catalogue
                    </h2>
                    <p style={{ color: 'var(--text-secondary)', fontSize: '0.9rem', marginBottom: '1.5rem' }}>
                        Enter book details. System automatically sets ID, Available Copies (matches Total Copies), and timestamps.
                    </p>

                    <form onSubmit={handleSubmit}>
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1.25rem' }}>
                            <div className="form-group" style={{ marginBottom: 0 }}>
                                <label className="form-label" htmlFor="book-title">
                                    Title <span style={{ color: 'var(--danger-color)' }}>*</span>
                                </label>
                                <input
                                    id="book-title"
                                    type="text"
                                    className="form-input"
                                    placeholder="e.g. The Pragmatic Programmer"
                                    value={title}
                                    onChange={e => setTitle(e.target.value)}
                                    required
                                />
                            </div>

                            <div className="form-group" style={{ marginBottom: 0 }}>
                                <label className="form-label" htmlFor="book-author">
                                    Author <span style={{ color: 'var(--danger-color)' }}>*</span>
                                </label>
                                <input
                                    id="book-author"
                                    type="text"
                                    className="form-input"
                                    placeholder="e.g. Andrew Hunt, David Thomas"
                                    value={author}
                                    onChange={e => setAuthor(e.target.value)}
                                    required
                                />
                            </div>

                            <div className="form-group" style={{ marginBottom: 0 }}>
                                <label className="form-label" htmlFor="book-isbn">
                                    ISBN <span style={{ color: 'var(--danger-color)' }}>*</span>
                                </label>
                                <input
                                    id="book-isbn"
                                    type="text"
                                    className="form-input"
                                    placeholder="e.g. 978-0135957059"
                                    value={isbn}
                                    onChange={e => setIsbn(e.target.value)}
                                    required
                                />
                            </div>

                                <div className="form-group" style={{ marginBottom: 0 }}>
                                <label className="form-label" htmlFor="book-genre">
                                    Genre <span style={{ color: 'var(--danger-color)' }}>*</span>
                                </label>
                                    <select id="book-genre" className="form-input" value={genre} onChange={e => setGenre(e.target.value)} required>
                                        <option value="">Select a genre</option>
                                        {genres.map(item => <option key={item.id} value={item.name}>{item.name}</option>)}
                                    </select>
                            </div>

                            <div className="form-group" style={{ marginBottom: 0 }}>
                                <label className="form-label" htmlFor="book-cover">Cover Image URL</label>
                                <input
                                    id="book-cover"
                                    type="url"
                                    className="form-input"
                                    placeholder="https://example.com/book-cover.jpg"
                                    value={coverImageUrl}
                                    onChange={e => setCoverImageUrl(e.target.value)}
                                    maxLength={500}
                                />
                            </div>

                            <div className="form-group" style={{ marginBottom: 0 }}>
                                <label className="form-label" htmlFor="book-copies">
                                    Total Copies <span style={{ color: 'var(--danger-color)' }}>*</span>
                                </label>
                                <input
                                    id="book-copies"
                                    type="number"
                                    min="0"
                                    className="form-input"
                                    placeholder="e.g. 5"
                                    value={totalCopies}
                                    onChange={e => setTotalCopies(e.target.value)}
                                    required
                                />
                            </div>
                        </div>

                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1.5rem', gap: '1rem', flexWrap: 'wrap' }}>
                            <button type="button" className="btn-outline" onClick={() => { setIsGenreManagerOpen(true); setGenreErrorMessage(''); }} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                <Tags size={16} />
                                Manage Genres
                            </button>
                            <div style={{ display: 'flex', gap: '1rem' }}>
                            <button
                                type="button"
                                className="btn-outline"
                                onClick={() => {
                                    setTitle('');
                                    setAuthor('');
                                    setIsbn('');
                                    setGenre('');
                                    setCoverImageUrl('');
                                    setTotalCopies('1');
                                    setErrorMessage('');
                                }}
                            >
                                Clear
                            </button>
                            <button
                                type="submit"
                                className="btn-primary"
                                style={{ width: 'auto', padding: '0.75rem 2rem', marginTop: 0 }}
                                disabled={isSubmitting}
                            >
                                {isSubmitting ? 'Adding Book...' : 'Add Book'}
                            </button>
                            </div>
                        </div>
                    </form>
                </div>
            )}

            {/* Current Books Inventory List */}
            <div className="admin-inventory-panel glass-panel" style={{ padding: '2rem' }}>
                <div className="admin-inventory-toolbar" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem', flexWrap: 'wrap', gap: '1rem' }}>
                    <div className="admin-inventory-title" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <Layers size={20} color="var(--accent-color)" />
                        <h2 style={{ fontSize: '1.25rem' }}>Current Inventory ({books.length})</h2>
                    </div>

                    <div className="admin-inventory-tools" style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
                        <div className="admin-inventory-search" style={{ position: 'relative', width: '280px' }}>
                            <Search size={16} color="var(--text-secondary)" style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)' }} />
                            <input
                                type="text"
                                className="form-input"
                                style={{ paddingLeft: '2.25rem', fontSize: '0.9rem' }}
                                placeholder="Search by title, author, ISBN..."
                                value={searchQuery}
                                onChange={e => setSearchQuery(e.target.value)}
                            />
                        </div>
                        <button
                            onClick={fetchBooks}
                            className="btn-outline admin-refresh-button"
                            title="Refresh List"
                            style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.65rem' }}
                        >
                            <RefreshCw size={16} />
                        </button>
                    </div>
                </div>

                {isLoading && (
                    <p style={{ color: 'var(--text-secondary)', textAlign: 'center', padding: '2rem' }}>
                        Loading inventory...
                    </p>
                )}
                {!isLoading && filteredBooks.length === 0 && (
                    <p style={{ color: 'var(--text-secondary)', textAlign: 'center', padding: '2rem' }}>
                        {emptyInventoryMessage}
                    </p>
                )}
                {!isLoading && filteredBooks.length > 0 && (
                    <div style={{ overflowX: 'auto' }}>
                        <table className="admin-inventory-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                            <thead>
                                <tr style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.1)' }}>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500 }}>ID</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Cover</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Title</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Author</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500 }}>ISBN</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Genre</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500, textAlign: 'center' }}>Total</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500, textAlign: 'center' }}>Available</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Added On</th>
                                    <th style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredBooks.map(b => (
                                    <tr key={b.id} style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.05)' }}>
                                        <td style={{ padding: '0.75rem', color: 'var(--text-secondary)' }}>#{b.id}</td>
                                        <td style={{ padding: '0.75rem' }}>
                                            {b.coverImageUrl ? (
                                                <img
                                                    src={b.coverImageUrl}
                                                    alt={`${b.title} cover`}
                                                    loading="lazy"
                                                    onError={e => {
                                                        e.currentTarget.style.display = 'none';
                                                    }}
                                                    style={{ width: '42px', height: '56px', objectFit: 'cover', borderRadius: '4px', backgroundColor: 'rgba(255, 255, 255, 0.06)' }}
                                                />
                                            ) : (
                                                <div style={{ width: '42px', height: '56px', display: 'flex', alignItems: 'center', justifyContent: 'center', borderRadius: '4px', backgroundColor: 'rgba(59, 130, 246, 0.15)', color: '#93c5fd', fontSize: '0.75rem', fontWeight: 700 }}>
                                                    {b.title.slice(0, 1).toUpperCase()}
                                                </div>
                                            )}
                                        </td>
                                        <td style={{ padding: '0.75rem', fontWeight: 600, color: 'var(--text-primary)' }}>{b.title}</td>
                                        <td style={{ padding: '0.75rem', color: 'var(--text-secondary)' }}>{b.author}</td>
                                        <td style={{ padding: '0.75rem', fontFamily: 'monospace', fontSize: '0.85rem' }}>{b.isbn}</td>
                                        <td style={{ padding: '0.75rem' }}>
                                            <span style={{
                                                padding: '0.2rem 0.5rem',
                                                borderRadius: '4px',
                                                backgroundColor: 'rgba(59, 130, 246, 0.15)',
                                                color: '#93c5fd',
                                                fontSize: '0.8rem'
                                            }}>
                                                {b.genre}
                                            </span>
                                        </td>
                                        <td style={{ padding: '0.75rem', textAlign: 'center' }}>{b.totalCopies}</td>
                                        <td style={{ padding: '0.75rem', textAlign: 'center' }}>
                                            <span style={{
                                                padding: '0.2rem 0.5rem',
                                                borderRadius: '4px',
                                                backgroundColor: b.availableCopies > 0 ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)',
                                                color: b.availableCopies > 0 ? '#6ee7b7' : '#fca5a5',
                                                fontWeight: 600,
                                                fontSize: '0.85rem'
                                            }}>
                                                {b.availableCopies}
                                            </span>
                                        </td>
                                        <td style={{ padding: '0.75rem', color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
                                            {new Date(b.createdAt).toLocaleDateString()}
                                        </td>
                                        <td style={{ padding: '0.75rem' }}>
                                            <button
                                                type="button"
                                                className="btn-outline"
                                                onClick={() => openEditModal(b)}
                                                title={`Edit ${b.title}`}
                                                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', padding: '0.5rem 0.75rem' }}
                                            >
                                                <Pencil size={15} />
                                                Edit
                                            </button>
                                            {b.availableCopies > 0 ? (
                                                <button
                                                    type="button"
                                                    className="btn-outline"
                                                    onClick={() => handleMarkUnavailable(b)}
                                                    disabled={markingUnavailableId === b.id}
                                                    title={`Mark ${b.title} unavailable`}
                                                    style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', padding: '0.5rem 0.75rem', marginLeft: '0.5rem', color: '#fca5a5', borderColor: 'rgba(239, 68, 68, 0.35)' }}
                                                >
                                                    <CircleOff size={15} />
                                                    {markingUnavailableId === b.id ? 'Saving...' : 'Unavailable'}
                                                </button>
                                            ) : (
                                                <button
                                                    type="button"
                                                    className="btn-outline"
                                                    onClick={() => handleMarkAvailable(b)}
                                                    disabled={markingAvailableId === b.id}
                                                    title={`Make ${b.title} available`}
                                                    style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', padding: '0.5rem 0.75rem', marginLeft: '0.5rem', color: '#6ee7b7', borderColor: 'rgba(16, 185, 129, 0.35)' }}
                                                >
                                                    <CheckCircle size={15} />
                                                    {markingAvailableId === b.id ? 'Saving...' : 'Available'}
                                                </button>
                                            )}
                                            <button
                                                type="button"
                                                className="btn-outline"
                                                onClick={() => handleDeleteBook(b)}
                                                title={`Remove ${b.title}`}
                                                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', padding: '0.5rem 0.75rem', marginLeft: '0.5rem', color: '#fca5a5', borderColor: 'rgba(239, 68, 68, 0.4)' }}
                                            >
                                                <Trash2 size={15} />
                                                Remove
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
            </main>
        </div>
    );
};

export default AdminBooksPage;
