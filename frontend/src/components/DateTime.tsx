// A date with its time on a second, quieter line. Used in tables so timestamps line up
// in a narrow column instead of wrapping wherever they happen to break.
const dateFormat = new Intl.DateTimeFormat(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
const timeFormat = new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' });

const DateTime = ({ value }: { value: string | null | undefined }) => {
    if (!value) return <>—</>;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return <>—</>;
    return (
        <time className="date-time" dateTime={value} title={date.toLocaleString()}>
            <span>{dateFormat.format(date)}</span>
            <small>{timeFormat.format(date)}</small>
        </time>
    );
};

export default DateTime;
