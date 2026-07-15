-- Idempotent seed data for local/dev/QA environments — NOT for production use.
-- Safe to re-run: every row uses a fixed, deterministic id and ON CONFLICT DO NOTHING.
--
-- Every seeded user shares the password: Sup3rSecret123!
-- (password_hash below is its PBKDF2-HMAC-SHA256 hash, ASP.NET Core Identity
-- PasswordHasher<TUser> v3 format, 100k iterations, 16-byte salt — the exact format
-- Microsoft.AspNetCore.Identity.PasswordHasher<User> in this app both writes and reads,
-- so these seeded users can log in through the real /api/v1/auth/login endpoint.)
--
-- Run against the `arkcloud` database after migrations have been applied:
--   psql "Host=localhost;Port=5432;Database=arkcloud;Username=arkcloud;Password=arkcloud" -f seed_users.sql
-- (or the Npgsql-style connection string form, adjust for your local setup.)

BEGIN;

INSERT INTO users ("Id", email, password_hash, first_name, last_name, is_active, created_at, failed_login_attempts, locked_out_until)
VALUES
    ('99999999-0000-0000-0000-000000000001', 'admin1@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Alice', 'Anderson', TRUE, now(), 0, NULL), -- Admin baseline
    ('99999999-0000-0000-0000-000000000002', 'admin2@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Marc', 'Bishop', TRUE, now(), 0, NULL), -- Admin baseline
    ('99999999-0000-0000-0000-000000000003', 'admin3@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Sofia', 'Costa', TRUE, now(), 0, NULL), -- Admin + Manager (multi-role)
    ('99999999-0000-0000-0000-000000000004', 'manager1@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Bruno', 'Dubois', TRUE, now(), 0, NULL), -- Manager baseline
    ('99999999-0000-0000-0000-000000000005', 'manager2@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Chloe', 'Evans', TRUE, now(), 0, NULL), -- Manager baseline
    ('99999999-0000-0000-0000-000000000006', 'manager3@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'David', 'Fontaine', TRUE, now(), 0, NULL), -- Manager baseline
    ('99999999-0000-0000-0000-000000000007', 'manager4@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Emma', 'Girard', TRUE, now(), 0, NULL), -- Manager baseline
    ('99999999-0000-0000-0000-000000000008', 'manager5@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Farid', 'Haddad', FALSE, now(), 0, NULL), -- Manager, deactivated
    ('99999999-0000-0000-0000-000000000009', 'manager6@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Giulia', 'Ibarra', TRUE, now(), 2, NULL), -- Manager, 2 failed attempts (not locked)
    ('99999999-0000-0000-0000-000000000010', 'manager7@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Hugo', 'Jansen', TRUE, now(), 0, NULL), -- Manager + User (multi-role)
    ('99999999-0000-0000-0000-000000000011', 'user1@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Ines', 'Klein', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000012', 'user2@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Jonas', 'Lund', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000013', 'user3@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Karim', 'Moreau', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000014', 'user4@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Lena', 'Novak', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000015', 'user5@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Milo', 'Ortiz', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000016', 'user6@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Nina', 'Petit', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000017', 'user7@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Omar', 'Quinn', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000018', 'user8@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Paula', 'Reyes', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000019', 'user9@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Quentin', 'Silva', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000020', 'user10@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Rosa', 'Tanaka', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000021', 'user11@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Said', 'Ulrich', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000022', 'user12@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Tara', 'Voss', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000023', 'user13@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Umar', 'Weiss', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000024', 'user14@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Vera', 'Xu', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000025', 'user15@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Walid', 'Young', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000026', 'user16@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Xenia', 'Zimmer', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000027', 'user17@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Yanis', 'Abara', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000028', 'user18@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Zoe', 'Bouchard', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000029', 'user19@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Adam', 'Caron', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000030', 'user20@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Bella', 'Dumont', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000031', 'user21@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Cyril', 'Ebert', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000032', 'user22@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Diane', 'Fischer', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000033', 'user23@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Elias', 'Gagnon', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000034', 'user24@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Fatima', 'Hebert', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000035', 'user25@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Gaspard', 'Imre', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000036', 'user26@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Hana', 'Joly', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000037', 'user27@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Ivo', 'Kessler', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000038', 'user28@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Julia', 'Leroux', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000039', 'user29@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Karl', 'Meunier', TRUE, now(), 4, NULL), -- User, 4 failed attempts (one away from lockout)
    ('99999999-0000-0000-0000-000000000040', 'user30@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Lucie', 'Novak', TRUE, now(), 1, NULL), -- User, 1 failed attempt
    ('99999999-0000-0000-0000-000000000041', 'user31@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Marco', 'Oliveira', TRUE, now(), 3, NULL), -- User, 3 failed attempts
    ('99999999-0000-0000-0000-000000000042', 'user32@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Nadia', 'Perez', TRUE, now(), 5, now() + interval '15 minutes'), -- User, locked out (active lock)
    ('99999999-0000-0000-0000-000000000043', 'user33@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Otis', 'Quiroga', TRUE, now(), 6, now() + interval '10 minutes'), -- User, locked out (active lock)
    ('99999999-0000-0000-0000-000000000044', 'user34@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Priya', 'Roche', TRUE, now(), 5, now() - interval '1 hour'), -- User, lock EXPIRED (can log in again)
    ('99999999-0000-0000-0000-000000000045', 'user35@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Quinn', 'Santos', FALSE, now(), 0, NULL), -- User, deactivated
    ('99999999-0000-0000-0000-000000000046', 'user36@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Rania', 'Tessier', FALSE, now(), 5, now() + interval '15 minutes'), -- User, deactivated AND locked out (combined edge case)
    ('99999999-0000-0000-0000-000000000047', 'user37@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Steven', 'Udo', TRUE, now(), 0, NULL), -- User + Manager (multi-role)
    ('99999999-0000-0000-0000-000000000048', 'user38@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Talia', 'Vance', TRUE, now(), 0, NULL), -- User + Admin (multi-role)
    ('99999999-0000-0000-0000-000000000049', 'user39@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Ugo', 'Weber', TRUE, now(), 0, NULL), -- User baseline
    ('99999999-0000-0000-0000-000000000050', 'user40@arkcloud.test', 'AQAAAAEAAYagAAAAEI6MnbhLfOU2xgQ33/R6IhuT6vKsGZXmbltJLA/bcoN9M3I3lJcpg/iLjiYIrXrmUw==', 'Valentina', 'Xavier', TRUE, now(), 0, NULL) -- User baseline
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO user_roles ("UserId", "RoleId")
VALUES
    ('99999999-0000-0000-0000-000000000001', '11111111-0000-0000-0000-000000000001'),
    ('99999999-0000-0000-0000-000000000002', '11111111-0000-0000-0000-000000000001'),
    ('99999999-0000-0000-0000-000000000003', '11111111-0000-0000-0000-000000000001'),
    ('99999999-0000-0000-0000-000000000003', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000004', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000005', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000006', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000007', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000008', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000009', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000010', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000010', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000011', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000012', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000013', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000014', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000015', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000016', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000017', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000018', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000019', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000020', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000021', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000022', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000023', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000024', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000025', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000026', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000027', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000028', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000029', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000030', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000031', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000032', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000033', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000034', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000035', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000036', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000037', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000038', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000039', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000040', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000041', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000042', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000043', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000044', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000045', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000046', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000047', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000047', '11111111-0000-0000-0000-000000000002'),
    ('99999999-0000-0000-0000-000000000048', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000048', '11111111-0000-0000-0000-000000000001'),
    ('99999999-0000-0000-0000-000000000049', '11111111-0000-0000-0000-000000000003'),
    ('99999999-0000-0000-0000-000000000050', '11111111-0000-0000-0000-000000000003')
ON CONFLICT ("UserId", "RoleId") DO NOTHING;

COMMIT;
