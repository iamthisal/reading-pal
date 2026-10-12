// Where a book or an author lives in the app. Authors are addressed by name because books store their author as text.
export const bookPath = (bookId: number) => `/books/${bookId}`;
export const authorPath = (name: string) => `/authors/${encodeURIComponent(name.trim())}`;
