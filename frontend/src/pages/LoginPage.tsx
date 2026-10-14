import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../contexts/AuthContext';
import { KeyRound, LogIn } from 'lucide-react';
import AuthShowcase from '../components/AuthShowcase';
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
            <AuthShowcase />

            <main className="auth-form-area">
                <div className="auth-form-card">
                    <span className="auth-wax-seal" aria-hidden="true"><KeyRound size={17} /></span>
                    <div className="auth-form-heading">
                        <p className="auth-eyebrow">Welcome back</p>
                        <h2>Sign in to your library</h2>
                        <p>Pick up where you left off.</p>
                    </div>

                    {error && <div className="error-message">{error}</div>}

                    <form onSubmit={handleSubmit} autoComplete="off">
                        <div className="form-group">
                            <label className="form-label" htmlFor="email">Email Address</label>
                            <input
                                id="email"
                                name="login-email"
                                type="email"
                                autoComplete="off"
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
                                name="login-password"
                                type="password"
                                autoComplete="new-password"
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
