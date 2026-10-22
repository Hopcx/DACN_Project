SET XACT_ABORT ON;
IF DB_NAME() <> N'ProjectDACN_Task04Task09_Test' THROW 51131, 'Wrong database for As-is visibility migration', 1;
IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) <> 8 OR
   (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory) <> N'20261019090000_Task12LegacyProjection'
   THROW 51132, 'Unexpected migration baseline', 1;
IF OBJECT_ID(N'dbo.AttemptVisibilityEvents') IS NOT NULL THROW 51133, 'Visibility table already exists', 1;
BEGIN TRANSACTION;
CREATE TABLE dbo.AttemptVisibilityEvents (
    AttemptId uniqueidentifier NOT NULL,
    EventId uniqueidentifier NOT NULL,
    RecordedAtUtc datetime2 NOT NULL,
    IsCounted bit NOT NULL,
    CONSTRAINT PK_AttemptVisibilityEvents PRIMARY KEY (AttemptId, EventId),
    CONSTRAINT FK_AttemptVisibilityEvents_ExamAttempts_AttemptId
        FOREIGN KEY (AttemptId) REFERENCES dbo.ExamAttempts(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_AttemptVisibilityEvents_AttemptId_RecordedAtUtc
    ON dbo.AttemptVisibilityEvents(AttemptId, RecordedAtUtc);
INSERT dbo.__EFMigrationsHistory(MigrationId, ProductVersion)
VALUES (N'20261022000000_AttemptVisibilityEvents', N'8.0.14');
COMMIT TRANSACTION;
SELECT DB_NAME() AS DbName, COUNT(*) AS VisibilityRows FROM dbo.AttemptVisibilityEvents;
