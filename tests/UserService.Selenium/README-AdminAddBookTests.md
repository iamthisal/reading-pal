Admin Add Book Selenium tests

What I added
- `AdminAddBookSelenium.cs` - Selenium UI tests that exercise the "Admin: Add new book" story acceptance criteria.

Test types covered
- UI / End-to-end tests using Selenium ChromeDriver

Prerequisites
- Frontend dev server running at `http://localhost:5173` (Vite)
- Inventory service backend running and accessible at the configured `INVENTORY_API_BASE_URL` (ensure frontend config points to it)
- Chrome browser installed and compatible ChromeDriver available on PATH (or use the Selenium.WebDriver.ChromeDriver NuGet if desired)

How to run (recommended branch workflow)

1. Create a feature branch locally before committing these changes:

```bash
git checkout -b test/inventory-admin-addbook
git add tests/UserService.Selenium/AdminAddBookSelenium.cs
git add tests/UserService.Selenium/README-AdminAddBookTests.md
git commit -m "tests: add Selenium UI tests for Admin Add Book story"
```

2. Start the required services:

```bash
# In one terminal: start frontend
cd frontend
npm install   # if not done
npm run dev

# In another terminal: start inventory service (example)
cd services/inventory-service
dotnet run
```

3. Run the Selenium tests:

```bash
cd tests/UserService.Selenium
dotnet test
```

Notes / Limitations
- I could not create a separate git branch from this agent — please run the branch commands above locally before committing.
- These tests assume the admin account `admin@library.com` / `adminpassword` exists and that the front-end and API are reachable at the default local addresses used by the project.
- Running Selenium tests requires a display for Chrome or headless mode in CI. `WebDriverHelper` already enables headless in CI when `CI=true`.

If you'd like, I can also:
- Add a dedicated test project `InventoryService.Selenium` instead of adding to `UserService.Selenium`.
- Add test data seeding helpers so the Selenium tests can run against a clean environment.
