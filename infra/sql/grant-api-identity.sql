-- Run once per environment, signed in to the k12 database as a member of the admin group (SSMS: Microsoft Entra MFA).
-- Makes the API's managed identity a database user that can read and write data, but not change the schema
-- (migrations run from the Deploy workflow instead). Replace k12dev-api-id with k12prod-api-id for prod.
-- Safe to run again.

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'k12dev-api-id')
    CREATE USER [k12dev-api-id] FROM EXTERNAL PROVIDER;

ALTER ROLE db_datareader ADD MEMBER [k12dev-api-id];
ALTER ROLE db_datawriter ADD MEMBER [k12dev-api-id];
GO
