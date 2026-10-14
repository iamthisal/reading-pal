import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import axios from 'axios';
import { CheckCircle, LogIn, UserPlus } from 'lucide-react';
import { API_BASE_URL } from '../config/api';
import AuthShowcase from '../components/AuthShowcase';

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
            await axios.post(`${API_BASE_URL}/api/auth/register`, {
                firstName,
                lastName,
                email,
                password
            });

            setSuccess(true);
            setTimeout(() => {
                navigate('/login');
            }, 3000);
        } catch (err: unknown) {
            setError(axios.isAxiosError(err) ? err.response?.data?.message || 'Failed to register. Please try again.' : 'Failed to register. Please try again.');
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="auth-page">
            <AuthShowcase />

            <main className="auth-form-area">
                <div className="auth-form-card">
                    <span className="auth-wax-seal" aria-hidden="true">{success ? <CheckCircle size={17} /> : <UserPlus size={17} />}</span>

                    {success ? (
                        <div className="auth-form-heading auth-register-success" role="status">
                            <p className="auth-eyebrow">Welcome aboard</p>
                            <h2>Registration Successful!</h2>
                            <p>Your account has been created. A librarian will approve it before you can borrow books. Taking you to sign in…</p>
                            <Link to="/login" className="auth-submit-button"><LogIn size={18} />Sign in now</Link>
                        </div>
                    ) : <>
                        <div className="auth-form-heading">
                            <p className="auth-eyebrow">Join Reading Pal</p>
                            <h2>Create your library card</h2>
                            <p>A few details and you can start exploring.</p>
                        </div>

                        {error && <div className="error-message">{error}</div>}

                        <form onSubmit={handleSubmit}>
                            <div className="auth-name-row">
                                <div className="form-group">
                                    <label className="form-label" htmlFor="firstName">First Name</label>
                                    <input id="firstName" type="text" className="form-input" autoComplete="given-name"
                                        value={firstName} onChange={(e) => setFirstName(e.target.value)} required placeholder="John" />
                                </div>
                                <div className="form-group">
                                    <label className="form-label" htmlFor="lastName">Last Name</label>
                                    <input id="lastName" type="text" className="form-input" autoComplete="family-name"
                                        value={lastName} onChange={(e) => setLastName(e.target.value)} required placeholder="Doe" />
                                </div>
                            </div>

                            <div className="form-group">
                                <label className="form-label" htmlFor="email">Email Address</label>
                                <input id="email" type="email" className="form-input" autoComplete="email"
                                    value={email} onChange={(e) => setEmail(e.target.value)} required placeholder="name@example.com" />
                            </div>

                            <div className="form-group">
                                <label className="form-label" htmlFor="password">Password</label>
                                <input id="password" type="password" className="form-input" autoComplete="new-password"
                                    value={password} onChange={(e) => setPassword(e.target.value)} required placeholder="Min. 6 characters" minLength={6} />
                            </div>

                            <button type="submit" className="auth-submit-button" disabled={isLoading}>
                                <UserPlus size={18} />
                                {isLoading ? 'Registering...' : 'Register Account'}
                            </button>

                            <div className="auth-register-prompt">
                                Already have an account? <Link to="/login">Sign in here</Link>
                            </div>
                        </form>
                    </>}
                </div>
            </main>
        </div>
    );
};

export default RegisterPage;
