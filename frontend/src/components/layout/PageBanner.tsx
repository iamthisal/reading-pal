import type { ReactNode } from 'react';
import './AppShell.css';

type PageBannerProps = {
    eyebrow: string;
    title: string;
    text?: ReactNode;
    /** The picture in the round badge on the right. */
    icon?: ReactNode;
    /** Extra content under the text, such as a count. */
    children?: ReactNode;
};

// The banner that opens a reader page. It hangs from the top bar: put it first inside a
// <main className="app-main app-main-banner">.
const PageBanner = ({ eyebrow, title, text, icon, children }: PageBannerProps) => (
    <section className="profile-hero page-banner">
        <div className="page-banner-copy">
            <p className="discover-eyebrow">{eyebrow}</p>
            <h1>{title}</h1>
            {text && <p className="page-banner-text">{text}</p>}
            {children}
        </div>
        {icon && <div className="profile-hero-mark" aria-hidden="true">{icon}</div>}
    </section>
);

export default PageBanner;
