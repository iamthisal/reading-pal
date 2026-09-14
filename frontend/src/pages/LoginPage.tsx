import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../contexts/AuthContext';
import { BookOpen, KeyRound, LogIn, Sparkles } from 'lucide-react';
import { API_BASE_URL } from '../config/api';

const LoginPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    
    const navigate = useNavigate();
    const { login } = useAuth();

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        setIsLoading(true);

        try {
            const response = await axios.post(`${API_BASE_URL}/api/auth/login`, {
                email,
                password
            });

            const { token, role } = response.data;
            login(token);

            if (role === 'Admin') {
                navigate('/admin/dashboard');
            } else {
                navigate('/home');
            }
        } catch (err: any) {
            setError(err.response?.data?.message || 'Failed to login. Please try again.');
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="auth-page">
            <section className="auth-showcase">
                <div className="auth-showcase-topline">
                    <BookOpen size={22} />
                    <span>Reading Pal</span>
                </div>
                <div className="auth-showcase-copy">
                    <p className="auth-eyebrow">A quieter place to read</p>
                    <h1>Make room for a good story.</h1>
                    <p>Keep your library close, discover your next favorite book, and return to the pages that stay with you.</p>
                </div>
                <div className="auth-reading-object" aria-hidden="true">
                    <span className="auth-book-cover auth-book-cover-back" />
                    <span className="auth-book-cover auth-book-cover-front" />
                    <span className="auth-book-spine" />
                    <span className="auth-book-page auth-book-page-left" />
                    <span className="auth-book-page auth-book-page-right" />
                    <span className="auth-book-page auth-book-page-turn" />
                </div>
                <div className="auth-book-stack" aria-hidden="true">
                    <span className="auth-book auth-book-one">READ</span>
                    <span className="auth-book auth-book-two">WANDER</span>
                    <span className="auth-book auth-book-three">NOTES</span>
                </div>
                <div className="auth-showcase-footer">
                    <Sparkles size={16} />
                    <span>Your reading life, in one place.</span>
                </div>
            </section>

            <main className="auth-form-area">
                <div className="auth-form-card">
                    <span className="auth-wax-seal" aria-hidden="true"><KeyRound size={17} /></span>
                    <div className="auth-form-heading">
                        <p className="auth-eyebrow">Welcome back</p>
                        <h2>Sign in to your library</h2>
                        <p>Pick up where you left off.</p>
                    </div>

                    {error && <div className="error-message">{error}</div>}

                    <form onSubmit={handleSubmit}>
                        <div className="form-group">
                            <label className="form-label" htmlFor="email">Email Address</label>
                            <input
                                id="email"
                                type="email"
                                className="form-input"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                required
                                placeholder="name@example.com"
                            />
                        </div>
                        
                        <div className="form-group">
                            <label className="form-label" htmlFor="password">Password</label>
                            <input
                                id="password"
                                type="password"
                                className="form-input"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                required
                                placeholder="Enter your password"
                            />
                        </div>

                        <button type="submit" className="auth-submit-button" disabled={isLoading}>
                            <LogIn size={18} />
                            {isLoading ? 'Signing in...' : 'Sign in'}
                        </button>

                        <div className="auth-register-prompt">
                            Don't have an account? <Link to="/register">Register here</Link>
                        </div>
                    </form>
                </div>
            </main>
        </div>
    );
};

export default LoginPage;
