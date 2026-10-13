import { Bell } from 'lucide-react';
import PageBanner from '../components/layout/PageBanner';
import AppTopbar from '../components/layout/AppTopbar';
import LibrarySidenav from '../components/layout/LibrarySidenav';
import NotificationList from '../components/NotificationList';

const MyNotificationsPage = () => {
    return <div className="app-shell">
        <AppTopbar />
        <LibrarySidenav active="notifications" />
        <main className="app-main app-main-banner discover-main profile-main">
            <PageBanner eyebrow="Stay up to date" title="Notifications" icon={<Bell size={38} />}
                text="Reservation updates, reminders, fines and new books, newest first." />
            <NotificationList />
        </main>
    </div>;
};

export default MyNotificationsPage;
