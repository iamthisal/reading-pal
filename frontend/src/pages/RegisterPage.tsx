import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import axios from 'axios';
import { BookOpen, UserPlus, CheckCircle } from 'lucide-react';

const RegisterPage = () => {
    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [success, setSuccess] = useState(false);
    const [isLoading, setIsLoading] = useState(false);
    
    const navigate = useNavigate();

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        setSuccess(false);
        setIsLoading(true);

        try {
            await axios.post('http://localhost:5000/api/auth/register', {
                firstName,
                lastName,
                email,
                password
            });

            setSuccess(true);
            setTimeout(() => {
                navigate('/login');
            }, 3000);
        } catch (err: any) {
            setError(err.response?.data?.message || 'Failed to register. Please try again.');
        } finally {
            setIsLoading(false);
        }
    };

    if (success) {
        return (
            <div className="app-container">
                <div className="glass-panel login-card" style={{ textAlign: 'center' }}>
                    <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '1.5rem' }}>
                        <CheckCircle size={64} color="var(--success-color, #10b981)" />
                    </div>
                    <h2 style={{ marginBottom: '1rem' }}>Registration Successful!</h2>
                    <p style={{ color: 'var(--text-secondary)' }}>
                        Your account has been created. You will be redirected to the login page momentarily...
                    </p>
                </div>
            </div>
        );
    }

    return (
        <div className="app-container">
            <div className="glass-panel login-card">
                <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '1rem' }}>
                    <BookOpen size={48} color="var(--accent-color)" />
                </div>
                <h1>Create Account</h1>
                <p>Join Reading Pal today</p>

                {error && <div className="error-message">{error}</div>}

                <form onSubmit={handleSubmit}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                        <div className="form-group">
                            <label className="form-label" htmlFor="firstName">First Name</label>
                            <input
                                id="firstName"
                                type="text"
                                className="form-input"
                                value={firstName}
                                onChange={(e) => setFirstName(e.target.value)}
                                required
                                placeholder="John"
                            />
                        </div>
                        <div className="form-group">
                            <label className="form-label" htmlFor="lastName">Last Name</label>
                            <input
                                id="lastName"
                                type="text"
                                className="form-input"
                                value={lastName}
                                onChange={(e) => setLastName(e.target.value)}
                                required
                                placeholder="Doe"
                            />
                        </div>
                    </div>

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
                            placeholder="Min. 6 characters"
                            minLength={6}
                        />
                    </div>

                    <button type="submit" className="btn-primary" disabled={isLoading}>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem' }}>
                            <UserPlus size={20} />
                            {isLoading ? 'Registering...' : 'Register Account'}
                        </div>
                    </button>

                    <div style={{ marginTop: '1.5rem', fontSize: '0.9rem', color: 'var(--text-secondary)' }}>
                        Already have an account? <Link to="/login" style={{ color: 'var(--accent-color)', textDecoration: 'none', fontWeight: 500 }}>Sign in here</Link>
                    </div>
                </form>
            </div>
        </div>
    );
};

export default RegisterPage;
