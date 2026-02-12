CREATE TABLE IF NOT EXISTS job_applications_new (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    company TEXT NOT NULL,
    role TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('Wishlist', 'Applied', 'Interviewing', 'Offer', 'Rejected', 'Ghosted', 'Closed')),
    applied_on TEXT NOT NULL,
    follow_up_date TEXT NULL,
    job_url TEXT NULL,
    location TEXT NULL,
    notes TEXT NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    application_link TEXT NULL,
    job_level TEXT NULL,
    salary_text TEXT NULL,
    key_skills_json TEXT NULL,
    source_url TEXT NULL
);

INSERT INTO job_applications_new (
    id, company, role, status, applied_on, follow_up_date, job_url, location, notes,
    created_at_utc, updated_at_utc, application_link, job_level, salary_text, key_skills_json, source_url
)
SELECT
    id, company, role,
    CASE
        WHEN status = 'Interviewed' THEN 'Interviewing'
        ELSE status
    END AS status,
    applied_on, follow_up_date, job_url, location, notes,
    created_at_utc, updated_at_utc, application_link, job_level, salary_text, key_skills_json, source_url
FROM job_applications;

DROP TABLE job_applications;
ALTER TABLE job_applications_new RENAME TO job_applications;

CREATE INDEX IF NOT EXISTS idx_job_applications_status ON job_applications(status);
CREATE INDEX IF NOT EXISTS idx_job_applications_applied_on ON job_applications(applied_on DESC);
CREATE INDEX IF NOT EXISTS idx_job_applications_follow_up_date ON job_applications(follow_up_date);

CREATE TABLE IF NOT EXISTS application_follow_up_drafts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    application_id INTEGER NOT NULL,
    days_inactive INTEGER NOT NULL,
    draft_text TEXT NOT NULL,
    recommendation TEXT NOT NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (application_id) REFERENCES job_applications(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_application_follow_up_drafts_application_id
    ON application_follow_up_drafts(application_id);
