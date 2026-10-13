import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import './AppShell.css';

export type AppNavItem = {
    label: string;
    icon: ReactNode;
    /** A route ("/notifications") or a section on the current page ("#recommendations"). */
    to: string;
    active?: boolean;
};

type AppSidenavProps = {
    items: AppNavItem[];
    label?: string;
    /** Extra panel content under the menu, such as the catalogue filters. */
    children?: ReactNode;
};

const AppSidenav = ({ items, label = 'Main navigation', children }: AppSidenavProps) => (
    <aside className="app-sidenav">
        <nav className="app-nav" aria-label={label}>
            {items.map(item => {
                const className = `app-nav-item${item.active ? ' app-nav-item-active' : ''}`;
                const current = item.active ? 'page' as const : undefined;
                return item.to.startsWith('#')
                    ? <a key={item.label} href={item.to} className={className} aria-current={current}>{item.icon}{item.label}</a>
                    : <Link key={item.label} to={item.to} className={className} aria-current={current}>{item.icon}{item.label}</Link>;
            })}
        </nav>
        {children}
    </aside>
);

export default AppSidenav;
