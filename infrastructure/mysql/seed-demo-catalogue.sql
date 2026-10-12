-- Demo catalogue for local development: REPLACES every book, category, author and curated list
-- in the inventory database with a set of real titles (covers come from Open Library).
--
-- Run it against the local inventory database only, then restart the inventory service so it
-- recreates the starter authors and lists:
--
--   docker exec -i readingpal-mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" ReadingPal_inventory < infrastructure/mysql/seed-demo-catalogue.sql
--   docker compose restart inventory-service
--
-- Rows are deleted rather than truncated so new books get fresh IDs: old reservations and loans in
-- the lending database keep pointing at IDs that no longer exist instead of at the wrong book.

SET NAMES utf8mb4;
START TRANSACTION;

DELETE FROM CuratedListItems;
DELETE FROM CuratedLists;
DELETE FROM Authors;
DELETE FROM BookCheckouts;
DELETE FROM Books;
DELETE FROM Genres;

INSERT INTO Genres (Name, CreatedAt, UpdatedAt) VALUES
  ('Classics', UTC_TIMESTAMP(), UTC_TIMESTAMP()),
  ('Fantasy', UTC_TIMESTAMP(), UTC_TIMESTAMP()),
  ('Fiction', UTC_TIMESTAMP(), UTC_TIMESTAMP()),
  ('Mystery & Thriller', UTC_TIMESTAMP(), UTC_TIMESTAMP()),
  ('Non-fiction', UTC_TIMESTAMP(), UTC_TIMESTAMP()),
  ('Science Fiction', UTC_TIMESTAMP(), UTC_TIMESTAMP()),
  ('Self-help', UTC_TIMESTAMP(), UTC_TIMESTAMP()),
  ('Technology', UTC_TIMESTAMP(), UTC_TIMESTAMP());

-- CreatedAt is staggered so 'newest first' mixes the categories. Three titles are out of stock on purpose.
INSERT INTO Books (Title, Author, ISBN, Genre, CoverImageUrl, TotalCopies, AvailableCopies, CreatedAt, UpdatedAt) VALUES
  ('Pride and Prejudice', 'Jane Austen', '9780141439518', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780141439518-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 0 MINUTE, UTC_TIMESTAMP() - INTERVAL 0 MINUTE),
  ('Emma', 'Jane Austen', '9780141439587', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780141439587-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 37 MINUTE, UTC_TIMESTAMP() - INTERVAL 37 MINUTE),
  ('Sense and Sensibility', 'Jane Austen', '9780141439662', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780141439662-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 3 MINUTE, UTC_TIMESTAMP() - INTERVAL 3 MINUTE),
  ('Persuasion', 'Jane Austen', '9780141439686', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780141439686-L.jpg?default=false', 1, 1, UTC_TIMESTAMP() - INTERVAL 40 MINUTE, UTC_TIMESTAMP() - INTERVAL 40 MINUTE),
  ('Nineteen Eighty-Four', 'George Orwell', '9780451524935', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780451524935-L.jpg?default=false', 5, 5, UTC_TIMESTAMP() - INTERVAL 6 MINUTE, UTC_TIMESTAMP() - INTERVAL 6 MINUTE),
  ('Animal Farm', 'George Orwell', '9780451526342', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780451526342-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 43 MINUTE, UTC_TIMESTAMP() - INTERVAL 43 MINUTE),
  ('To Kill a Mockingbird', 'Harper Lee', '9780061120084', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780061120084-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 9 MINUTE, UTC_TIMESTAMP() - INTERVAL 9 MINUTE),
  ('The Great Gatsby', 'F. Scott Fitzgerald', '9780743273565', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780743273565-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 46 MINUTE, UTC_TIMESTAMP() - INTERVAL 46 MINUTE),
  ('Jane Eyre', 'Charlotte Brontë', '9780141441146', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780141441146-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 12 MINUTE, UTC_TIMESTAMP() - INTERVAL 12 MINUTE),
  ('Wuthering Heights', 'Emily Brontë', '9780141439556', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780141439556-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 49 MINUTE, UTC_TIMESTAMP() - INTERVAL 49 MINUTE),
  ('The Catcher in the Rye', 'J.D. Salinger', '9780316769488', 'Classics', 'https://covers.openlibrary.org/b/isbn/9780316769488-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 15 MINUTE, UTC_TIMESTAMP() - INTERVAL 15 MINUTE),
  ('The Hobbit', 'J.R.R. Tolkien', '9780547928227', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780547928227-L.jpg?default=false', 5, 5, UTC_TIMESTAMP() - INTERVAL 52 MINUTE, UTC_TIMESTAMP() - INTERVAL 52 MINUTE),
  ('The Fellowship of the Ring', 'J.R.R. Tolkien', '9780547928210', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780547928210-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 18 MINUTE, UTC_TIMESTAMP() - INTERVAL 18 MINUTE),
  ('The Two Towers', 'J.R.R. Tolkien', '9780547928203', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780547928203-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 55 MINUTE, UTC_TIMESTAMP() - INTERVAL 55 MINUTE),
  ('The Return of the King', 'J.R.R. Tolkien', '9780547928197', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780547928197-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 21 MINUTE, UTC_TIMESTAMP() - INTERVAL 21 MINUTE),
  ('Harry Potter and the Sorcerer''s Stone', 'J.K. Rowling', '9780590353427', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780590353427-L.jpg?default=false', 6, 6, UTC_TIMESTAMP() - INTERVAL 58 MINUTE, UTC_TIMESTAMP() - INTERVAL 58 MINUTE),
  ('Harry Potter and the Chamber of Secrets', 'J.K. Rowling', '9780439064873', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780439064873-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 24 MINUTE, UTC_TIMESTAMP() - INTERVAL 24 MINUTE),
  ('Harry Potter and the Prisoner of Azkaban', 'J.K. Rowling', '9780439136365', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780439136365-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 61 MINUTE, UTC_TIMESTAMP() - INTERVAL 61 MINUTE),
  ('Harry Potter and the Goblet of Fire', 'J.K. Rowling', '9780439139601', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780439139601-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 27 MINUTE, UTC_TIMESTAMP() - INTERVAL 27 MINUTE),
  ('A Game of Thrones', 'George R.R. Martin', '9780553593716', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780553593716-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 64 MINUTE, UTC_TIMESTAMP() - INTERVAL 64 MINUTE),
  ('The Name of the Wind', 'Patrick Rothfuss', '9780756404741', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780756404741-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 30 MINUTE, UTC_TIMESTAMP() - INTERVAL 30 MINUTE),
  ('American Gods', 'Neil Gaiman', '9780062572233', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780062572233-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 67 MINUTE, UTC_TIMESTAMP() - INTERVAL 67 MINUTE),
  ('Coraline', 'Neil Gaiman', '9780380807345', 'Fantasy', 'https://covers.openlibrary.org/b/isbn/9780380807345-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 33 MINUTE, UTC_TIMESTAMP() - INTERVAL 33 MINUTE),
  ('Dune', 'Frank Herbert', '9780441172719', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780441172719-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 70 MINUTE, UTC_TIMESTAMP() - INTERVAL 70 MINUTE),
  ('Foundation', 'Isaac Asimov', '9780553293357', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780553293357-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 36 MINUTE, UTC_TIMESTAMP() - INTERVAL 36 MINUTE),
  ('I, Robot', 'Isaac Asimov', '9780553382563', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780553382563-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 2 MINUTE, UTC_TIMESTAMP() - INTERVAL 2 MINUTE),
  ('Ender''s Game', 'Orson Scott Card', '9780812550702', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780812550702-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 39 MINUTE, UTC_TIMESTAMP() - INTERVAL 39 MINUTE),
  ('The Hitchhiker''s Guide to the Galaxy', 'Douglas Adams', '9780345391803', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780345391803-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 5 MINUTE, UTC_TIMESTAMP() - INTERVAL 5 MINUTE),
  ('Fahrenheit 451', 'Ray Bradbury', '9781451673319', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9781451673319-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 42 MINUTE, UTC_TIMESTAMP() - INTERVAL 42 MINUTE),
  ('Brave New World', 'Aldous Huxley', '9780060850524', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780060850524-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 8 MINUTE, UTC_TIMESTAMP() - INTERVAL 8 MINUTE),
  ('The Martian', 'Andy Weir', '9780553418026', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780553418026-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 45 MINUTE, UTC_TIMESTAMP() - INTERVAL 45 MINUTE),
  ('Project Hail Mary', 'Andy Weir', '9780593135204', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780593135204-L.jpg?default=false', 2, 0, UTC_TIMESTAMP() - INTERVAL 11 MINUTE, UTC_TIMESTAMP() - INTERVAL 11 MINUTE),
  ('Neuromancer', 'William Gibson', '9780441569595', 'Science Fiction', 'https://covers.openlibrary.org/b/isbn/9780441569595-L.jpg?default=false', 1, 0, UTC_TIMESTAMP() - INTERVAL 48 MINUTE, UTC_TIMESTAMP() - INTERVAL 48 MINUTE),
  ('Murder on the Orient Express', 'Agatha Christie', '9780062693662', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9780062693662-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 14 MINUTE, UTC_TIMESTAMP() - INTERVAL 14 MINUTE),
  ('And Then There Were None', 'Agatha Christie', '9780062073488', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9780062073488-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 51 MINUTE, UTC_TIMESTAMP() - INTERVAL 51 MINUTE),
  ('The Murder of Roger Ackroyd', 'Agatha Christie', '9780062073563', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9780062073563-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 17 MINUTE, UTC_TIMESTAMP() - INTERVAL 17 MINUTE),
  ('Death on the Nile', 'Agatha Christie', '9780062073556', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9780062073556-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 54 MINUTE, UTC_TIMESTAMP() - INTERVAL 54 MINUTE),
  ('The Girl with the Dragon Tattoo', 'Stieg Larsson', '9780307454546', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9780307454546-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 20 MINUTE, UTC_TIMESTAMP() - INTERVAL 20 MINUTE),
  ('Gone Girl', 'Gillian Flynn', '9780307588371', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9780307588371-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 57 MINUTE, UTC_TIMESTAMP() - INTERVAL 57 MINUTE),
  ('The Da Vinci Code', 'Dan Brown', '9780307474278', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9780307474278-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 23 MINUTE, UTC_TIMESTAMP() - INTERVAL 23 MINUTE),
  ('The Silent Patient', 'Alex Michaelides', '9781250301697', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9781250301697-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 60 MINUTE, UTC_TIMESTAMP() - INTERVAL 60 MINUTE),
  ('The Shining', 'Stephen King', '9780307743657', 'Mystery & Thriller', 'https://covers.openlibrary.org/b/isbn/9780307743657-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 26 MINUTE, UTC_TIMESTAMP() - INTERVAL 26 MINUTE),
  ('Sapiens: A Brief History of Humankind', 'Yuval Noah Harari', '9780062316097', 'Non-fiction', 'https://covers.openlibrary.org/b/isbn/9780062316097-L.jpg?default=false', 4, 4, UTC_TIMESTAMP() - INTERVAL 63 MINUTE, UTC_TIMESTAMP() - INTERVAL 63 MINUTE),
  ('Homo Deus', 'Yuval Noah Harari', '9780062464316', 'Non-fiction', 'https://covers.openlibrary.org/b/isbn/9780062464316-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 29 MINUTE, UTC_TIMESTAMP() - INTERVAL 29 MINUTE),
  ('Educated', 'Tara Westover', '9780399590504', 'Non-fiction', 'https://covers.openlibrary.org/b/isbn/9780399590504-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 66 MINUTE, UTC_TIMESTAMP() - INTERVAL 66 MINUTE),
  ('Becoming', 'Michelle Obama', '9781524763138', 'Non-fiction', 'https://covers.openlibrary.org/b/isbn/9781524763138-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 32 MINUTE, UTC_TIMESTAMP() - INTERVAL 32 MINUTE),
  ('A Brief History of Time', 'Stephen Hawking', '9780553380163', 'Non-fiction', 'https://covers.openlibrary.org/b/isbn/9780553380163-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 69 MINUTE, UTC_TIMESTAMP() - INTERVAL 69 MINUTE),
  ('Thinking, Fast and Slow', 'Daniel Kahneman', '9780374533557', 'Non-fiction', 'https://covers.openlibrary.org/b/isbn/9780374533557-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 35 MINUTE, UTC_TIMESTAMP() - INTERVAL 35 MINUTE),
  ('The Immortal Life of Henrietta Lacks', 'Rebecca Skloot', '9781400052189', 'Non-fiction', 'https://covers.openlibrary.org/b/isbn/9781400052189-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 1 MINUTE, UTC_TIMESTAMP() - INTERVAL 1 MINUTE),
  ('Atomic Habits', 'James Clear', '9780735211292', 'Self-help', 'https://covers.openlibrary.org/b/isbn/9780735211292-L.jpg?default=false', 5, 5, UTC_TIMESTAMP() - INTERVAL 38 MINUTE, UTC_TIMESTAMP() - INTERVAL 38 MINUTE),
  ('The Psychology of Money', 'Morgan Housel', '9780857197689', 'Self-help', 'https://covers.openlibrary.org/b/isbn/9780857197689-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 4 MINUTE, UTC_TIMESTAMP() - INTERVAL 4 MINUTE),
  ('Deep Work', 'Cal Newport', '9781455586691', 'Self-help', 'https://covers.openlibrary.org/b/isbn/9781455586691-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 41 MINUTE, UTC_TIMESTAMP() - INTERVAL 41 MINUTE),
  ('The 7 Habits of Highly Effective People', 'Stephen R. Covey', '9781982137274', 'Self-help', 'https://covers.openlibrary.org/b/isbn/9781982137274-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 7 MINUTE, UTC_TIMESTAMP() - INTERVAL 7 MINUTE),
  ('How to Win Friends and Influence People', 'Dale Carnegie', '9780671027032', 'Self-help', 'https://covers.openlibrary.org/b/isbn/9780671027032-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 44 MINUTE, UTC_TIMESTAMP() - INTERVAL 44 MINUTE),
  ('The Power of Habit', 'Charles Duhigg', '9780812981605', 'Self-help', 'https://covers.openlibrary.org/b/isbn/9780812981605-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 10 MINUTE, UTC_TIMESTAMP() - INTERVAL 10 MINUTE),
  ('Clean Code', 'Robert C. Martin', '9780132350884', 'Technology', 'https://covers.openlibrary.org/b/isbn/9780132350884-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 47 MINUTE, UTC_TIMESTAMP() - INTERVAL 47 MINUTE),
  ('The Pragmatic Programmer', 'Andrew Hunt', '9780135957059', 'Technology', 'https://covers.openlibrary.org/b/isbn/9780135957059-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 13 MINUTE, UTC_TIMESTAMP() - INTERVAL 13 MINUTE),
  ('Design Patterns', 'Erich Gamma', '9780201633610', 'Technology', 'https://covers.openlibrary.org/b/isbn/9780201633610-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 50 MINUTE, UTC_TIMESTAMP() - INTERVAL 50 MINUTE),
  ('Refactoring', 'Martin Fowler', '9780134757599', 'Technology', 'https://covers.openlibrary.org/b/isbn/9780134757599-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 16 MINUTE, UTC_TIMESTAMP() - INTERVAL 16 MINUTE),
  ('Introduction to Algorithms', 'Thomas H. Cormen', '9780262033848', 'Technology', 'https://covers.openlibrary.org/b/isbn/9780262033848-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 53 MINUTE, UTC_TIMESTAMP() - INTERVAL 53 MINUTE),
  ('The Phoenix Project', 'Gene Kim', '9781942788294', 'Technology', 'https://covers.openlibrary.org/b/isbn/9781942788294-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 19 MINUTE, UTC_TIMESTAMP() - INTERVAL 19 MINUTE),
  ('Designing Data-Intensive Applications', 'Martin Kleppmann', '9781449373320', 'Technology', 'https://covers.openlibrary.org/b/isbn/9781449373320-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 56 MINUTE, UTC_TIMESTAMP() - INTERVAL 56 MINUTE),
  ('The Alchemist', 'Paulo Coelho', '9780062315007', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9780062315007-L.jpg?default=false', 5, 5, UTC_TIMESTAMP() - INTERVAL 22 MINUTE, UTC_TIMESTAMP() - INTERVAL 22 MINUTE),
  ('The Kite Runner', 'Khaled Hosseini', '9781594631931', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9781594631931-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 59 MINUTE, UTC_TIMESTAMP() - INTERVAL 59 MINUTE),
  ('A Thousand Splendid Suns', 'Khaled Hosseini', '9781594483851', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9781594483851-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 25 MINUTE, UTC_TIMESTAMP() - INTERVAL 25 MINUTE),
  ('Life of Pi', 'Yann Martel', '9780156027328', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9780156027328-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 62 MINUTE, UTC_TIMESTAMP() - INTERVAL 62 MINUTE),
  ('The Book Thief', 'Markus Zusak', '9780375842207', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9780375842207-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 28 MINUTE, UTC_TIMESTAMP() - INTERVAL 28 MINUTE),
  ('Normal People', 'Sally Rooney', '9781984822185', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9781984822185-L.jpg?default=false', 2, 0, UTC_TIMESTAMP() - INTERVAL 65 MINUTE, UTC_TIMESTAMP() - INTERVAL 65 MINUTE),
  ('Where the Crawdads Sing', 'Delia Owens', '9780735219090', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9780735219090-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 31 MINUTE, UTC_TIMESTAMP() - INTERVAL 31 MINUTE),
  ('The Midnight Library', 'Matt Haig', '9780525559474', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9780525559474-L.jpg?default=false', 3, 3, UTC_TIMESTAMP() - INTERVAL 68 MINUTE, UTC_TIMESTAMP() - INTERVAL 68 MINUTE),
  ('The Seven Moons of Maali Almeida', 'Shehan Karunatilaka', '9781324064824', 'Fiction', 'https://covers.openlibrary.org/b/isbn/9781324064824-L.jpg?default=false', 2, 2, UTC_TIMESTAMP() - INTERVAL 34 MINUTE, UTC_TIMESTAMP() - INTERVAL 34 MINUTE);

COMMIT;
