import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import axios from 'axios';
import { LENDING_API_BASE_URL } from '../config/api';
import { useAuth } from './AuthContext';

/** Where the signed-in member stands with a book: asked for it, or has a copy now. */
export type BorrowState = 'pending' | 'borrowed';

type BorrowStatusContextType = {
    stateOf: (bookId: number) => BorrowState | undefined;
    /** Marks a book as requested straight away, before the next refresh confirms it. */
    markPending: (bookId: number) => void;
    refresh: () => Promise<void>;
};

const BorrowStatusContext = createContext<BorrowStatusContextType | undefined>(undefined);
const REFRESH_MS = 30000;

interface MyBorrowings {
    pending?: { bookId: number }[];
    active?: { bookId: number }[];
}

// Keeps one picture of the member's reservations and loans for every Borrow button in the app, so a
// button can say Pending or Borrowed. It follows an admin's accept or reject by checking again every
// half minute while the page is visible, and whenever the reader comes back to the tab.
export const BorrowStatusProvider = ({ children }: { children: ReactNode }) => {
    const { token, user } = useAuth();
    const [states, setStates] = useState<Map<number, BorrowState>>(new Map());
    // Only members borrow; for admins and signed-out visitors there is nothing to load.
    const enabled = Boolean(token) && user?.role === 'User';

    const refresh = useCallback(async () => {
        if (!enabled) return;
        try {
            const { data } = await axios.get<MyBorrowings>(`${LENDING_API_BASE_URL}/api/my-borrowings`,
                { headers: { Authorization: `Bearer ${token}` } });
            const next = new Map<number, BorrowState>();
            (data.pending ?? []).forEach(r => next.set(r.bookId, 'pending'));
            // A copy in hand outranks a request for another one.
            (data.active ?? []).forEach(loan => next.set(loan.bookId, 'borrowed'));
            setStates(next);
        } catch {
            // Keep the last known picture; the next check will catch up.
        }
    }, [enabled, token]);

    useEffect(() => {
        if (!enabled) return;
        void refresh();
        const tick = () => { if (!document.hidden) void refresh(); };
        const timer = window.setInterval(tick, REFRESH_MS);
        window.addEventListener('focus', tick);
        document.addEventListener('visibilitychange', tick);
        return () => {
            window.clearInterval(timer);
            window.removeEventListener('focus', tick);
            document.removeEventListener('visibilitychange', tick);
            // A different reader signing in must not see the previous reader's states.
            setStates(new Map());
        };
    }, [enabled, refresh]);

    const markPending = useCallback((bookId: number) => setStates(current => {
        if (current.get(bookId) === 'borrowed') return current;
        const next = new Map(current);
        next.set(bookId, 'pending');
        return next;
    }), []);

    const value = useMemo(() => ({ stateOf: (bookId: number) => states.get(bookId), markPending, refresh }), [states, markPending, refresh]);
    return <BorrowStatusContext.Provider value={value}>{children}</BorrowStatusContext.Provider>;
};

export const useBorrowStatus = () => {
    const context = useContext(BorrowStatusContext);
    if (!context) throw new Error('useBorrowStatus must be used within a BorrowStatusProvider');
    return context;
};
