import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import axios from 'axios';
import { INVENTORY_API_BASE_URL } from '../config/api';
import { useAuth } from './AuthContext';

type FavouritesContextType = {
    isFavourite: (bookId: number) => boolean;
    /** Adds or removes the book. Resolves to false when the server refused, in which case nothing changed. */
    toggle: (bookId: number) => Promise<boolean>;
    count: number;
};

const FavouritesContext = createContext<FavouritesContextType | undefined>(undefined);
const url = `${INVENTORY_API_BASE_URL}/api/favourites`;

// Keeps the signed-in reader's favourite book IDs in one place, so every heart on every page agrees.
export const FavouritesProvider = ({ children }: { children: ReactNode }) => {
    const { token } = useAuth();
    const [ids, setIds] = useState<Set<number>>(new Set());

    useEffect(() => {
        if (!token) return;
        const controller = new AbortController();
        axios.get<number[]>(`${url}/ids`, { headers: { Authorization: `Bearer ${token}` }, signal: controller.signal })
            .then(response => setIds(new Set(response.data)))
            .catch(() => { /* Hearts simply start empty if favourites cannot be loaded. */ });
        // Signing out (or switching account) must not leave the previous reader's hearts behind.
        return () => { controller.abort(); setIds(new Set()); };
    }, [token]);

    const toggle = useCallback(async (bookId: number) => {
        if (!token) return false;
        const adding = !ids.has(bookId);
        const apply = (add: boolean) => setIds(current => {
            const next = new Set(current);
            if (add) next.add(bookId); else next.delete(bookId);
            return next;
        });

        // Fill the heart straight away; put it back if the server says no.
        apply(adding);
        try {
            await axios.request({ method: adding ? 'put' : 'delete', url: `${url}/${bookId}`, headers: { Authorization: `Bearer ${token}` } });
            return true;
        } catch {
            apply(!adding);
            return false;
        }
    }, [ids, token]);

    const value = useMemo(() => ({ isFavourite: (bookId: number) => ids.has(bookId), toggle, count: ids.size }), [ids, toggle]);

    return <FavouritesContext.Provider value={value}>{children}</FavouritesContext.Provider>;
};

export const useFavourites = () => {
    const context = useContext(FavouritesContext);
    if (!context) throw new Error('useFavourites must be used within a FavouritesProvider');
    return context;
};
