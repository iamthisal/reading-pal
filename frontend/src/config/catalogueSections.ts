// The sections shown on the catalogue page, top to bottom. Edit this list to change what readers see.
// A section whose list, author or category is missing or has no books is simply not shown.
export type CatalogueSection =
    /** A curated list from the inventory service, by its key. Works for book lists and author lists. */
    | { kind: 'list'; key: string; limit?: number; title?: string }
    /** Books by one author. Leave the name out to feature the first author on the popular-authors list. */
    | { kind: 'author'; name?: string; limit?: number; title?: string }
    /** Books in one category. Leave the name out to feature the first category that has books. */
    | { kind: 'category'; name?: string; limit?: number; title?: string };

export const POPULAR_AUTHORS_LIST = 'popular-authors';

export const catalogueSections: CatalogueSection[] = [
    { kind: 'list', key: 'recommended', limit: 6 },
    { kind: 'list', key: 'top-100', limit: 12 },
    { kind: 'list', key: POPULAR_AUTHORS_LIST },
    { kind: 'author', limit: 12 },
    { kind: 'category', limit: 12 },
];
