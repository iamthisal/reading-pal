import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { useAuth } from '../contexts/AuthContext';
import axios from 'axios';
import { BookMarked, BookOpen, Heart, LayoutDashboard, Library, LogOut, Save, User } from 'lucide-react';
import { Link } from 'react-router-dom';
import { API_BASE_URL } from '../config/api';

const ProfilePage = () => {
    const { logout, token, user } = useAuth();
    
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
                const response = await axios.get(`${API_BASE_URL}/api/user/profile`, {
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
            await axios.put(`${API_BASE_URL}/api/user/profile`, {
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
            <div className="profile-shell profile-loading">
                <p>Loading profile...</p>
            </div>
        );
    }

    return (
        <div className="profile-shell discover-shell">
            <aside className="discover-sidebar">
                <Link to="/home" className="discover-brand">
                    <BookMarked size={24} />
                    <span>Reading Pal</span>
                </Link>

                <nav className="discover-nav" aria-label="Library navigation">
                    <span className="discover-nav-label">Menu</span>
                    <Link to="/home" className="discover-nav-item">
                        <BookOpen size={16} />
                        Discover
                    </Link>
                    <a href="/home#recommendations" className="discover-nav-item">
                        <Library size={16} />
                        My Library
                    </a>
                    <a href="/home#recommendations" className="discover-nav-item">
                        <Heart size={16} />
                        Favorite
                    </a>
                </nav>

                <div className="discover-sidebar-bottom">
                    {user?.role === 'Admin' ? (
                        <Link to="/admin/dashboard" className="discover-nav-item">
                            <LayoutDashboard size={16} />
                            Dashboard
                        </Link>
                    ) : (
                        <Link to="/profile" className="discover-nav-item discover-nav-item-active">
                            <User size={16} />
                            My Profile
                        </Link>
                    )}
                    <button type="button" onClick={logout} className="discover-nav-item discover-nav-button">
                        <LogOut size={16} />
                        Log out
                    </button>
                </div>
            </aside>

            <main className="profile-main discover-main">
                <header className="profile-topbar">
                    <div className="profile-user-chip">
                        <span className="discover-avatar">{email?.slice(0, 1).toUpperCase() || 'R'}</span>
                        <span>{email || 'Reader'}</span>
                    </div>
                </header>

                <section className="profile-hero">
                    <div>
                        <p className="discover-eyebrow">Your reading identity</p>
                        <h1>My profile</h1>
                        <p>Keep your details current so your Reading Pal experience stays personal.</p>
                    </div>
                    <div className="profile-hero-mark"><User size={38} /></div>
                </section>

                <section className="profile-form-panel">
                    <div className="profile-panel-heading">
                        <div>
                            <p className="discover-eyebrow">Account settings</p>
                            <h2>Edit details</h2>
                        </div>
                        <span className="profile-account-label">Member account</span>
                    </div>

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

                        <div className="profile-form-actions" style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '2rem' }}>
                            <button type="submit" className="profile-save-button btn-primary" disabled={isSaving}>
                                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem' }}>
                                    <Save size={20} />
                                    {isSaving ? 'Saving...' : 'Save Changes'}
                                </div>
                            </button>
                        </div>
                    </form>
                </section>
            </main>
        </div>
    );
};

export default ProfilePage;
