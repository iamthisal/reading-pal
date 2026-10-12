import AdminSidebar from '../components/layout/AdminSidebar';
import NotificationBell from '../components/NotificationBell';
import NotificationList from '../components/NotificationList';

const AdminNotificationsPage = () => {

    return <div className="admin-shell">
        <AdminSidebar active="notifications" />
        <main className="admin-main admin-users-main">
            <header className="admin-topbar admin-users-topbar">
                <div>
                    <p className="admin-eyebrow">Library operations</p>
                    <h1>Notifications</h1>
                    <p className="admin-users-subtitle">Customer reservation activity and other admins’ catalogue changes, newest first. Your read status is your own.</p>
                </div>
                <div className="admin-topbar-actions"><NotificationBell /></div>
            </header>
            <NotificationList />
        </main>
    </div>;
};

export default AdminNotificationsPage;
