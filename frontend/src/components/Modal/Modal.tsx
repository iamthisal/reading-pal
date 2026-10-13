import { useEffect, useId, useRef } from 'react';
import type { MouseEvent, ReactNode } from 'react';
import { X } from 'lucide-react';
import './Modal.css';

type ModalProps = {
    title: string;
    /** A line under the title saying what the form is for. */
    description?: ReactNode;
    onClose: () => void;
    /** "wide" suits forms with several columns; "large" suits side-by-side editors. */
    size?: 'default' | 'wide' | 'large';
    /** While a save is in flight the dialog cannot be dismissed. */
    busy?: boolean;
    /** Return false to keep the dialog open (for example when there are unsaved changes). */
    confirmClose?: () => boolean;
    children: ReactNode;
};

// A form floating in the middle of the screen. It uses the browser's own <dialog>, which keeps
// keyboard focus inside, closes on Escape and makes the page behind it inert. Clicking the dimmed
// area closes it too. Put the buttons in a <div className="modal-actions"> at the end.
const Modal = ({ title, description, onClose, size = 'default', busy = false, confirmClose, children }: ModalProps) => {
    const dialogRef = useRef<HTMLDialogElement>(null);
    const titleId = useId();

    useEffect(() => {
        const dialog = dialogRef.current;
        if (!dialog) return;
        if (!dialog.open) dialog.showModal();
        // Opening a dialog puts focus on its first control, which is the close button; a form should start in its first field.
        dialog.querySelector<HTMLElement>('.modal-body :is(input, select, textarea):not(:disabled)')?.focus();
        // The page behind should not scroll while a dialog is up.
        const previous = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        return () => {
            document.body.style.overflow = previous;
            if (dialog.open) dialog.close();
        };
    }, []);

    const requestClose = () => {
        if (busy) return;
        if (confirmClose && !confirmClose()) return;
        onClose();
    };

    // The dialog element covers the whole screen; a click that lands on it (not on the card) is on the backdrop.
    const closeOnBackdrop = (event: MouseEvent<HTMLDialogElement>) => {
        if (event.target === dialogRef.current) requestClose();
    };

    return (
        <dialog ref={dialogRef} className="modal" aria-labelledby={titleId} onMouseDown={closeOnBackdrop}
            onCancel={event => { event.preventDefault(); requestClose(); }}>
            <div className={`modal-card modal-card-${size}`}>
                <header className="modal-header">
                    <div>
                        <h2 id={titleId}>{title}</h2>
                        {description && <p>{description}</p>}
                    </div>
                    <button type="button" className="modal-close" aria-label="Close" title="Close" disabled={busy} onClick={requestClose}><X size={18} /></button>
                </header>
                <div className="modal-body">{children}</div>
            </div>
        </dialog>
    );
};

export default Modal;
