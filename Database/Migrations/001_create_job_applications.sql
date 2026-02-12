CREATE TABLE IF NOT EXISTS job_applications (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    company TEXT NOT NULL,
    role TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('Wishlist', 'Applied', 'Interviewing', 'Offer', 'Rejected', 'Closed')),
    applied_on TEXT NOT NULL,
    follow_up_date TEXT NULL,
    job_url TEXT NULL,
    location TEXT NULL,
    notes TEXT NULL,
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_job_applications_status ON job_applications(status);
CREATE INDEX IF NOT EXISTS idx_job_applications_applied_on ON job_applications(applied_on DESC);
CREATE INDEX IF NOT EXISTS idx_job_applications_follow_up_date ON job_applications(follow_up_date);
