import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import './BookShelf.css';

export type ShelfBook = {
    id: number;
    title: string;
    author: string;
    genre: string;
    coverImageUrl?: string | null;
    totalCopies: number;
    availableCopies: number;
};

export type ShelfAuthor = {
    id: number;
    name: string;
    bookCount: number;
};

/** What the reader can do with a book on a shelf. Leave it out for a read-only shelf. */
export type BorrowControls = {
    /** Whether this reader is allowed to borrow at all (signed in and approved). */
    canBorrow: boolean;
    /** IDs with a request in flight, so their buttons show progress and cannot be pressed twice. */
    pending: Record<number, boolean>;
    onBorrow: (bookId: number) => void;
};

type ShelfProps = {
    title: string;
    /** Sizes the cards: wide detailed cards or taller cover-led ones. */
    variant: 'detail' | 'cover' | 'author';
    /** Changes whenever the cards do, so the arrows are re-checked. */
    itemCount: number;
    children: ReactNode;
};

// A titled row of cards shown a page at a time. It only moves when the reader uses the arrows (or swipes).
const Shelf = ({ title, variant, itemCount, children }: ShelfProps) => {
    const trackRef = useRef<HTMLDivElement>(null);
    const [atStart, setAtStart] = useState(true);
    const [atEnd, setAtEnd] = useState(false);

    const syncArrows = () => {
        const track = trackRef.current;
        if (!track) return;
        setAtStart(track.scrollLeft <= 1);
        setAtEnd(track.scrollLeft + track.clientWidth >= track.scrollWidth - 1);
    };

    // The number of cards per page depends on the shelf's width, so re-check whenever it or the cards change.
    useEffect(() => {
        const track = trackRef.current;
        if (!track) return;
        syncArrows();
        const observer = new ResizeObserver(syncArrows);
        observer.observe(track);
        // The cards change width when the number per page changes, even if the shelf itself does not.
        Array.from(track.children).forEach(card => observer.observe(card));
        return () => observer.disconnect();
    }, [itemCount]);

    const move = (direction: 1 | -1) => {
        const track = trackRef.current;
        if (!track) return;
        const gap = parseFloat(getComputedStyle(track).columnGap) || 0;
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        track.scrollBy({ left: direction * (track.clientWidth + gap), behavior: reduceMotion ? 'auto' : 'smooth' });
    };

    return (
        <section className="shelf" aria-label={title}>
            <header className="shelf-header">
                <h2>{title}</h2>
                {/* Nothing to page through when every card already fits. */}
                {!(atStart && atEnd) && <div className="shelf-arrows">
                    <button type="button" aria-label={`Previous in ${title}`} disabled={atStart} onClick={() => move(-1)}><ChevronLeft size={18} /></button>
                    <button type="button" aria-label={`More in ${title}`} disabled={atEnd} onClick={() => move(1)}><ChevronRight size={18} /></button>
                </div>}
            </header>
            <div className={`shelf-track shelf-track-${variant}`} ref={trackRef} onScroll={syncArrows}>
                {children}
            </div>
        </section>
    );
};

const Cover = ({ book }: { book: ShelfBook }) => {
    const [failed, setFailed] = useState(false);
    return book.coverImageUrl && !failed
        ? <img className="shelf-cover" src={book.coverImageUrl} alt={`${book.title} cover`} loading="lazy" onError={() => setFailed(true)} />
        : <div className="shelf-cover shelf-cover-fallback" aria-hidden="true"><span>{book.title}</span></div>;
};

const BorrowButton = ({ book, borrow }: { book: ShelfBook; borrow?: BorrowControls }) => {
    if (!borrow?.canBorrow || book.availableCopies <= 0) return null;
    const busy = Boolean(borrow.pending[book.id]);
    return (
        <button type="button" className="shelf-borrow" disabled={busy} onClick={() => borrow.onBorrow(book.id)}
            aria-label={`Borrow ${book.title}`}>
            {busy ? 'Borrowing…' : 'Borrow'}
        </button>
    );
};

type BookShelfProps = {
    title: string;
    books: ShelfBook[];
    /** "detail": wide cards with the cover beside the text. "cover": tall cards led by a large cover. */
    variant?: 'detail' | 'cover';
    borrow?: BorrowControls;
};

export const BookShelf = ({ title, books, variant = 'cover', borrow }: BookShelfProps) => {
    if (books.length === 0) return null;
    return (
        <Shelf title={title} variant={variant} itemCount={books.length}>
            {books.map(book => {
                const available = book.availableCopies > 0;
                const status = <span className={`shelf-status${available ? '' : ' shelf-status-out'}`}>{available ? 'Available' : 'Not available now'}</span>;
                return variant === 'detail' ? (
                    <article key={book.id} className="shelf-card">
                        <Cover book={book} />
                        <div className="shelf-info">
                            <h3>{book.title}</h3>
                            <p className="shelf-byline">{book.author}{book.genre && <><span aria-hidden="true"> • </span>{book.genre}</>}</p>
                            {status}
                            <p className="shelf-copies">{book.availableCopies} of {book.totalCopies} {book.totalCopies === 1 ? 'copy' : 'copies'} on the shelf</p>
                            <BorrowButton book={book} borrow={borrow} />
                        </div>
                    </article>
                ) : (
                    <article key={book.id} className="shelf-tile">
                        <Cover book={book} />
                        <h3>{book.title}</h3>
                        <p className="shelf-byline">{book.author}</p>
                        <div className="shelf-tile-stock">
                            <span className="shelf-copies">{book.availableCopies} of {book.totalCopies} {book.totalCopies === 1 ? 'copy' : 'copies'}</span>
                            {status}
                        </div>
                        <BorrowButton book={book} borrow={borrow} />
                    </article>
                );
            })}
        </Shelf>
    );
};

type AuthorShelfProps = {
    title: string;
    authors: ShelfAuthor[];
    onSelect?: (author: ShelfAuthor) => void;
};

export const AuthorShelf = ({ title, authors, onSelect }: AuthorShelfProps) => {
    if (authors.length === 0) return null;
    return (
        <Shelf title={title} variant="author" itemCount={authors.length}>
            {authors.map(author => (
                <button key={author.id} type="button" className="shelf-card" onClick={() => onSelect?.(author)}
                    aria-label={`Show books by ${author.name}`}>
                    <span className="shelf-author-initial" aria-hidden="true">{author.name.slice(0, 1).toUpperCase()}</span>
                    <span className="shelf-info">
                        <strong>{author.name}</strong>
                        <small>{author.bookCount} {author.bookCount === 1 ? 'book' : 'books'}</small>
                    </span>
                </button>
            ))}
        </Shelf>
    );
};
