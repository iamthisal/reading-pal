import { Bell as BellIcon } from 'lucide-react';
import AppTopbar from '../components/layout/AppTopbar';
import PageBanner from '../components/layout/PageBanner';
import AdminSidebar from '../components/layout/AdminSidebar';
import NotificationList from '../components/NotificationList';

const AdminNotificationsPage = () => {

    return <div className="app-shell">
        <AppTopbar />
        <AdminSidebar active="notifications" />
        <main className="app-main app-main-banner admin-main admin-users-main">
            <PageBanner eyebrow="Library operations" title="Notifications" icon={<BellIcon size={38} />} text={<>Customer reservation activity and other admins’ catalogue changes, newest first. Your read status is your own.</>} />
            <NotificationList />
        </main>
    </div>;
};

export default AdminNotificationsPage;
