import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import axios from 'axios';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { INVENTORY_API_BASE_URL } from '../config/api';
import AdminSidebar from '../components/layout/AdminSidebar';
import NotificationBell from '../components/NotificationBell';
import './AdminCuration.css';

interface Author { id: number; name: string; bookCount: number }

const failure = (err: unknown, fallback: string) =>
    axios.isAxiosError(err) ? err.response?.data?.message || fallback : fallback;

export default function AdminAuthorsPage() {
    const { token } = useAuth();
    const [authors, setAuthors] = useState<Author[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [message, setMessage] = useState('');
    const [newName, setNewName] = useState('');
    const [editingId, setEditingId] = useState<number | null>(null);
    const [editingName, setEditingName] = useState('');
    const [busy, setBusy] = useState(false);

    const headers = { Authorization: `Bearer ${token}` };
    const url = `${INVENTORY_API_BASE_URL}/api/authors`;

    const load = useCallback(async () => {
        try {
            setAuthors((await axios.get<Author[]>(`${INVENTORY_API_BASE_URL}/api/authors`)).data);
        } catch (err) {
            setError(failure(err, 'Unable to load authors. Please refresh.'));
        } finally { setLoading(false); }
    }, []);
    useEffect(() => { void load(); }, [load]);

    // Runs one change, then reloads so book counts and ordering come from the server.
    const run = async (action: () => Promise<unknown>, done: string, fallback: string) => {
        setBusy(true); setError(''); setMessage('');
        try {
            await action();
            setMessage(done);
            await load();
            return true;
        } catch (err) {
            setError(failure(err, fallback));
            return false;
        } finally { setBusy(false); }
    };

    const create = async (event: FormEvent) => {
        event.preventDefault();
        const name = newName.trim();
        if (!name) return;
        if (await run(() => axios.post(url, { name }, { headers }), `Added ${name}.`, 'Unable to add the author.')) setNewName('');
    };

    const rename = async (author: Author) => {
        const name = editingName.trim();
        if (!name || name === author.name) { setEditingId(null); return; }
        if (await run(() => axios.put(`${url}/${author.id}`, { name }, { headers }), `Renamed ${author.name} to ${name}.`, 'Unable to rename the author.')) setEditingId(null);
    };

    const remove = async (author: Author) => {
        if (!window.confirm(`Remove ${author.name} from the authors? Their books stay in the catalogue, but they come off any list.`)) return;
        await run(() => axios.delete(`${url}/${author.id}`, { headers }), `Removed ${author.name}.`, 'Unable to remove the author.');
    };

    return <div className="admin-shell">
        <AdminSidebar active="authors" />
        <main className="admin-main admin-users-main">
            <header className="admin-topbar admin-users-topbar">
                <div>
                    <p className="admin-eyebrow">Catalogue curation</p>
                    <h1>Authors</h1>
                    <p className="admin-users-subtitle">Authors you can feature on the catalogue page and add to lists. A book belongs to an author when the names match.</p>
                </div>
                <div className="admin-topbar-actions"><NotificationBell /></div>
            </header>

            <section className="admin-users-panel glass-panel curation-panel" aria-label="Authors" aria-busy={loading}>
                <div className="admin-users-panel-heading"><h2>All authors</h2><span className="admin-users-count">{authors.length} {authors.length === 1 ? 'author' : 'authors'}</span></div>

                <form className="curation-form" onSubmit={create}>
                    <label>New author
                        <input id="author-name" value={newName} onChange={event => setNewName(event.target.value)} maxLength={150} placeholder="e.g. Agatha Christie" />
                    </label>
                    <button type="submit" className="curation-button curation-button-primary" disabled={busy || !newName.trim()}><Plus size={16} />Add author</button>
                </form>
                <p className="curation-hint">Use the exact name that appears on the books. Renaming an author here also renames it on their books.</p>

                {error && <p role="alert" className="curation-message curation-message-error">{error}</p>}
                {message && <p role="status" className="curation-message">{message}</p>}

                {loading ? <p role="status" className="curation-empty">Loading authors…</p> : authors.length === 0 ? <p className="curation-empty">No authors yet. Add the first one above.</p> :
                    <div className="curation-table-wrap" tabIndex={0} role="region" aria-label="Authors table">
                        <table className="admin-users-table">
                            <thead><tr><th scope="col">Name</th><th scope="col">Books in catalogue</th><th scope="col">Actions</th></tr></thead>
                            <tbody>{authors.map(author => <tr key={author.id}>
                                <td>{editingId === author.id
                                    ? <input className="curation-inline-input" aria-label={`New name for ${author.name}`} value={editingName} maxLength={150} autoFocus
                                        onChange={event => setEditingName(event.target.value)}
                                        onKeyDown={event => { if (event.key === 'Enter') void rename(author); if (event.key === 'Escape') setEditingId(null); }} />
                                    : author.name}</td>
                                <td>{author.bookCount}</td>
                                <td><div className="curation-actions">
                                    {editingId === author.id ? <>
                                        <button type="button" className="curation-button curation-button-small curation-button-primary" disabled={busy} onClick={() => void rename(author)}>Save</button>
                                        <button type="button" className="curation-button curation-button-small" onClick={() => setEditingId(null)}>Cancel</button>
                                    </> : <>
                                        <button type="button" className="curation-button curation-button-small" disabled={busy} onClick={() => { setEditingId(author.id); setEditingName(author.name); }}><Pencil size={14} />Rename</button>
                                        <button type="button" className="curation-button curation-button-small curation-button-danger" disabled={busy} onClick={() => void remove(author)}><Trash2 size={14} />Remove</button>
                                    </>}
                                </div></td>
                            </tr>)}</tbody>
                        </table>
                    </div>}
            </section>
        </main>
    </div>;
}
