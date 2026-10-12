type BorrowToastProps = { success: string; error: string; onDismiss: () => void };

// The outcome of pressing Borrow, shown in a corner so it is visible wherever the button was.
const BorrowToast = ({ success, error, onDismiss }: BorrowToastProps) => {
    if (!success && !error) return null;
    return (
        <div className={`discover-toast${error ? ' discover-toast-error' : ''}`} role={error ? 'alert' : 'status'}>
            <span>{error || success}</span>
            <button type="button" aria-label="Dismiss" onClick={onDismiss}>×</button>
        </div>
    );
};

export default BorrowToast;
