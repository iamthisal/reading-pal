import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search } from 'lucide-react';

// The search box inside a page banner. Submitting opens the Search page with what was typed;
// an empty box browses the whole catalogue.
const BannerSearch = ({ query = '' }: { query?: string }) => {
    const navigate = useNavigate();
    const [text, setText] = useState(query);
    // Follow the page when its query changes (back button, a new search), without fighting the reader's typing.
    const [lastQuery, setLastQuery] = useState(query);
    if (query !== lastQuery) {
        setLastQuery(query);
        setText(query);
    }

    const submit = (event: FormEvent) => {
        event.preventDefault();
        const trimmed = text.trim();
        navigate(trimmed ? `/search?q=${encodeURIComponent(trimmed)}` : '/search');
    };

    return (
        <form className="discover-search-panel discover-search-panel-single" role="search" onSubmit={submit}>
            <div className="discover-search-input">
                <Search size={16} />
                <input type="search" value={text} onChange={event => setText(event.target.value)}
                    placeholder="Find the book you like..." aria-label="Search the catalogue" />
            </div>
            <button type="submit" className="discover-search-button">Search</button>
        </form>
    );
};

export default BannerSearch;
