import { useEffect, useState } from 'react';
import axios from 'axios';
import { LENDING_API_BASE_URL } from '../config/api';
import { useAuth } from '../contexts/AuthContext';

// Borrowing a book (a reservation that an admin then approves), with the progress and result
// the page needs to show. Shared by every page that has Borrow buttons.
export const useBorrow = (onBorrowed?: () => void) => {
    const { user, token } = useAuth();
    const [pending, setPending] = useState<Record<number, boolean>>({});
    const [success, setSuccess] = useState('');
    const [error, setError] = useState('');

    useEffect(() => {
        if (!success) return;
        const timeout = window.setTimeout(() => setSuccess(''), 5000);
        return () => window.clearTimeout(timeout);
    }, [success]);

    const borrow = async (bookId: number) => {
        if (!user || !token) return;
        setPending(current => ({ ...current, [bookId]: true }));
        setError('');
        setSuccess('');

        try {
            await axios.post(`${LENDING_API_BASE_URL}/api/Reservations`, { bookId }, { headers: { Authorization: `Bearer ${token}` } });
            setSuccess('Book reserved successfully! It is now pending admin approval.');
            onBorrowed?.();
        } catch (err) {
            console.error('Failed to reserve book:', err);
            const data = axios.isAxiosError(err) ? err.response?.data : undefined;
            setError(data?.message || data?.title || (typeof data === 'string' ? data : 'Failed to reserve book. Please try again.'));
        } finally {
            setPending(current => ({ ...current, [bookId]: false }));
        }
    };

    return {
        /** Only approved readers may borrow. */
        canBorrow: Boolean(user?.isValidated),
        pending,
        borrow,
        success,
        error,
        dismiss: () => { setSuccess(''); setError(''); },
    };
};
