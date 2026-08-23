import React, { createContext, useContext, useState, useEffect } from 'react';
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

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [token, setToken] = useState<string | null>(localStorage.getItem('token'));
    const [user, setUser] = useState<User | null>(null);

    useEffect(() => {
        if (token) {
            try {
                const decoded: any = jwtDecode(token);
                // The claims are mapped slightly differently by MS Identity Model depending on standard vs MS claim types
                // We'll extract them carefully.
                const role = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || decoded.role;
                const email = decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || decoded.email;
                const id = decoded.sub;
                const isValidated = decoded.IsValidated === 'True' || decoded.IsValidated === true;
                
                setUser({
                    id,
                    email,
                    role: role as 'Admin' | 'User',
                    isValidated
                });
                localStorage.setItem('token', token);
            } catch (error) {
                console.error("Invalid token:", error);
                logout();
            }
        } else {
            setUser(null);
            localStorage.removeItem('token');
        }
    }, [token]);

    const login = (newToken: string) => {
        setToken(newToken);
    };

    const logout = () => {
        setToken(null);
    };

    return (
        <AuthContext.Provider value={{ user, token, login, logout, isAuthenticated: !!user }}>
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
