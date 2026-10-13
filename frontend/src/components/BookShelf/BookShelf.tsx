import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { authorPath, bookPath } from '../../utils/links';
import FavouriteButton from '../Favourites/FavouriteButton';
import { LazyImage } from '../Skeleton/Skeleton';
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
    imageUrl?: string | null;
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

export type ShelfAction = { label: string; to: string };

type ShelfProps = {
    title: string;
    /** Sizes the cards: wide detailed cards or taller cover-led ones. */
    variant: 'detail' | 'cover' | 'author';
    /** A link beside the title, such as "View all". */
    action?: ShelfAction;
    /** Changes whenever the cards do, so the arrows are re-checked. */
    itemCount: number;
    children: ReactNode;
};

// A titled row of cards shown a page at a time. It only moves when the reader uses the arrows (or swipes).
const Shelf = ({ title, variant, action, itemCount, children }: ShelfProps) => {
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
                {action && <Link to={action.to} className="shelf-action">{action.label}</Link>}
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

const Cover = ({ book }: { book: ShelfBook }) => (
    <LazyImage className="shelf-cover" src={book.coverImageUrl ?? undefined} alt={`${book.title} cover`} loading="lazy"
        fallback={<div className="shelf-cover shelf-cover-fallback" aria-hidden="true"><span>{book.title}</span></div>} />
);

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
    action?: ShelfAction;
};

export const BookShelf = ({ title, books, variant = 'cover', borrow, action }: BookShelfProps) => {
    if (books.length === 0) return null;
    return (
        <Shelf title={title} variant={variant} action={action} itemCount={books.length}>
            {books.map(book => {
                const available = book.availableCopies > 0;
                const status = <span className={`shelf-status${available ? '' : ' shelf-status-out'}`}>{available ? 'Available' : 'Not available now'}</span>;
                return variant === 'detail' ? (
                    <article key={book.id} className="shelf-card favourite-anchor">
                        <FavouriteButton bookId={book.id} title={book.title} variant="plain" />
                        <Cover book={book} />
                        <div className="shelf-info">
                            <h3><Link to={bookPath(book.id)}>{book.title}</Link></h3>
                            <p className="shelf-byline"><Link to={authorPath(book.author)}>{book.author}</Link>{book.genre && <><span aria-hidden="true"> • </span>{book.genre}</>}</p>
                            {status}
                            <p className="shelf-copies">{book.availableCopies} of {book.totalCopies} {book.totalCopies === 1 ? 'copy' : 'copies'} on the shelf</p>
                            <BorrowButton book={book} borrow={borrow} />
                        </div>
                    </article>
                ) : (
                    <article key={book.id} className="shelf-tile">
                        <div className="favourite-anchor shelf-tile-cover">
                            <Link to={bookPath(book.id)} tabIndex={-1} aria-hidden="true"><Cover book={book} /></Link>
                            <FavouriteButton bookId={book.id} title={book.title} />
                        </div>
                        <h3><Link to={bookPath(book.id)}>{book.title}</Link></h3>
                        <p className="shelf-byline"><Link to={authorPath(book.author)}>{book.author}</Link></p>
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

const AuthorPortrait = ({ author }: { author: ShelfAuthor }) => (
    <LazyImage className="shelf-author-initial shelf-author-portrait" src={author.imageUrl ?? undefined} alt="" loading="lazy"
        fallback={<span className="shelf-author-initial" aria-hidden="true">{author.name.slice(0, 1).toUpperCase()}</span>} />
);

export const AuthorShelf = ({ title, authors }: { title: string; authors: ShelfAuthor[] }) => {
    if (authors.length === 0) return null;
    return (
        <Shelf title={title} variant="author" itemCount={authors.length}>
            {authors.map(author => (
                <Link key={author.id} to={authorPath(author.name)} className="shelf-card shelf-card-link">
                    <AuthorPortrait author={author} />
                    <span className="shelf-info">
                        <strong>{author.name}</strong>
                        <small>{author.bookCount} {author.bookCount === 1 ? 'book' : 'books'}</small>
                    </span>
                </Link>
            ))}
        </Shelf>
    );
};
