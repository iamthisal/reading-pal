import { useEffect, useRef, useState } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import './RecommendedBooks.css';

export type RecommendedBook = {
    id: number;
    title: string;
    author: string;
    genre: string;
    coverImageUrl?: string | null;
    totalCopies: number;
    availableCopies: number;
};

type RecommendedBooksProps = {
    books: RecommendedBook[];
    title?: string;
};

const Cover = ({ book }: { book: RecommendedBook }) => {
    const [failed, setFailed] = useState(false);
    return book.coverImageUrl && !failed
        ? <img className="recommended-cover" src={book.coverImageUrl} alt={`${book.title} cover`} loading="lazy" onError={() => setFailed(true)} />
        : <div className="recommended-cover recommended-cover-fallback" aria-hidden="true"><span>{book.title}</span></div>;
};

// A shelf of book cards shown a page at a time. It only moves when the reader uses the arrows (or swipes).
const RecommendedBooks = ({ books, title = 'Recommended for you' }: RecommendedBooksProps) => {
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
    }, [books]);

    const move = (direction: 1 | -1) => {
        const track = trackRef.current;
        if (!track) return;
        const gap = parseFloat(getComputedStyle(track).columnGap) || 0;
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        track.scrollBy({ left: direction * (track.clientWidth + gap), behavior: reduceMotion ? 'auto' : 'smooth' });
    };

    if (books.length === 0) return null;

    return (
        <section className="recommended" aria-label={title}>
            <header className="recommended-header">
                <h2>{title}</h2>
                {/* Nothing to page through when every card already fits. */}
                {!(atStart && atEnd) && <div className="recommended-arrows">
                    <button type="button" aria-label="Previous recommended books" disabled={atStart} onClick={() => move(-1)}><ChevronLeft size={18} /></button>
                    <button type="button" aria-label="More recommended books" disabled={atEnd} onClick={() => move(1)}><ChevronRight size={18} /></button>
                </div>}
            </header>
            <div className="recommended-track" ref={trackRef} onScroll={syncArrows}>
                {books.map(book => {
                    const available = book.availableCopies > 0;
                    return (
                        <article key={book.id} className="recommended-card">
                            <Cover book={book} />
                            <div className="recommended-info">
                                <h3>{book.title}</h3>
                                <p className="recommended-byline">{book.author}{book.genre && <><span aria-hidden="true"> • </span>{book.genre}</>}</p>
                                <span className={`recommended-status${available ? '' : ' recommended-status-out'}`}>{available ? 'Available' : 'Not available now'}</span>
                                <p className="recommended-copies">{book.availableCopies} of {book.totalCopies} {book.totalCopies === 1 ? 'copy' : 'copies'} on the shelf</p>
                            </div>
                        </article>
                    );
                })}
            </div>
        </section>
    );
};

export default RecommendedBooks;
