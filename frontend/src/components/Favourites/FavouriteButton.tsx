import { Heart } from 'lucide-react';
import { useFavourites } from '../../contexts/FavouritesContext';
import './FavouriteButton.css';

type FavouriteButtonProps = {
    bookId: number;
    title: string;
    /** "floating" sits on a white disc over a cover; "plain" is for use on a card's own background. */
    variant?: 'floating' | 'plain';
};

const FavouriteButton = ({ bookId, title, variant = 'floating' }: FavouriteButtonProps) => {
    const { isFavourite, toggle } = useFavourites();
    // Placeholder books (negative IDs) are not in the catalogue, so they cannot be saved.
    if (bookId <= 0) return null;
    const active = isFavourite(bookId);

    return (
        <button type="button" className={`favourite-button favourite-button-${variant}${active ? ' favourite-button-active' : ''}`}
            aria-pressed={active} aria-label={active ? `Remove ${title} from favourites` : `Add ${title} to favourites`}
            title={active ? 'Remove from favourites' : 'Add to favourites'} onClick={() => void toggle(bookId)}>
            <Heart size={18} fill={active ? 'currentColor' : 'none'} />
        </button>
    );
};

export default FavouriteButton;
