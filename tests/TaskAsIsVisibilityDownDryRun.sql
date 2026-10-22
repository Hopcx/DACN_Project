SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'ProjectDACN_Task04Task09_Test' THROW 51191, 'Wrong database', 1;
IF OBJECT_ID(N'dbo.AttemptVisibilityEvents') IS NULL THROW 51192, 'Visibility table missing', 1;
IF EXISTS (SELECT 1 FROM dbo.AttemptVisibilityEvents) THROW 51193, 'Visibility data must be cleared before Down', 1;
BEGIN TRANSACTION;
DROP TABLE dbo.AttemptVisibilityEvents;
DELETE FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261022000000_AttemptVisibilityEvents';
IF OBJECT_ID(N'dbo.AttemptVisibilityEvents') IS NOT NULL THROW 51194, 'Down did not remove table', 1;
ROLLBACK TRANSACTION;
IF OBJECT_ID(N'dbo.AttemptVisibilityEvents') IS NULL OR
   NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261022000000_AttemptVisibilityEvents')
   THROW 51195, 'Down dry-run rollback failed', 1;
SELECT N'VISIBILITY_DOWN_DRY_RUN_PASS' AS Result, DB_NAME() AS DbName;
