import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { useAuth } from '../contexts/AuthContext';
import axios from 'axios';
import { User, Save, ArrowLeft } from 'lucide-react';
import { Link } from 'react-router-dom';

const ProfilePage = () => {
    const { token } = useAuth();
    
    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState('');
    const [successMsg, setSuccessMsg] = useState('');

    useEffect(() => {
        const fetchProfile = async () => {
            try {
                const response = await axios.get('http://localhost:5000/api/user/profile', {
                    headers: { Authorization: `Bearer ${token}` }
                });
                setFirstName(response.data.firstName);
                setLastName(response.data.lastName);
                setEmail(response.data.email);
            } catch (err: any) {
                console.error(err);
                setError('Failed to load profile details.');
            } finally {
                setIsLoading(false);
            }
        };

        if (token) fetchProfile();
    }, [token]);

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        setSuccessMsg('');
        setIsSaving(true);

        try {
            await axios.put('http://localhost:5000/api/user/profile', {
                firstName,
                lastName,
                email,
                password: password || null
            }, {
                headers: { Authorization: `Bearer ${token}` }
            });

            setSuccessMsg('Profile updated successfully!');
            setPassword(''); // clear password field on success
        } catch (err: any) {
            setError(err.response?.data?.message || 'Failed to update profile.');
        } finally {
            setIsSaving(false);
        }
    };

    if (isLoading) {
        return (
            <div className="page-container" style={{ justifyContent: 'center', alignItems: 'center' }}>
                <p>Loading profile...</p>
            </div>
        );
    }

    return (
        <div className="page-container">
            <header className="dashboard-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    <User size={32} color="var(--accent-color)" />
                    <h1 style={{ fontSize: '1.5rem', fontWeight: 600 }}>My Profile</h1>
                </div>
                <Link to="/home" className="btn-outline" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', textDecoration: 'none' }}>
                    <ArrowLeft size={16} />
                    Back to Home
                </Link>
            </header>
            
            <main style={{ display: 'flex', justifyContent: 'center', marginTop: '2rem' }}>
                <div className="glass-panel" style={{ width: '100%', maxWidth: '600px', padding: '2rem' }}>
                    <h2 style={{ marginBottom: '1.5rem' }}>Edit Details</h2>

                    {error && <div className="error-message" style={{ marginBottom: '1rem' }}>{error}</div>}
                    {successMsg && <div style={{ padding: '1rem', backgroundColor: 'rgba(16, 185, 129, 0.1)', color: '#10b981', border: '1px solid rgba(16, 185, 129, 0.2)', borderRadius: '8px', marginBottom: '1rem' }}>{successMsg}</div>}

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
                            />
                        </div>
                        
                        <div className="form-group">
                            <label className="form-label" htmlFor="password">New Password (Optional)</label>
                            <input
                                id="password"
                                type="password"
                                className="form-input"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                placeholder="Leave blank to keep current password"
                                minLength={6}
                            />
                        </div>

                        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '2rem' }}>
                            <button type="submit" className="btn-primary" disabled={isSaving}>
                                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem' }}>
                                    <Save size={20} />
                                    {isSaving ? 'Saving...' : 'Save Changes'}
                                </div>
                            </button>
                        </div>
                    </form>
                </div>
            </main>
        </div>
    );
};

export default ProfilePage;
