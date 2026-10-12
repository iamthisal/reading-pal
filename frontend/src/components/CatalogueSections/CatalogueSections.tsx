import { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import { INVENTORY_API_BASE_URL } from '../../config/api';
import { catalogueSections, POPULAR_AUTHORS_LIST } from '../../config/catalogueSections';
import type { CatalogueSection } from '../../config/catalogueSections';
import { AuthorShelf, BookShelf } from '../BookShelf/BookShelf';
import type { ShelfAuthor, ShelfBook } from '../BookShelf/BookShelf';

type ListResponse = { name: string; itemType: 'Books' | 'Authors'; books: ShelfBook[]; authors: ShelfAuthor[] };
type Named = { id: number; name: string };
type LoadedSection = { id: string; title: string; books?: ShelfBook[]; authors?: ShelfAuthor[] };

const api = `${INVENTORY_API_BASE_URL}/api`;
const sameName = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

const loadList = async (key: string, limit?: number) =>
    (await axios.get<ListResponse>(`${api}/lists/${encodeURIComponent(key)}`, { params: limit ? { limit } : undefined })).data;

const loadSection = async (section: CatalogueSection, index: number): Promise<LoadedSection | null> => {
    const id = `${section.kind}-${index}`;

    if (section.kind === 'list') {
        const list = await loadList(section.key, section.limit);
        const title = section.title ?? list.name;
        return list.itemType === 'Authors' ? { id, title, authors: list.authors } : { id, title, books: list.books };
    }

    if (section.kind === 'author') {
        const wanted = section.name;
        const author = wanted
            ? (await axios.get<Named[]>(`${api}/authors`)).data.find(entry => sameName(entry.name, wanted))
            : (await loadList(POPULAR_AUTHORS_LIST, 1)).authors[0];
        if (!author) return null;
        const books = (await axios.get<ShelfBook[]>(`${api}/authors/${author.id}/books`, { params: { limit: section.limit } })).data;
        return { id, title: section.title ?? `Books by ${author.name}`, books };
    }

    const genres = (await axios.get<Named[]>(`${api}/genres`)).data;
    const wanted = section.name;
    // With no category named, try them in order until one has books (a handful at most).
    const candidates = wanted ? genres.filter(genre => sameName(genre.name, wanted)) : genres.slice(0, 5);
    for (const genre of candidates) {
        const books = (await axios.get<ShelfBook[]>(`${api}/genres/${genre.id}/books`, { params: { limit: section.limit } })).data;
        if (books.length > 0) return { id, title: section.title ?? genre.name, books };
    }
    return null;
};

type CatalogueSectionsProps = {
    /** The page's own, regularly refreshed books: used so copies and availability on the shelves stay current. */
    liveBooks?: ShelfBook[];
    onSelectAuthor?: (name: string) => void;
};

// The curated shelves on the catalogue page: lists, an author and a category, as set in config/catalogueSections.
const CatalogueSections = ({ liveBooks, onSelectAuthor }: CatalogueSectionsProps) => {
    const [sections, setSections] = useState<LoadedSection[]>([]);

    useEffect(() => {
        let cancelled = false;
        // One section failing (a missing list, say) must not take the others down with it.
        void Promise.all(catalogueSections.map((section, index) => loadSection(section, index).catch(() => null)))
            .then(loaded => { if (!cancelled) setSections(loaded.filter((section): section is LoadedSection => section !== null)); });
        return () => { cancelled = true; };
    }, []);

    const live = useMemo(() => new Map((liveBooks ?? []).map(book => [book.id, book])), [liveBooks]);

    return (
        <>
            {sections.map(section => section.authors
                ? <AuthorShelf key={section.id} title={section.title} authors={section.authors} onSelect={author => onSelectAuthor?.(author.name)} />
                : <BookShelf key={section.id} title={section.title} books={(section.books ?? []).map(book => live.get(book.id) ?? book)} />)}
        </>
    );
};

export default CatalogueSections;
