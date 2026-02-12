INSERT INTO job_applications (company, role, status, applied_on, follow_up_date, job_url, location, notes)
SELECT
    'Stripe',
    'Software Engineer, Backend',
    'Interviewing',
    date('now', '-14 day'),
    date('now', '+2 day'),
    'https://jobs.example.com/stripe-backend',
    'Remote (US)',
    'Recruiter screen completed. System design round next week.'
WHERE NOT EXISTS (SELECT 1 FROM job_applications);

INSERT INTO job_applications (company, role, status, applied_on, follow_up_date, job_url, location, notes)
SELECT
    'Notion',
    'Frontend Engineer',
    'Applied',
    date('now', '-7 day'),
    date('now', '+1 day'),
    'https://jobs.example.com/notion-frontend',
    'San Francisco, CA',
    'Applied with referral from former teammate.'
WHERE EXISTS (SELECT 1 FROM job_applications WHERE company = 'Stripe')
  AND NOT EXISTS (SELECT 1 FROM job_applications WHERE company = 'Notion');

INSERT INTO job_applications (company, role, status, applied_on, follow_up_date, job_url, location, notes)
SELECT
    'GitHub',
    'Senior Full Stack Engineer',
    'Offer',
    date('now', '-23 day'),
    NULL,
    'https://jobs.example.com/github-fullstack',
    'Remote',
    'Offer received. Review compensation package by Friday.'
WHERE EXISTS (SELECT 1 FROM job_applications WHERE company = 'Stripe')
  AND NOT EXISTS (SELECT 1 FROM job_applications WHERE company = 'GitHub');
