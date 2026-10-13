import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import axios from 'axios';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { INVENTORY_API_BASE_URL } from '../config/api';
import { PenLine as PenLineIcon } from 'lucide-react';
import AppTopbar from '../components/layout/AppTopbar';
import PageBanner from '../components/layout/PageBanner';
import AdminSidebar from '../components/layout/AdminSidebar';
import Modal from '../components/Modal/Modal';
import './AdminCuration.css';

interface Author { id: number; name: string; description?: string | null; imageUrl?: string | null; bookCount: number }
/** The author being added (no id yet) or edited, as typed into the dialog. */
interface Draft { id: number | null; name: string; description: string; imageUrl: string }

const emptyDraft: Draft = { id: null, name: '', description: '', imageUrl: '' };
const failure = (err: unknown, fallback: string) =>
    axios.isAxiosError(err) ? err.response?.data?.message || fallback : fallback;

export default function AdminAuthorsPage() {
    const { token } = useAuth();
    const [authors, setAuthors] = useState<Author[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [message, setMessage] = useState('');
    const [busy, setBusy] = useState(false);
    // The add/edit dialog: null when closed. Its own error stays inside it.
    const [draft, setDraft] = useState<Draft | null>(null);
    const [draftError, setDraftError] = useState('');

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

    const openDraft = (next: Draft) => { setDraft(next); setDraftError(''); };

    const save = async (event: FormEvent) => {
        event.preventDefault();
        if (!draft) return;
        const name = draft.name.trim();
        if (!name) return;
        setBusy(true); setDraftError(''); setMessage(''); setError('');
        try {
            if (draft.id === null) {
                await axios.post(url, { name, description: draft.description.trim() || null, imageUrl: draft.imageUrl.trim() || null }, { headers });
                setMessage(`Added ${name}.`);
            } else {
                await axios.put(`${url}/${draft.id}`, { name, description: draft.description.trim(), imageUrl: draft.imageUrl.trim() }, { headers });
                setMessage(`Saved ${name}.`);
            }
            setDraft(null);
            await load();
        } catch (err) {
            setDraftError(failure(err, draft.id === null ? 'Unable to add the author.' : 'Unable to save the author.'));
        } finally { setBusy(false); }
    };

    const remove = async (author: Author) => {
        if (!window.confirm(`Remove ${author.name} from the authors? Their books stay in the catalogue, but they come off any list.`)) return;
        setBusy(true); setError(''); setMessage('');
        try {
            await axios.delete(`${url}/${author.id}`, { headers });
            setMessage(`Removed ${author.name}.`);
            await load();
        } catch (err) {
            setError(failure(err, 'Unable to remove the author.'));
        } finally { setBusy(false); }
    };

    const adding = draft?.id === null;

    return <div className="app-shell">
        <AppTopbar />
        <AdminSidebar active="authors" />
        <main className="app-main app-main-banner admin-main admin-users-main">
            <PageBanner eyebrow="Catalogue curation" title="Authors" icon={<PenLineIcon size={38} />}
                text={<>Authors you can feature on the catalogue page and add to lists. A book belongs to an author when the names match.</>}>
                <div className="page-banner-actions">
                    <button type="button" className="admin-primary-action" onClick={() => openDraft(emptyDraft)}><Plus size={17} />Add author</button>
                </div>
            </PageBanner>

            <section className="admin-users-panel glass-panel curation-panel" aria-label="Authors" aria-busy={loading}>
                <div className="admin-users-panel-heading"><h2>All authors</h2><span className="admin-users-count">{authors.length} {authors.length === 1 ? 'author' : 'authors'}</span></div>

                {error && <p role="alert" className="curation-message curation-message-error">{error}</p>}
                {message && <p role="status" className="curation-message">{message}</p>}

                {loading ? <p role="status" className="curation-empty">Loading authors…</p> : authors.length === 0 ? <p className="curation-empty">No authors yet. Use “Add author” to create the first one.</p> :
                    <div className="curation-table-wrap" tabIndex={0} role="region" aria-label="Authors table">
                        <table className="admin-users-table">
                            <thead><tr><th scope="col">Name</th><th scope="col">About</th><th scope="col">Books in catalogue</th><th scope="col">Actions</th></tr></thead>
                            <tbody>{authors.map(author => (
                                <tr key={author.id}>
                                    <td><span className="curation-author">
                                        {author.imageUrl ? <img src={author.imageUrl} alt="" loading="lazy" /> : <span aria-hidden="true">{author.name.slice(0, 1).toUpperCase()}</span>}
                                        {author.name}
                                    </span></td>
                                    <td className="curation-about">{author.description || '—'}</td>
                                    <td>{author.bookCount}</td>
                                    <td><div className="curation-actions">
                                        <button type="button" className="curation-button curation-button-small" disabled={busy}
                                            onClick={() => openDraft({ id: author.id, name: author.name, description: author.description ?? '', imageUrl: author.imageUrl ?? '' })}><Pencil size={14} />Edit</button>
                                        <button type="button" className="curation-button curation-button-small curation-button-danger" disabled={busy} onClick={() => void remove(author)}><Trash2 size={14} />Remove</button>
                                    </div></td>
                                </tr>
                            ))}</tbody>
                        </table>
                    </div>}
            </section>

            {draft && (
                <Modal title={adding ? 'Add an author' : 'Edit author'} busy={busy} onClose={() => setDraft(null)}
                    description={adding ? 'Use the exact name that appears on the books.' : 'Renaming an author also renames it on their books.'}>
                    {draftError && <p role="alert" className="modal-error">{draftError}</p>}
                    <form onSubmit={save}>
                        <div className="modal-fields">
                            <label>Name
                                <input id="author-name" value={draft.name} maxLength={150} required autoFocus placeholder="e.g. Agatha Christie"
                                    onChange={event => setDraft({ ...draft, name: event.target.value })} />
                            </label>
                            <label>Portrait link (optional)
                                <input id="author-image" type="url" value={draft.imageUrl} maxLength={500} placeholder="https://…"
                                    onChange={event => setDraft({ ...draft, imageUrl: event.target.value })} />
                            </label>
                            <label>About (optional)
                                <textarea id="author-description" value={draft.description} maxLength={2000} rows={4} placeholder="A short biography"
                                    onChange={event => setDraft({ ...draft, description: event.target.value })} />
                            </label>
                        </div>
                        <div className="modal-actions">
                            <button type="button" className="modal-button" disabled={busy} onClick={() => setDraft(null)}>Cancel</button>
                            <button type="submit" className="modal-button modal-button-primary" disabled={busy || !draft.name.trim()}>
                                {busy ? 'Saving…' : adding ? 'Add author' : 'Save changes'}
                            </button>
                        </div>
                    </form>
                </Modal>
            )}
        </main>
    </div>;
}
