import type { ReactNode } from 'react';
import { AlarmClock, Bell, BookCheck, BookPlus, BookX, CalendarCheck, PencilLine, Sparkles, Trash2, Wallet } from 'lucide-react';

// Shared by the header bell and the My Notifications pages.
export type NotificationItem = {
    id: number;
    type: string;
    bookTitle: string;
    message: string;
    isRead: boolean;
    createdAtUtc: string;
    // Admin notifications point to the page where the request is handled.
    link?: string;
};

const typeIcon: Record<string, ReactNode> = {
    ReservationAccepted: <BookCheck size={16} />,
    ReservationCancelled: <BookX size={16} />,
    BookReturned: <CalendarCheck size={16} />,
    NewReservation: <BookPlus size={16} />,
    CustomerCancelledReservation: <BookX size={16} />,
    FineRecorded: <Wallet size={16} />,
    DueDateReminder: <AlarmClock size={16} />,
    NewBook: <Sparkles size={16} />,
    // No link: a deleted book has no page; the saved title in the message identifies it.
    BookDeleted: <Trash2 size={16} />,
    // Catalogue changes made by another admin.
    AdminBookCreated: <BookPlus size={16} />,
    AdminBookUpdated: <PencilLine size={16} />,
    AdminBookDeleted: <Trash2 size={16} />
};

// Shown in the warning colour: something the reader should notice.
const warningTypes = new Set(['ReservationCancelled', 'CustomerCancelledReservation', 'FineRecorded', 'BookDeleted', 'AdminBookDeleted']);

export const notificationIcon = (type: string) => typeIcon[type] ?? <Bell size={16} />;

export const isWarningType = (type: string) => warningTypes.has(type);

// Text for an admin notification's link, by where it leads.
export const notificationLinkLabel = (link: string) =>
    link === '/admin/books' ? 'View book inventory →' : 'View pending reservations →';

export const notificationApiPath = (isAdmin: boolean) => `/api/${isAdmin ? 'admin/' : ''}notifications`;

// Lets the bell and the My Notifications page keep each other's unread counts current.
export const NOTIFICATIONS_CHANGED_EVENT = 'notifications:changed';
export const announceNotificationsChanged = () => window.dispatchEvent(new Event(NOTIFICATIONS_CHANGED_EVENT));
