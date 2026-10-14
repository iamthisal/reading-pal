import { useBorrowStatus } from '../contexts/BorrowStatusContext';

type BorrowButtonProps = {
    book: { id: number; title: string; availableCopies: number };
    /** Whether this reader may borrow at all (signed in and approved). */
    canBorrow: boolean;
    /** True while this book's request is on its way. */
    busy: boolean;
    onBorrow: (bookId: number) => void;
    className?: string;
};

// The one Borrow button used on every page. It reads:
//   Borrow       a copy is on the shelf and the reader can ask for it
//   Borrowing…   the request is being sent
//   Pending      the reader has asked; an admin has not accepted yet
//   Borrowed     the reader has a copy now
//   Borrow (greyed out) nothing is on the shelf right now
const BorrowButton = ({ book, canBorrow, busy, onBorrow, className = 'shelf-borrow' }: BorrowButtonProps) => {
    const { stateOf } = useBorrowStatus();
    const state = stateOf(book.id);

    if (state === 'borrowed') {
        return <button type="button" className={`${className} shelf-borrow-done`} disabled aria-label={`You have borrowed ${book.title}`}>Borrowed</button>;
    }
    if (state === 'pending') {
        return <button type="button" className={`${className} shelf-borrow-pending`} disabled aria-label={`Your request for ${book.title} is waiting for approval`}
            title="Waiting for a librarian to accept your request">Pending</button>;
    }
    if (!canBorrow) return null;
    if (book.availableCopies <= 0) {
        return <button type="button" className={`${className} shelf-borrow-unavailable`} disabled aria-label={`${book.title} has no copies on the shelf`}
            title="No copies on the shelf right now">Borrow</button>;
    }
    return (
        <button type="button" className={className} disabled={busy} aria-label={`Borrow ${book.title}`} onClick={() => onBorrow(book.id)}>
            {busy ? 'Borrowing…' : 'Borrow'}
        </button>
    );
};

export default BorrowButton;
