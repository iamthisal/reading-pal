import { useState } from 'react';
import type { CSSProperties, ImgHTMLAttributes, ReactNode } from 'react';
import '../BookShelf/BookShelf.css';
import '../BookList/BookList.css';
import './Skeleton.css';

// Loading placeholders. Each one mirrors the shape of what it stands in for, so the page does not
// jump when the real content arrives. They are decoration: screen readers get one "Loading…" line instead.

type LineProps = { width?: string; className?: string; style?: CSSProperties };
export const SkeletonLine = ({ width = '100%', className = '', style }: LineProps) =>
    <span className={`skeleton skeleton-line ${className}`} style={{ width, ...style }} />;

const Announce = ({ label }: { label: string }) => <span className="visually-hidden" role="status">{label}</span>;

type LazyImageProps = Omit<ImgHTMLAttributes<HTMLImageElement>, 'onLoad' | 'onError'> & {
    /** Shown instead when there is no picture or it fails to load. */
    fallback: ReactNode;
};

// A picture that shows a placeholder of its own size until it has downloaded.
export const LazyImage = ({ src, className = '', fallback, alt = '', ...rest }: LazyImageProps) => {
    const [state, setState] = useState<'loading' | 'loaded' | 'failed'>('loading');
    // A different picture in the same slot starts over.
    const [shownSrc, setShownSrc] = useState(src);
    if (src !== shownSrc) {
        setShownSrc(src);
        setState('loading');
    }

    if (!src || state === 'failed') return <>{fallback}</>;
    return (
        <img {...rest} src={src} alt={alt} className={`${className}${state === 'loading' ? ' skeleton' : ''}`}
            // Pictures already in the browser's cache are complete before React attaches onLoad.
            ref={image => { if (image?.complete && image.naturalWidth > 0 && state === 'loading') setState('loaded'); }}
            onLoad={() => setState('loaded')} onError={() => setState('failed')} />
    );
};

type ShelfSkeletonProps = { variant: 'detail' | 'cover' | 'author'; count?: number };

export const ShelfSkeleton = ({ variant, count = variant === 'cover' ? 8 : 6 }: ShelfSkeletonProps) => (
    <section className="shelf" aria-hidden="true">
        <header className="shelf-header"><span className="skeleton skeleton-title" /></header>
        <div className={`shelf-track shelf-track-${variant} skeleton-track`}>
            {Array.from({ length: count }, (_, index) => variant === 'cover' ? (
                <div key={index} className="shelf-tile">
                    <span className="skeleton skeleton-cover" />
                    <SkeletonLine width="85%" />
                    <SkeletonLine width="55%" />
                </div>
            ) : (
                <div key={index} className="shelf-card">
                    <span className={`skeleton ${variant === 'author' ? 'skeleton-circle' : 'skeleton-cover'}`} />
                    <div className="skeleton-fill">
                        <SkeletonLine width="80%" />
                        <SkeletonLine width="50%" />
                        {variant === 'detail' && <SkeletonLine width="65%" />}
                    </div>
                </div>
            ))}
        </div>
    </section>
);

export const BookListSkeleton = ({ rows = 6, label = 'Loading books…' }: { rows?: number; label?: string }) => (
    <>
        <Announce label={label} />
        <ul className="book-list" aria-hidden="true">
            {Array.from({ length: rows }, (_, index) => (
                <li key={index} className="book-list-row">
                    <span className="skeleton skeleton-cover" />
                    <div className="skeleton-fill">
                        <SkeletonLine width="45%" style={{ height: '1.05rem' }} />
                        <SkeletonLine width="25%" />
                        <SkeletonLine width="35%" />
                    </div>
                    <span className="skeleton skeleton-pill" />
                </li>
            ))}
        </ul>
    </>
);

export const BookGridSkeleton = ({ count = 10 }: { count?: number }) => (
    <>
        <Announce label="Loading books…" />
        <div className="discover-book-row" aria-hidden="true">
            {Array.from({ length: count }, (_, index) => (
                <div key={index} className="discover-featured-book">
                    <span className="skeleton skeleton-cover" style={{ display: 'block' }} />
                    <SkeletonLine width="85%" style={{ marginTop: '0.9rem' }} />
                    <SkeletonLine width="55%" />
                </div>
            ))}
        </div>
    </>
);

// The header card of a book page (a cover) or an author page (a round portrait).
export const DetailSkeleton = ({ kind }: { kind: 'book' | 'author' }) => (
    <>
        <Announce label={kind === 'book' ? 'Loading book…' : 'Loading author…'} />
        <div className={`detail-card${kind === 'author' ? ' detail-author' : ''}`} aria-hidden="true">
            <span className={`skeleton ${kind === 'book' ? 'skeleton-cover' : 'skeleton-circle'}`} />
            <div className="skeleton-fill">
                <SkeletonLine width="90px" />
                <SkeletonLine width="min(420px, 70%)" style={{ height: '2.2rem', marginTop: '0.8rem' }} />
                <SkeletonLine width="180px" style={{ marginTop: '0.8rem' }} />
                {kind === 'book' && <span className="skeleton skeleton-pill" style={{ display: 'block', margin: '1.4rem 0' }} />}
                <SkeletonLine width="min(620px, 95%)" style={{ marginTop: '1.4rem' }} />
                <SkeletonLine width="min(560px, 85%)" />
                {kind === 'book' && <SkeletonLine width="min(380px, 60%)" />}
            </div>
        </div>
        <ShelfSkeleton variant="cover" count={6} />
    </>
);
