import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import axios from 'axios';
import { ChevronDown, ChevronLeft, ChevronRight, Search as SearchIcon, SearchX, SlidersHorizontal, X } from 'lucide-react';
import { useBorrow } from '../hooks/useBorrow';
import { INVENTORY_API_BASE_URL } from '../config/api';
import BannerSearch from '../components/layout/BannerSearch';
import PageBanner from '../components/layout/PageBanner';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import BorrowToast from '../components/BorrowToast';
import { BookListRow } from '../components/BookList/BookList';
import { BookListSkeleton, SkeletonLine } from '../components/Skeleton/Skeleton';
import type { ListBook } from '../components/BookList/BookList';
import './SearchPage.css';

interface SearchResponse {
    items: ListBook[]; total: number; page: number; pageSize: number; totalPages: number;
    facets: { genres: { name: string; count: number }[]; availableCount: number };
}
interface Genre { id: number; name: string }

const SORTS = [
    { value: 'relevance', label: 'Relevance' },
    { value: 'title', label: 'Title A–Z' },
    { value: 'author', label: 'Author A–Z' },
    { value: 'newest', label: 'Newest first' },
];
const sameName = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

// Page numbers to show: always the first and last, the current one and its neighbours, with gaps marked.
const pageNumbers = (current: number, total: number): (number | 'gap')[] => {
    const wanted = new Set([1, total, current - 1, current, current + 1].filter(page => page >= 1 && page <= total));
    const pages = [...wanted].sort((a, b) => a - b);
    return pages.flatMap((page, index) => index > 0 && page - pages[index - 1] > 1 ? ['gap' as const, page] : [page]);
};

const SearchPage = () => {
    // Everything that defines the search lives in the address, so results can be bookmarked, shared and stepped back through.
    const [params, setParams] = useSearchParams();
    const query = (params.get('q') ?? '').trim();
    const genreKey = params.getAll('genre').join('\n');
    const selectedGenres = useMemo(() => genreKey ? genreKey.split('\n') : [], [genreKey]);
    const availableOnly = params.get('available') === '1';
    // Relevance means nothing without words to match, so browsing defaults to newest.
    const requestedSort = params.get('sort');
    const sort = requestedSort && SORTS.some(option => option.value === requestedSort) && (query || requestedSort !== 'relevance')
        ? requestedSort : (query ? 'relevance' : 'newest');
    const page = Math.max(1, Number(params.get('page')) || 1);

    const [data, setData] = useState<SearchResponse | null>(null);
    const [allGenres, setAllGenres] = useState<string[]>([]);
    const [genresLoaded, setGenresLoaded] = useState(false);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [reload, setReload] = useState(0);
    const [genresOpen, setGenresOpen] = useState(true);
    const [filtersOpen, setFiltersOpen] = useState(false);
    const borrowing = useBorrow(() => setReload(value => value + 1));

    useEffect(() => {
        axios.get<Genre[]>(`${INVENTORY_API_BASE_URL}/api/genres`)
            .then(response => setAllGenres(response.data.map(genre => genre.name)))
            .catch(() => { /* The checklist falls back to the categories found in the results. */ })
            .finally(() => setGenresLoaded(true));
    }, []);

    useEffect(() => {
        const controller = new AbortController();
        const search = new URLSearchParams();
        if (query) search.set('q', query);
        selectedGenres.forEach(genre => search.append('genres', genre));
        if (availableOnly) search.set('availableOnly', 'true');
        search.set('sort', sort);
        search.set('page', String(page));
        setLoading(true);
        axios.get<SearchResponse>(`${INVENTORY_API_BASE_URL}/api/books/search?${search}`, { signal: controller.signal })
            .then(response => { setData(response.data); setError(''); })
            .catch(() => { if (!controller.signal.aborted) setError('Unable to search the catalogue. Please try again.'); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [query, selectedGenres, availableOnly, sort, page, reload]);

    // Changing a filter or the sort order starts again from the first page.
    const update = (change: (next: URLSearchParams) => void, keepPage = false) => {
        const next = new URLSearchParams(params);
        if (!keepPage) next.delete('page');
        change(next);
        setParams(next);
    };

    const toggleGenre = (genre: string) => update(next => {
        const remaining = next.getAll('genre').filter(current => !sameName(current, genre));
        const wasSelected = remaining.length !== next.getAll('genre').length;
        next.delete('genre');
        [...remaining, ...(wasSelected ? [] : [genre])].forEach(value => next.append('genre', value));
    });
    const setAvailable = (on: boolean) => update(next => { if (on) next.set('available', '1'); else next.delete('available'); });
    const clearFilters = () => update(next => { next.delete('genre'); next.delete('available'); });
    const goToPage = (target: number) => {
        update(next => { if (target > 1) next.set('page', String(target)); else next.delete('page'); }, true);
        window.scrollTo({ top: 0 });
    };

    // Every category, each with how many of the current matches it holds (0 when it has none).
    const genreOptions = useMemo(() => {
        const counts = data?.facets.genres ?? [];
        const names = [...allGenres];
        [...counts.map(facet => facet.name), ...selectedGenres].forEach(name => { if (!names.some(known => sameName(known, name))) names.push(name); });
        return names.sort((a, b) => a.localeCompare(b))
            .map(name => ({ name, count: counts.find(facet => sameName(facet.name, name))?.count ?? 0 }));
    }, [allGenres, data, selectedGenres]);

    const filterCount = selectedGenres.length + (availableOnly ? 1 : 0);
    const total = data?.total ?? 0;
    const first = data && total > 0 ? (data.page - 1) * data.pageSize + 1 : 0;
    const last = data ? first + data.items.length - 1 : 0;

    return (
        <div className="app-shell">
            <AppTopbar />
            <LibrarySidenav active="search" />
            <BorrowToast success={borrowing.success} error={borrowing.error} onDismiss={borrowing.dismiss} />
            <main className="app-main app-main-banner search-main">
                <PageBanner eyebrow="Catalogue search" title={query ? 'Search' : 'Browse All'} icon={<SearchIcon size={38} />}
                    text={query ? <>Results for <q>{query}</q></> : 'Every book in the catalogue. Search by title, author, category or ISBN.'}>
                    <BannerSearch query={query} />
                </PageBanner>

                <div className="search-toolbar">
                    <p className="search-count" role="status">
                        {error ? '' : !data ? 'Searching…' : total === 0 ? 'No books found'
                            : total <= data.pageSize ? `${total} ${total === 1 ? 'book' : 'books'}`
                            : `Showing ${first}–${last} of ${total} books`}
                    </p>
                    <label className="search-sort">Sort by
                        <select value={sort} onChange={event => update(next => next.set('sort', event.target.value))}>
                            {SORTS.filter(option => query || option.value !== 'relevance').map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
                        </select>
                    </label>
                </div>

                {filterCount > 0 && (
                    <ul className="search-chips" aria-label="Active filters">
                        {availableOnly && <li><button type="button" className="search-chip" onClick={() => setAvailable(false)} aria-label="Remove filter: available now">Available now<X size={14} /></button></li>}
                        {selectedGenres.map(genre => <li key={genre}><button type="button" className="search-chip" onClick={() => toggleGenre(genre)} aria-label={`Remove filter: ${genre}`}>{genre}<X size={14} /></button></li>)}
                        <li><button type="button" className="search-chip search-chip-clear" onClick={clearFilters}>Clear all</button></li>
                    </ul>
                )}

                {/* The list and the filter card start on the same line. */}
                <div className="search-layout">
                    <section aria-label="Search results" aria-busy={loading}>
                        {error && <p role="alert" className="search-error">{error}</p>}

                        {/* Only the very first search has nothing to show yet; later ones dim the previous results instead. */}
                        {!error && !data && <BookListSkeleton rows={8} label="Searching…" />}

                        {!error && data && total === 0 && (
                            <div className="search-empty">
                                <SearchX size={30} aria-hidden="true" />
                                <h2>{query ? <>Nothing matches “{query}”</> : 'No books match these filters'}</h2>
                                <ul>
                                    {query && <li>Check the spelling, or try fewer words.</li>}
                                    {query && <li>Search by title, author, category or ISBN.</li>}
                                    {filterCount > 0 && <li>Remove a filter to widen the search.</li>}
                                </ul>
                                {filterCount > 0 && <div><button type="button" className="shelf-borrow" onClick={clearFilters}>Clear filters</button></div>}
                            </div>
                        )}

                        {!error && data && total > 0 && (
                            <ol className={`book-list${loading ? ' book-list-stale' : ''}`} start={first}>
                                {data.items.map(book => <BookListRow key={book.id} book={book} canBorrow={borrowing.canBorrow}
                                    pending={borrowing.pending} onBorrow={bookId => void borrowing.borrow(bookId)} />)}
                            </ol>
                        )}

                        {!error && data && data.totalPages > 1 && (
                            <nav className="search-pagination" aria-label="Result pages">
                                <button type="button" disabled={data.page <= 1} onClick={() => goToPage(data.page - 1)}><ChevronLeft size={16} />Previous</button>
                                {pageNumbers(data.page, data.totalPages).map((entry, index) => entry === 'gap'
                                    ? <span key={`gap-${index}`} aria-hidden="true">…</span>
                                    : <button key={entry} type="button" aria-label={`Page ${entry}`} aria-current={entry === data.page ? 'page' : undefined}
                                        onClick={() => { if (entry !== data.page) goToPage(entry); }}>{entry}</button>)}
                                <button type="button" disabled={data.page >= data.totalPages} onClick={() => goToPage(data.page + 1)}>Next<ChevronRight size={16} /></button>
                            </nav>
                        )}
                    </section>

                    <aside className={`search-filters${filtersOpen ? '' : ' search-filters-collapsed'}`} aria-label="Filter results">
                        <div className="search-filters-heading">
                            <h2><SlidersHorizontal size={16} aria-hidden="true" />Filters{filterCount > 0 && ` (${filterCount})`}</h2>
                            {filterCount > 0 && <button type="button" className="search-link-button" onClick={clearFilters}>Clear all</button>}
                            <button type="button" className="search-link-button search-filters-toggle" aria-expanded={filtersOpen} onClick={() => setFiltersOpen(open => !open)}>{filtersOpen ? 'Hide' : 'Show'}</button>
                        </div>

                        <fieldset className="search-filter-group">
                            <legend className="visually-hidden">Availability</legend>
                            <label className="search-check">
                                <input type="checkbox" checked={availableOnly} onChange={event => setAvailable(event.target.checked)} />
                                <span>Available now</span>
                                <small>{data?.facets.availableCount ?? ''}</small>
                            </label>
                        </fieldset>

                        <div className="search-filter-group">
                            <button type="button" className="search-filter-title" aria-expanded={genresOpen} aria-controls="search-genres" onClick={() => setGenresOpen(open => !open)}>
                                Genres{selectedGenres.length > 0 && ` (${selectedGenres.length})`}<ChevronDown size={16} aria-hidden="true" />
                            </button>
                            {genresOpen && (
                                <ul className="search-filter-options" id="search-genres">
                                    {genreOptions.map(option => {
                                        const checked = selectedGenres.some(genre => sameName(genre, option.name));
                                        return (
                                            <li key={option.name}>
                                                <label className={`search-check${option.count === 0 && !checked ? ' search-check-empty' : ''}`}>
                                                    <input type="checkbox" checked={checked} onChange={() => toggleGenre(option.name)} />
                                                    <span>{option.name}</span>
                                                    <small>{option.count}</small>
                                                </label>
                                            </li>
                                        );
                                    })}
                                    {genreOptions.length === 0 && (genresLoaded && data
                                        ? <li className="search-check-empty"><small>No genres yet</small></li>
                                        : Array.from({ length: 6 }, (_, index) => <li key={index} className="search-check" aria-hidden="true"><SkeletonLine width={`${55 + (index % 3) * 12}%`} /></li>))}
                                </ul>
                            )}
                        </div>
                    </aside>
                </div>
            </main>
        </div>
    );
};

export default SearchPage;
