import { Link } from 'react-router-dom';
import { authorPath, bookPath } from '../../utils/links';
import FavouriteButton from '../Favourites/FavouriteButton';
import { LazyImage } from '../Skeleton/Skeleton';
import BorrowButton from '../BorrowButton';
import '../BookShelf/BookShelf.css';
import './BookList.css';

export type ListBook = {
    id: number; title: string; author: string; isbn?: string; genre: string; coverImageUrl?: string | null;
    totalCopies: number; availableCopies: number;
};

type BookListRowProps = {
    book: ListBook;
    /** Whether this reader may borrow, which requests are in flight, and what Borrow does. */
    canBorrow: boolean;
    pending: Record<number, boolean>;
    onBorrow: (bookId: number) => void;
};

const Cover = ({ book }: { book: ListBook }) => (
    <LazyImage className="book-list-cover" src={book.coverImageUrl ?? undefined} alt={`${book.title} cover`} loading="lazy"
        fallback={<div className="book-list-cover book-list-cover-fallback" aria-hidden="true">{book.title}</div>} />
);

// One book as a row: cover, details, then Borrow and the heart. Used wherever books are shown as a list
// (search results, favourites) so they look and behave the same. Place inside an element with class "book-list".
export const BookListRow = ({ book, canBorrow, pending, onBorrow }: BookListRowProps) => {
    const available = book.availableCopies > 0;
    const busy = Boolean(pending[book.id]);
    return (
        <li className="book-list-row">
            <Cover book={book} />
            <div className="book-list-info">
                <h2><Link to={bookPath(book.id)}>{book.title}</Link></h2>
                <p className="book-list-byline"><Link to={authorPath(book.author)}>{book.author}</Link></p>
                <p className="book-list-meta">{book.genre}{book.isbn && <><span aria-hidden="true"> • </span>ISBN {book.isbn}</>}</p>
                <div className="book-list-stock">
                    <span className={`shelf-status${available ? '' : ' shelf-status-out'}`}>{available ? 'Available' : 'Not available now'}</span>
                    <span>{book.availableCopies} of {book.totalCopies} {book.totalCopies === 1 ? 'copy' : 'copies'} on the shelf</span>
                </div>
            </div>
            <div className="book-list-actions">
                <BorrowButton book={book} canBorrow={canBorrow} busy={busy} onBorrow={onBorrow} />
                <FavouriteButton bookId={book.id} title={book.title} variant="plain" />
            </div>
        </li>
    );
};
