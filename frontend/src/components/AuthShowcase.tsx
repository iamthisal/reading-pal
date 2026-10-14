import { BookOpen, Sparkles } from 'lucide-react';

// The illustrated panel beside the sign-in and registration forms, so both pages look like one place.
const AuthShowcase = () => (
    <section className="auth-showcase">
        <div className="auth-showcase-topline">
            <BookOpen size={22} />
            <span>Reading Pal</span>
        </div>
        <div className="auth-showcase-copy">
            <p className="auth-eyebrow">A quieter place to read</p>
            <h1>Make room for a good story.</h1>
            <p>Keep your library close, discover your next favorite book, and return to the pages that stay with you.</p>
        </div>
        <div className="auth-reading-object" aria-hidden="true">
            <span className="auth-book-cover auth-book-cover-back" />
            <span className="auth-book-cover auth-book-cover-front" />
            <span className="auth-book-spine" />
            <span className="auth-book-page auth-book-page-left" />
            <span className="auth-book-page auth-book-page-right" />
            <span className="auth-book-page auth-book-page-turn" />
        </div>
        <span className="auth-ink-bottle" aria-hidden="true"><i /></span>
        <span className="auth-pen" aria-hidden="true" />
        <div className="auth-book-stack" aria-hidden="true">
            <span className="auth-book auth-book-one">READ</span>
            <span className="auth-book auth-book-two">WANDER</span>
            <span className="auth-book auth-book-three">NOTES</span>
        </div>
        <div className="auth-showcase-footer">
            <Sparkles size={16} />
            <span>Your reading life, in one place.</span>
        </div>
    </section>
);

export default AuthShowcase;
