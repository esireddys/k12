-- Run once per environment, signed in to the k12 database as the admin (SSMS: Microsoft Entra MFA). Safe to run again.
--   k12dev-api-id   the API's managed identity: reads and writes data, cannot change the schema.
--   k12-github-dev  the GitHub Actions deploy identity: also changes the schema, to apply migrations.
-- For prod, replace them with k12prod-api-id and k12-github-prod.

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'k12dev-api-id')
    CREATE USER [k12dev-api-id] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [k12dev-api-id];
ALTER ROLE db_datawriter ADD MEMBER [k12dev-api-id];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'k12-github-dev')
    CREATE USER [k12-github-dev] FROM EXTERNAL PROVIDER;
ALTER ROLE db_ddladmin ADD MEMBER [k12-github-dev];
ALTER ROLE db_datareader ADD MEMBER [k12-github-dev];
ALTER ROLE db_datawriter ADD MEMBER [k12-github-dev];
GO
