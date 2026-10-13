import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { useAuth } from '../contexts/AuthContext';
import axios from 'axios';
import { Save, User } from 'lucide-react';
import { API_BASE_URL } from '../config/api';
import PageBanner from '../components/layout/PageBanner';
import '../components/Skeleton/Skeleton.css';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';

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

    return (
        <div className="app-shell">
            <AppTopbar />
        <LibrarySidenav active="profile" />

            <main className="app-main app-main-banner profile-main discover-main">
                <PageBanner eyebrow="Your reading identity" title="My profile" icon={<User size={38} />}
                    text="Keep your details current so your Reading Pal experience stays personal." />

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

                    <form onSubmit={handleSubmit} aria-busy={isLoading}>
                        {/* While the details are loading the fields are greyed placeholders that cannot be typed in. */}
                        <fieldset className={`profile-fieldset${isLoading ? ' skeleton-fields' : ''}`} disabled={isLoading}>
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
                                placeholder="Leave blank to keep current"
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
                        </fieldset>
                    </form>
                </section>
            </main>
        </div>
    );
};

export default ProfilePage;
