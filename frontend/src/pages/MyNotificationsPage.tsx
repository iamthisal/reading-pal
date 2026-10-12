import { Bell } from 'lucide-react';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import NotificationList from '../components/NotificationList';

const MyNotificationsPage = () => {
    return <div className="app-shell">
        <AppTopbar />
        <LibrarySidenav active="notifications" />
        <main className="app-main discover-main profile-main">
            <section className="profile-hero">
                <div><p className="discover-eyebrow">Stay up to date</p><h1>My notifications</h1><p>Reservation updates, reminders, fines and new books, newest first.</p></div>
                <div className="profile-hero-mark" aria-hidden="true"><Bell size={38} /></div>
            </section>
            <NotificationList />
        </main>
    </div>;
};

export default MyNotificationsPage;
