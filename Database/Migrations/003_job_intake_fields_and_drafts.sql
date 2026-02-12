ALTER TABLE job_applications ADD COLUMN application_link TEXT NULL;
ALTER TABLE job_applications ADD COLUMN job_level TEXT NULL;
ALTER TABLE job_applications ADD COLUMN salary_text TEXT NULL;
ALTER TABLE job_applications ADD COLUMN key_skills_json TEXT NULL;
ALTER TABLE job_applications ADD COLUMN source_url TEXT NULL;

CREATE TABLE IF NOT EXISTS job_posting_drafts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source_url TEXT NULL,
    raw_text TEXT NOT NULL,
    extracted_json TEXT NOT NULL,
    saved_application_id INTEGER NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    saved_at_utc TEXT NULL,
    FOREIGN KEY(saved_application_id) REFERENCES job_applications(id)
);

CREATE INDEX IF NOT EXISTS idx_job_posting_drafts_saved_application_id
    ON job_posting_drafts(saved_application_id);
