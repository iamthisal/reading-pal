export const API_BASE_URL = (
    import.meta.env.VITE_API_BASE_URL || 'https://reading-pal-userservice-bwh5bfc0agcvguf6.uaenorth-01.azurewebsites.net'
).replace(/\/$/, '');

export const INVENTORY_API_BASE_URL = (
    import.meta.env.VITE_INVENTORY_API_BASE_URL || 'http://localhost:5001'
).replace(/\/$/, '');

