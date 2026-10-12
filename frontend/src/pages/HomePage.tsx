import { useAuth } from '../contexts/AuthContext';
import { useCallback, useEffect, useState } from 'react';
import axios from 'axios';
import { Compass } from 'lucide-react';
import { INVENTORY_API_BASE_URL } from '../config/api';
import BannerSearch from '../components/layout/BannerSearch';
import PageBanner from '../components/layout/PageBanner';
import '../components/Skeleton/Skeleton.css';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import PosterSlider from '../components/PosterSlider/PosterSlider';
import type { Poster } from '../components/PosterSlider/PosterSlider';
import BorrowToast from '../components/BorrowToast';
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

const HomePage = () => {
    const { user } = useAuth();
    const [books, setBooks] = useState<Book[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState('');

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

    const availableCount = books.filter(book => (book.isAvailable ?? book.availableCopies > 0)).length;

    const posters: Poster[] = [
        { id: 'new', theme: 'forest', eyebrow: 'Fresh on the shelves', title: 'New arrivals', text: 'See the latest books added to the catalogue and reserve yours before they go.', action: { label: 'Browse books', to: '/search?sort=newest' } },
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

            <main id="discover" className="app-main app-main-banner discover-main">
                <PageBanner eyebrow="Available Books" title="Discover" icon={<Compass size={38} />}>
                    <BannerSearch />

                    {errorMessage && <div className="discover-error" role="alert">{errorMessage}</div>}

                    {!user?.isValidated && (
                        <div className="discover-warning">
                            <strong>Account Pending Approval:</strong> Your registration must be validated before reserving physical books.
                        </div>
                    )}

                    {!errorMessage && (
                        <div className="discover-stats" aria-busy={isLoading}>
                            {isLoading ? <><span className="skeleton skeleton-stat" /><span className="skeleton skeleton-stat" /></> : <>
                                <span>{books.length} {books.length === 1 ? 'book' : 'books'}</span>
                                <span>{availableCount} available now</span>
                            </>}
                        </div>
                    )}
                </PageBanner>

                <PosterSlider posters={posters} label="Library highlights" />
                <CatalogueSections liveBooks={books}
                    borrow={{ canBorrow: borrowing.canBorrow, pending: borrowing.pending, onBorrow: bookId => void borrowing.borrow(bookId) }} />

            </main>
        </div>
    );
};

export default HomePage;
