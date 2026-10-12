import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import axios from 'axios';
import { useBorrow } from '../hooks/useBorrow';
import { INVENTORY_API_BASE_URL } from '../config/api';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import BackButton from '../components/BackButton';
import BorrowToast from '../components/BorrowToast';
import { BookShelf } from '../components/BookShelf/BookShelf';
import { DetailSkeleton, LazyImage } from '../components/Skeleton/Skeleton';
import type { ShelfBook } from '../components/BookShelf/BookShelf';
import './DetailPage.css';

interface AuthorProfile {
    id: number | null; name: string; description?: string | null; imageUrl?: string | null;
    bookCount: number; books: ShelfBook[];
}

const AuthorPage = () => {
    const { name = '' } = useParams();
    const [author, setAuthor] = useState<AuthorProfile | null>(null);
    const [status, setStatus] = useState<'loading' | 'ready' | 'missing' | 'error'>('loading');
    const [reload, setReload] = useState(0);
    const borrowing = useBorrow(() => setReload(value => value + 1));

    useEffect(() => {
        const controller = new AbortController();
        axios.get<AuthorProfile>(`${INVENTORY_API_BASE_URL}/api/authors/profile`, { params: { name }, signal: controller.signal })
            .then(response => { setAuthor(response.data); setStatus('ready'); })
            .catch(err => {
                if (controller.signal.aborted) return;
                setStatus(axios.isAxiosError(err) && [400, 404].includes(err.response?.status ?? 0) ? 'missing' : 'error');
            });
        return () => controller.abort();
    }, [name, reload]);

    const [shownName, setShownName] = useState(name);
    if (name !== shownName) {
        setShownName(name);
        setAuthor(null);
        setStatus('loading');
    }
    useEffect(() => { window.scrollTo({ top: 0 }); }, [name]);

    const available = author?.books.filter(book => book.availableCopies > 0).length ?? 0;

    return (
        <div className="app-shell">
            <AppTopbar />
            <LibrarySidenav />
            <BorrowToast success={borrowing.success} error={borrowing.error} onDismiss={borrowing.dismiss} />
            <main className="app-main detail-main">
                <BackButton />

                {status === 'loading' && <DetailSkeleton kind="author" />}
                {status === 'error' && <p role="alert" className="detail-status">Unable to load this author. Please try again.</p>}
                {status === 'missing' && (
                    <div className="detail-missing">
                        <h1>Author not found</h1>
                        <p>There are no books by “{name}” in the catalogue.</p>
                    </div>
                )}

                {author && status === 'ready' && <>
                    <header className="detail-card detail-author">
                        <LazyImage className="detail-author-portrait" src={author.imageUrl ?? undefined} alt={`Portrait of ${author.name}`}
                            fallback={<span className="detail-author-initial" aria-hidden="true">{author.name.slice(0, 1).toUpperCase()}</span>} />
                        <div className="detail-body">
                            <p className="detail-eyebrow">Author</p>
                            <h1>{author.name}</h1>
                            <p className="detail-byline">
                                {author.bookCount === 0 ? 'No books in the catalogue yet.'
                                    : `${author.bookCount} ${author.bookCount === 1 ? 'book' : 'books'} in the catalogue, ${available} available now.`}
                            </p>
                            {author.description && (
                                <section className="detail-about" aria-label="About the author">
                                    <h2>About the author</h2>
                                    <p>{author.description}</p>
                                </section>
                            )}
                        </div>
                    </header>

                    {/* Shown only when the catalogue holds books by this author. */}
                    <BookShelf title={`Books by ${author.name}`} books={author.books}
                        action={{ label: 'View all', to: `/search?q=${encodeURIComponent(author.name)}` }}
                        borrow={{ canBorrow: borrowing.canBorrow, pending: borrowing.pending, onBorrow: id => void borrowing.borrow(id) }} />
                </>}
            </main>
        </div>
    );
};

export default AuthorPage;
