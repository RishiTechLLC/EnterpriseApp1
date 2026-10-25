-- Everytime script: runs on every migration, so it must be idempotent.
-- The API connects as a member of this role (least privilege: data access only, no DDL).
--   Azure SQL with managed identity:  CREATE USER [app-schoolcalendar-api] FROM EXTERNAL PROVIDER;
--                                     ALTER ROLE schoolcalendar_app ADD MEMBER [app-schoolcalendar-api];
IF DATABASE_PRINCIPAL_ID(N'schoolcalendar_app') IS NULL
    CREATE ROLE schoolcalendar_app AUTHORIZATION dbo;

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO schoolcalendar_app;
GRANT EXECUTE ON SCHEMA::dbo TO schoolcalendar_app;
