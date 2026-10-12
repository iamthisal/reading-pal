import { useCallback, useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import axios from 'axios';
import { ArrowDown, ArrowUp, Pencil, Plus, Trash2, X } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { INVENTORY_API_BASE_URL } from '../config/api';
import AdminSidebar from '../components/layout/AdminSidebar';
import NotificationBell from '../components/NotificationBell';
import './AdminCuration.css';

type ItemType = 'Books' | 'Authors';
interface ListSummary { id: number; key: string; name: string; itemType: ItemType; itemCount: number }
interface Book { id: number; title: string; author: string }
interface Author { id: number; name: string; bookCount: number }
interface ListDetail extends ListSummary { books: Book[]; authors: Author[] }
/** A book or an author in the shape the editor shows. */
interface Entry { id: number; label: string; detail: string }

const api = `${INVENTORY_API_BASE_URL}/api`;
const failure = (err: unknown, fallback: string) =>
    axios.isAxiosError(err) ? err.response?.data?.message || fallback : fallback;
const toKey = (name: string) => name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
const bookEntry = (book: Book): Entry => ({ id: book.id, label: book.title, detail: book.author });
const authorEntry = (author: Author): Entry => ({ id: author.id, label: author.name, detail: `${author.bookCount} ${author.bookCount === 1 ? 'book' : 'books'}` });

export default function AdminListsPage() {
    const { token } = useAuth();
    const [lists, setLists] = useState<ListSummary[]>([]);
    const [books, setBooks] = useState<Book[]>([]);
    const [authors, setAuthors] = useState<Author[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [message, setMessage] = useState('');
    const [busy, setBusy] = useState(false);

    const [newName, setNewName] = useState('');
    const [newKey, setNewKey] = useState('');
    const [keyEdited, setKeyEdited] = useState(false);
    const [newType, setNewType] = useState<ItemType>('Books');

    const [renamingKey, setRenamingKey] = useState<string | null>(null);
    const [renamingName, setRenamingName] = useState('');

    // The list being edited, and its contents as they stand in the editor (unsaved until "Save list").
    const [editing, setEditing] = useState<ListSummary | null>(null);
    const [items, setItems] = useState<Entry[]>([]);
    const [savedIds, setSavedIds] = useState<number[]>([]);
    const [search, setSearch] = useState('');

    const headers = { Authorization: `Bearer ${token}` };

    const loadLists = useCallback(async () => {
        setLists((await axios.get<ListSummary[]>(`${api}/lists`)).data);
    }, []);

    useEffect(() => {
        void (async () => {
            try {
                const [, bookResponse, authorResponse] = await Promise.all([
                    loadLists(), axios.get<Book[]>(`${api}/books`), axios.get<Author[]>(`${api}/authors`)]);
                setBooks(bookResponse.data);
                setAuthors(authorResponse.data);
            } catch (err) {
                setError(failure(err, 'Unable to load the lists. Please refresh.'));
            } finally { setLoading(false); }
        })();
    }, [loadLists]);

    const run = async (action: () => Promise<unknown>, done: string, fallback: string) => {
        setBusy(true); setError(''); setMessage('');
        try {
            await action();
            setMessage(done);
            await loadLists();
            return true;
        } catch (err) {
            setError(failure(err, fallback));
            return false;
        } finally { setBusy(false); }
    };

    const create = async (event: FormEvent) => {
        event.preventDefault();
        const name = newName.trim();
        const key = (keyEdited ? newKey : toKey(name)).trim();
        if (!name || !key) return;
        if (await run(() => axios.post(`${api}/lists`, { key, name, itemType: newType }, { headers }), `Created "${name}". Add its ${newType.toLowerCase()} below.`, 'Unable to create the list.')) {
            setNewName(''); setNewKey(''); setKeyEdited(false);
            await openEditor({ id: 0, key, name, itemType: newType, itemCount: 0 });
        }
    };

    const rename = async (list: ListSummary) => {
        const name = renamingName.trim();
        if (!name || name === list.name) { setRenamingKey(null); return; }
        if (await run(() => axios.put(`${api}/lists/${list.key}`, { name }, { headers }), `Renamed to "${name}".`, 'Unable to rename the list.')) {
            setRenamingKey(null);
            if (editing?.key === list.key) setEditing({ ...editing, name });
        }
    };

    const remove = async (list: ListSummary) => {
        if (!window.confirm(`Delete the list "${list.name}"? Sections of the site that show it will disappear. The ${list.itemType.toLowerCase()} themselves are kept.`)) return;
        if (await run(() => axios.delete(`${api}/lists/${list.key}`, { headers }), `Deleted "${list.name}".`, 'Unable to delete the list.') && editing?.key === list.key) setEditing(null);
    };

    const openEditor = async (list: ListSummary) => {
        if (editing && dirty && !window.confirm('Discard the unsaved changes to the list you are editing?')) return;
        setError('');
        try {
            const detail = (await axios.get<ListDetail>(`${api}/lists/${list.key}`)).data;
            const entries = detail.itemType === 'Books' ? detail.books.map(bookEntry) : detail.authors.map(authorEntry);
            setEditing(detail);
            setItems(entries);
            setSavedIds(entries.map(entry => entry.id));
            setSearch('');
        } catch (err) {
            setError(failure(err, 'Unable to open the list.'));
        }
    };

    const dirty = editing !== null && (items.length !== savedIds.length || items.some((item, index) => item.id !== savedIds[index]));

    const candidates = useMemo(() => {
        if (!editing) return [];
        const onList = new Set(items.map(item => item.id));
        const query = search.trim().toLowerCase();
        const all = editing.itemType === 'Books' ? books.map(bookEntry) : authors.map(authorEntry);
        return all
            .filter(entry => !onList.has(entry.id))
            .filter(entry => !query || entry.label.toLowerCase().includes(query) || entry.detail.toLowerCase().includes(query))
            .sort((first, second) => first.label.localeCompare(second.label));
    }, [editing, items, search, books, authors]);

    const move = (index: number, by: 1 | -1) => setItems(current => {
        const next = [...current];
        [next[index], next[index + by]] = [next[index + by], next[index]];
        return next;
    });

    const save = async () => {
        if (!editing) return;
        const ids = items.map(item => item.id);
        if (await run(() => axios.put(`${api}/lists/${editing.key}/items`, { itemIds: ids }, { headers }), `Saved "${editing.name}" with ${ids.length} ${ids.length === 1 ? 'item' : 'items'}.`, 'Unable to save the list.')) setSavedIds(ids);
    };

    const noun = editing?.itemType === 'Authors' ? 'authors' : 'books';

    return <div className="admin-shell">
        <AdminSidebar active="lists" />
        <main className="admin-main admin-users-main">
            <header className="admin-topbar admin-users-topbar">
                <div>
                    <p className="admin-eyebrow">Catalogue curation</p>
                    <h1>Curated lists</h1>
                    <p className="admin-users-subtitle">Hand-picked, ordered lists of books or authors. The catalogue page shows a list by its key.</p>
                </div>
                <div className="admin-topbar-actions"><NotificationBell /></div>
            </header>

            <section className="admin-users-panel glass-panel curation-panel" aria-label="Curated lists" aria-busy={loading}>
                <div className="admin-users-panel-heading"><h2>All lists</h2><span className="admin-users-count">{lists.length} {lists.length === 1 ? 'list' : 'lists'}</span></div>

                <form className="curation-form" onSubmit={create}>
                    <label>List name
                        <input id="list-name" value={newName} maxLength={150} placeholder="e.g. Staff picks" onChange={event => setNewName(event.target.value)} />
                    </label>
                    <label>Key
                        <input id="list-key" value={keyEdited ? newKey : toKey(newName)} maxLength={80} placeholder="staff-picks"
                            onChange={event => { setKeyEdited(true); setNewKey(event.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '')); }} />
                    </label>
                    <label className="curation-field-narrow">Contains
                        <select id="list-type" value={newType} onChange={event => setNewType(event.target.value as ItemType)}>
                            <option value="Books">Books</option><option value="Authors">Authors</option>
                        </select>
                    </label>
                    <button type="submit" className="curation-button curation-button-primary" disabled={busy || !newName.trim()}><Plus size={16} />Create list</button>
                </form>
                <p className="curation-hint">The key and what a list contains cannot be changed later, because pages refer to the list by its key.</p>

                {error && <p role="alert" className="curation-message curation-message-error">{error}</p>}
                {message && <p role="status" className="curation-message">{message}</p>}

                {loading ? <p role="status" className="curation-empty">Loading lists…</p> : lists.length === 0 ? <p className="curation-empty">No lists yet. Create the first one above.</p> :
                    <div className="curation-table-wrap" tabIndex={0} role="region" aria-label="Lists table">
                        <table className="admin-users-table">
                            <thead><tr><th scope="col">Name</th><th scope="col">Key</th><th scope="col">Contains</th><th scope="col">Items</th><th scope="col">Actions</th></tr></thead>
                            <tbody>{lists.map(list => <tr key={list.key}>
                                <td>{renamingKey === list.key
                                    ? <input className="curation-inline-input" aria-label={`New name for ${list.name}`} value={renamingName} maxLength={150} autoFocus
                                        onChange={event => setRenamingName(event.target.value)}
                                        onKeyDown={event => { if (event.key === 'Enter') void rename(list); if (event.key === 'Escape') setRenamingKey(null); }} />
                                    : list.name}</td>
                                <td><span className="curation-key">{list.key}</span></td>
                                <td>{list.itemType}</td>
                                <td>{list.itemCount}</td>
                                <td><div className="curation-actions">
                                    {renamingKey === list.key ? <>
                                        <button type="button" className="curation-button curation-button-small curation-button-primary" disabled={busy} onClick={() => void rename(list)}>Save</button>
                                        <button type="button" className="curation-button curation-button-small" onClick={() => setRenamingKey(null)}>Cancel</button>
                                    </> : <>
                                        <button type="button" className="curation-button curation-button-small curation-button-primary" disabled={busy} onClick={() => void openEditor(list)}>Edit contents</button>
                                        <button type="button" className="curation-button curation-button-small" disabled={busy} onClick={() => { setRenamingKey(list.key); setRenamingName(list.name); }}><Pencil size={14} />Rename</button>
                                        <button type="button" className="curation-button curation-button-small curation-button-danger" disabled={busy} onClick={() => void remove(list)}><Trash2 size={14} />Delete</button>
                                    </>}
                                </div></td>
                            </tr>)}</tbody>
                        </table>
                    </div>}
            </section>

            {editing && <section className="admin-users-panel glass-panel curation-panel" aria-label={`Contents of ${editing.name}`}>
                <div className="admin-users-panel-heading"><h2>{editing.name}</h2><span className="admin-users-count">{items.length} {noun}{dirty ? ' · unsaved changes' : ''}</span></div>
                <div className="curation-editor">
                    <div>
                        <h3>On this list, in order</h3>
                        {items.length === 0 ? <p className="curation-empty">Nothing on this list yet. Add {noun} from the right.</p> :
                            <ol className="curation-items">{items.map((item, index) => <li key={item.id} className="curation-item">
                                <span className="curation-item-position">{index + 1}</span>
                                <span className="curation-item-text"><strong>{item.label}</strong><small>{item.detail}</small></span>
                                <button type="button" className="curation-button curation-button-icon" aria-label={`Move ${item.label} up`} disabled={index === 0} onClick={() => move(index, -1)}><ArrowUp size={15} /></button>
                                <button type="button" className="curation-button curation-button-icon" aria-label={`Move ${item.label} down`} disabled={index === items.length - 1} onClick={() => move(index, 1)}><ArrowDown size={15} /></button>
                                <button type="button" className="curation-button curation-button-icon curation-button-danger" aria-label={`Remove ${item.label} from the list`} onClick={() => setItems(current => current.filter(entry => entry.id !== item.id))}><X size={15} /></button>
                            </li>)}</ol>}
                    </div>
                    <div>
                        <h3>Add {noun}</h3>
                        <input className="curation-search" type="search" value={search} onChange={event => setSearch(event.target.value)}
                            placeholder={editing.itemType === 'Books' ? 'Search by title or author' : 'Search by name'} aria-label={`Search ${noun} to add`} />
                        {candidates.length === 0 ? <p className="curation-empty">{search ? `No ${noun} match that search.` : `Every one of the ${noun} is already on this list.`}</p> :
                            <ul className="curation-items">{candidates.map(entry => <li key={entry.id} className="curation-item">
                                <span className="curation-item-text"><strong>{entry.label}</strong><small>{entry.detail}</small></span>
                                <button type="button" className="curation-button curation-button-small" aria-label={`Add ${entry.label} to the list`} onClick={() => setItems(current => [...current, entry])}><Plus size={14} />Add</button>
                            </li>)}</ul>}
                    </div>
                </div>
                <div className="curation-editor-footer">
                    <span>{dirty ? 'Changes are not visible to readers until you save.' : 'All changes saved.'}</span>
                    <button type="button" className="curation-button" disabled={busy} onClick={() => { if (!dirty || window.confirm('Discard the unsaved changes?')) setEditing(null); }}>Close</button>
                    <button type="button" className="curation-button curation-button-primary" disabled={busy || !dirty} onClick={() => void save()}>Save list</button>
                </div>
            </section>}
        </main>
    </div>;
}
