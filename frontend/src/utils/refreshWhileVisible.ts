// Keep lists current across tabs/users without overlapping requests or polling hidden pages.
export function refreshWhileVisible(refresh: () => Promise<void>) {
    let running = false;
    let stopped = false;
    const run = async () => {
        if (stopped || running || document.hidden) return;
        running = true;
        try { await refresh(); } finally { running = false; }
    };
    const timer = window.setInterval(() => void run(), 10000);
    const onVisible = () => { void run(); };
    window.addEventListener('focus', onVisible);
    document.addEventListener('visibilitychange', onVisible);
    return () => {
        stopped = true;
        window.clearInterval(timer);
        window.removeEventListener('focus', onVisible);
        document.removeEventListener('visibilitychange', onVisible);
    };
}
