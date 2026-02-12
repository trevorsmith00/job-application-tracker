# Job Application Tracker

Local web app for tracking applications with a status pipeline, reminders, search/filter, and job-posting extraction with review-before-save.

## Tech
- ASP.NET Core 8 API + static React UI (CDN React)
- SQLite (`Database/job-tracker.db`)
- SQL file migrations auto-applied at startup

## Run
1. `dotnet restore`
2. `dotnet run --project JobApplicationTracker.csproj`
3. Open the local URL shown in terminal (typically `https://localhost:xxxx`)

## Key Features
- CRUD: `GET/POST/PUT/DELETE /api/applications`
- Pipeline statuses: `Wishlist`, `Applied`, `Interviewing`, `Offer`, `Rejected`, `Ghosted`, `Closed`
- Reminder filter: follow-up due today
- Search/filter UI by text + status
- Job intake:
  - `POST /api/job-intake/extract` with URL or pasted text
  - Extracts: company, title, location, level, salary, key skills, application link
  - Stores raw text + extracted JSON in `job_posting_drafts`
  - Review/edit screen in UI before save
  - `POST /api/job-intake/drafts/{draftId}/save` to create application
- Ghosting automation:
  - `POST /api/applications/ghosting-sweep?inactivityDays=14`
  - Finds `Applied`/`Interviewing` (and legacy `Interviewed`) apps inactive for 14+ days
  - Moves them to `Ghosted`
  - Stores a polite final follow-up draft + recommendation in `application_follow_up_drafts`

## Database
- `Database/Migrations/001_create_job_applications.sql`
- `Database/Migrations/002_seed_job_applications.sql`
- `Database/Migrations/003_job_intake_fields_and_drafts.sql`
- `Database/Migrations/004_add_ghosted_and_follow_up_drafts.sql`
- `Database/Migrations/005_normalized_job_tracking_schema.sql`

Migrations are tracked in `schema_migrations`.

## Normalized Schema (v005)
`005_normalized_job_tracking_schema.sql` introduces these entities:
- `companies`
- `applications`
- `contacts`
- `application_contacts` (bridge)
- `interviews`
- `tasks_reminders`
- `communication_logs`
- `attachments`

Key constraints and indexes:
- Duplicate prevention:
  - `ux_companies_name_ci` on `companies(lower(trim(name)))`
  - `ux_applications_company_title_posting_ci` on `(company_id, lower(trim(title)), lower(trim(COALESCE(posting_url, ''))))`
- Fast lookup/search:
  - `idx_applications_status`
  - `idx_applications_company_id`
  - `idx_applications_title_ci`
  - `idx_applications_status_company_title`
- Additional operational indexes on interviews, reminders/tasks, communications, and attachments.

## Logging and Validation
- URL/text validation in intake endpoints
- Status + URL validation in applications endpoints
- Structured logging in extraction, draft save, and repository CRUD operations
