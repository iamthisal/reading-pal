import { createContext, useContext, useState, useEffect, useMemo } from 'react';
import type { ReactNode } from 'react';
import { jwtDecode } from 'jwt-decode';

interface User {
    id: string;
    email: string;
    role: 'Admin' | 'User';
    isValidated: boolean;
}

interface AuthContextType {
    user: User | null;
    token: string | null;
    login: (token: string) => void;
    logout: () => void;
    isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

// Reads the user from a token, or null if it is missing, malformed or expired.
const userFromToken = (token: string | null): User | null => {
    if (!token) return null;
    try {
        const decoded: any = jwtDecode(token);
        if (typeof decoded.exp === 'number' && decoded.exp * 1000 <= Date.now()) return null;
        // The claims are mapped slightly differently by MS Identity Model depending on standard vs MS claim types
        // We'll extract them carefully.
        const role = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || decoded.role;
        const email = decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || decoded.email;
        return {
            id: decoded.sub,
            email,
            role: role as 'Admin' | 'User',
            isValidated: decoded.IsValidated === 'True' || decoded.IsValidated === true
        };
    } catch (error) {
        console.error("Invalid token:", error);
        return null;
    }
};

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const [token, setToken] = useState<string | null>(() => {
        const stored = localStorage.getItem('token');
        return userFromToken(stored) ? stored : null;
    });
    // Derived during render, so a page refresh knows the user before routes are checked
    // (deriving it in an effect left the first render logged out and redirected to /login).
    const user = useMemo(() => userFromToken(token), [token]);

    useEffect(() => {
        if (token && user) localStorage.setItem('token', token);
        else localStorage.removeItem('token');
    }, [token, user]);

    // Sign out when the token expires mid-session, instead of polling the APIs with a rejected token.
    useEffect(() => {
        if (!token) return;
        const exp = (() => { try { return (jwtDecode(token) as { exp?: number }).exp; } catch { return undefined; } })();
        if (typeof exp !== 'number') return;
        const timer = window.setTimeout(() => setToken(null), Math.max(0, exp * 1000 - Date.now()));
        return () => window.clearTimeout(timer);
    }, [token]);

    const login = (newToken: string) => {
        setToken(newToken);
    };

    const logout = () => {
        setToken(null);
    };

    return (
        <AuthContext.Provider value={{ user, token: user ? token : null, login, logout, isAuthenticated: !!user }}>
            {children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (context === undefined) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
};
