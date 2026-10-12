import { useLocation, useNavigate } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import './BackButton.css';

// Goes back to wherever the reader came from. Someone who opened the page directly (a shared link,
// a new tab) has nowhere to go back to, so they are sent to the catalogue instead.
const BackButton = () => {
    const navigate = useNavigate();
    const location = useLocation();
    const hasHistory = location.key !== 'default';

    return (
        <button type="button" className="back-button" onClick={() => (hasHistory ? navigate(-1) : navigate('/home'))}>
            <ArrowLeft size={16} aria-hidden="true" />{hasHistory ? 'Back' : 'Back to catalogue'}
        </button>
    );
};

export default BackButton;
