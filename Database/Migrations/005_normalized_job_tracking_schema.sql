PRAGMA foreign_keys = ON;

-- Companies master table
CREATE TABLE IF NOT EXISTS companies (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    website_url TEXT NULL,
    linkedin_url TEXT NULL,
    industry TEXT NULL,
    location TEXT NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Prevent duplicate company rows (case-insensitive)
CREATE UNIQUE INDEX IF NOT EXISTS ux_companies_name_ci
    ON companies (lower(trim(name)));

-- Core applications table, normalized by company_id
CREATE TABLE IF NOT EXISTS applications (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    company_id INTEGER NOT NULL,
    title TEXT NOT NULL,
    posting_url TEXT NULL,
    application_url TEXT NULL,
    status TEXT NOT NULL CHECK (status IN ('Wishlist', 'Applied', 'Interviewing', 'Offer', 'Rejected', 'Ghosted', 'Closed')),
    source TEXT NULL,
    applied_on TEXT NOT NULL,
    salary_text TEXT NULL,
    location TEXT NULL,
    notes TEXT NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE RESTRICT
);

-- Prevent logical duplicates: company + title + posting_url
-- Coalesce posting_url so NULL and '' are treated the same.
CREATE UNIQUE INDEX IF NOT EXISTS ux_applications_company_title_posting_ci
    ON applications (
        company_id,
        lower(trim(title)),
        lower(trim(COALESCE(posting_url, '')))
    );

-- Search/index strategy for company/title/status lookups
CREATE INDEX IF NOT EXISTS idx_applications_status ON applications(status);
CREATE INDEX IF NOT EXISTS idx_applications_company_id ON applications(company_id);
CREATE INDEX IF NOT EXISTS idx_applications_title_ci ON applications(lower(trim(title)));
CREATE INDEX IF NOT EXISTS idx_applications_status_company_title
    ON applications(status, company_id, lower(trim(title)));

-- Contacts at a company
CREATE TABLE IF NOT EXISTS contacts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    company_id INTEGER NOT NULL,
    first_name TEXT NOT NULL,
    last_name TEXT NULL,
    role_title TEXT NULL,
    email TEXT NULL,
    phone TEXT NULL,
    linkedin_url TEXT NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_contacts_company_email_ci
    ON contacts(company_id, lower(trim(email)))
    WHERE email IS NOT NULL AND trim(email) <> '';
CREATE INDEX IF NOT EXISTS idx_contacts_company_id ON contacts(company_id);

-- Optional bridge for many-to-many relationship between applications and contacts
CREATE TABLE IF NOT EXISTS application_contacts (
    application_id INTEGER NOT NULL,
    contact_id INTEGER NOT NULL,
    relationship_type TEXT NOT NULL DEFAULT 'Recruiter',
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (application_id, contact_id),
    FOREIGN KEY (application_id) REFERENCES applications(id) ON DELETE CASCADE,
    FOREIGN KEY (contact_id) REFERENCES contacts(id) ON DELETE CASCADE
);

-- Interview lifecycle per application
CREATE TABLE IF NOT EXISTS interviews (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    application_id INTEGER NOT NULL,
    interview_type TEXT NOT NULL CHECK (interview_type IN ('Recruiter Screen', 'Phone Screen', 'Technical', 'System Design', 'Behavioral', 'Onsite', 'Take Home', 'Final Round', 'Other')),
    round_number INTEGER NOT NULL DEFAULT 1 CHECK (round_number > 0),
    scheduled_at_utc TEXT NULL,
    duration_minutes INTEGER NULL CHECK (duration_minutes IS NULL OR duration_minutes > 0),
    result TEXT NULL CHECK (result IS NULL OR result IN ('Pending', 'Passed', 'Failed', 'Canceled', 'No Show')),
    notes TEXT NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (application_id) REFERENCES applications(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_interviews_application_id ON interviews(application_id);
CREATE INDEX IF NOT EXISTS idx_interviews_scheduled_at_utc ON interviews(scheduled_at_utc);

-- Tasks/reminders (follow-ups, prep tasks, deadlines)
CREATE TABLE IF NOT EXISTS tasks_reminders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    application_id INTEGER NOT NULL,
    task_type TEXT NOT NULL CHECK (task_type IN ('Follow Up', 'Prep', 'Document', 'Deadline', 'Networking', 'Other')),
    title TEXT NOT NULL,
    due_at_utc TEXT NULL,
    completed_at_utc TEXT NULL,
    priority TEXT NOT NULL DEFAULT 'Medium' CHECK (priority IN ('Low', 'Medium', 'High')),
    status TEXT NOT NULL DEFAULT 'Open' CHECK (status IN ('Open', 'In Progress', 'Done', 'Canceled')),
    notes TEXT NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (application_id) REFERENCES applications(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_tasks_reminders_application_id ON tasks_reminders(application_id);
CREATE INDEX IF NOT EXISTS idx_tasks_reminders_due_status ON tasks_reminders(due_at_utc, status);

-- Communication history for auditability and context
CREATE TABLE IF NOT EXISTS communication_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    application_id INTEGER NOT NULL,
    contact_id INTEGER NULL,
    channel TEXT NOT NULL CHECK (channel IN ('Email', 'LinkedIn', 'Phone', 'SMS', 'Portal', 'In Person', 'Other')),
    direction TEXT NOT NULL CHECK (direction IN ('Inbound', 'Outbound')),
    occurred_at_utc TEXT NOT NULL,
    subject TEXT NULL,
    summary TEXT NOT NULL,
    sentiment TEXT NULL CHECK (sentiment IS NULL OR sentiment IN ('Positive', 'Neutral', 'Negative')),
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (application_id) REFERENCES applications(id) ON DELETE CASCADE,
    FOREIGN KEY (contact_id) REFERENCES contacts(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_communication_logs_application_time
    ON communication_logs(application_id, occurred_at_utc DESC);
CREATE INDEX IF NOT EXISTS idx_communication_logs_contact_id ON communication_logs(contact_id);

-- Attachment metadata for resume/cover-letter versions and other files
CREATE TABLE IF NOT EXISTS attachments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    application_id INTEGER NOT NULL,
    attachment_type TEXT NOT NULL CHECK (attachment_type IN ('Resume', 'Cover Letter', 'Portfolio', 'Other')),
    version_label TEXT NOT NULL,
    file_name TEXT NOT NULL,
    file_path TEXT NOT NULL,
    file_hash_sha256 TEXT NULL,
    mime_type TEXT NULL,
    uploaded_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (application_id) REFERENCES applications(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_attachments_application_id ON attachments(application_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_attachments_app_type_version
    ON attachments(application_id, attachment_type, lower(trim(version_label)));

-- Optional backfill from legacy denormalized table (best-effort, idempotent)
INSERT OR IGNORE INTO companies (name, location)
SELECT DISTINCT company, location
FROM job_applications
WHERE company IS NOT NULL AND trim(company) <> '';

INSERT OR IGNORE INTO applications (
    company_id,
    title,
    posting_url,
    application_url,
    status,
    source,
    applied_on,
    salary_text,
    location,
    notes,
    created_at_utc,
    updated_at_utc
)
SELECT
    c.id,
    ja.role,
    ja.source_url,
    COALESCE(ja.application_link, ja.job_url),
    CASE
        WHEN ja.status IN ('Wishlist', 'Applied', 'Interviewing', 'Offer', 'Rejected', 'Ghosted', 'Closed') THEN ja.status
        WHEN ja.status = 'Interviewed' THEN 'Interviewing'
        ELSE 'Applied'
    END AS status,
    'LegacyImport',
    ja.applied_on,
    ja.salary_text,
    ja.location,
    ja.notes,
    ja.created_at_utc,
    ja.updated_at_utc
FROM job_applications ja
JOIN companies c ON lower(trim(c.name)) = lower(trim(ja.company));
