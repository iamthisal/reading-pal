import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, ChevronLeft, ChevronRight } from 'lucide-react';
import './PosterSlider.css';

export type Poster = {
    id: string;
    title: string;
    eyebrow?: string;
    text?: string;
    /** A full-bleed picture. Without one the poster uses its colour theme and drawn book covers. */
    image?: string;
    theme?: 'forest' | 'clay' | 'sand';
    /** Optional button: a route ("/my-borrowings"), a section ("#recommendations") or a full URL. */
    action?: { label: string; to: string };
};

type PosterSliderProps = {
    posters: Poster[];
    label?: string;
    /** Milliseconds between slides. 0 turns the automatic advance off. */
    interval?: number;
};

const PosterAction = ({ action }: { action: NonNullable<Poster['action']> }) => {
    const content = <>{action.label}<ArrowRight size={16} /></>;
    if (/^https?:\/\//.test(action.to)) {
        return <a className="poster-action" href={action.to} target="_blank" rel="noreferrer">{content}</a>;
    }
    return action.to.startsWith('#')
        ? <a className="poster-action" href={action.to}>{content}</a>
        : <Link className="poster-action" to={action.to}>{content}</Link>;
};

const PosterSlider = ({ posters, label = 'Highlights', interval = 6000 }: PosterSliderProps) => {
    const trackRef = useRef<HTMLDivElement>(null);
    const [index, setIndex] = useState(0);
    const [paused, setPaused] = useState(false);

    const goTo = useCallback((next: number) => {
        const track = trackRef.current;
        if (!track || posters.length === 0) return;
        const target = (next + posters.length) % posters.length;
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        track.scrollTo({ left: target * track.clientWidth, behavior: reduceMotion ? 'auto' : 'smooth' });
    }, [posters.length]);

    // The track is a native scroller (so swiping works); the dots follow wherever it lands.
    const syncIndex = () => {
        const track = trackRef.current;
        if (track && track.clientWidth > 0) setIndex(Math.round(track.scrollLeft / track.clientWidth));
    };

    useEffect(() => {
        if (!interval || paused || posters.length < 2) return;
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        const timer = window.setTimeout(() => goTo(index + 1), interval);
        return () => window.clearTimeout(timer);
    }, [goTo, index, interval, paused, posters.length]);

    if (posters.length === 0) return null;
    const many = posters.length > 1;

    return (
        <section className="poster-slider" aria-roledescription="carousel" aria-label={label}
            onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)}
            onFocus={() => setPaused(true)} onBlur={() => setPaused(false)}>
            <div className="poster-track" ref={trackRef} onScroll={syncIndex}>
                {posters.map((poster, position) => (
                    <article key={poster.id} className={`poster poster-${poster.theme ?? 'forest'}${poster.image ? ' poster-has-image' : ''}`}
                        aria-roledescription="slide" aria-label={`${position + 1} of ${posters.length}`}
                        style={poster.image ? { backgroundImage: `url(${poster.image})` } : undefined}>
                        <div className="poster-copy">
                            {poster.eyebrow && <p className="poster-eyebrow">{poster.eyebrow}</p>}
                            <h2>{poster.title}</h2>
                            {poster.text && <p className="poster-text">{poster.text}</p>}
                            {poster.action && <PosterAction action={poster.action} />}
                        </div>
                        {!poster.image && (
                            <div className="poster-art" aria-hidden="true">
                                <span /><span /><span /><span />
                            </div>
                        )}
                    </article>
                ))}
            </div>
            {many && (
                <>
                    <button type="button" className="poster-arrow poster-arrow-prev" aria-label="Previous poster" onClick={() => goTo(index - 1)}><ChevronLeft size={20} /></button>
                    <button type="button" className="poster-arrow poster-arrow-next" aria-label="Next poster" onClick={() => goTo(index + 1)}><ChevronRight size={20} /></button>
                    <div className="poster-dots">
                        {posters.map((poster, position) => (
                            <button key={poster.id} type="button" className={`poster-dot${position === index ? ' poster-dot-active' : ''}`}
                                aria-label={`Show poster ${position + 1}: ${poster.title}`} aria-current={position === index ? 'true' : undefined}
                                onClick={() => goTo(position)} />
                        ))}
                    </div>
                </>
            )}
        </section>
    );
};

export default PosterSlider;
