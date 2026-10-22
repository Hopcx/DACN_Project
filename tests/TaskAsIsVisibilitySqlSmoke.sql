SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'ProjectDACN_Task04Task09_Test' THROW 51141, 'Wrong database', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20261022000000_AttemptVisibilityEvents')
    THROW 51142, 'Visibility migration missing', 1;

DECLARE @AttemptId uniqueidentifier = NEWID();
DECLARE @EventId uniqueidentifier = NEWID();
DECLARE @UserId uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Users WHERE LevelId = 4);
DECLARE @ExamId int, @DetailId int, @ScheduleId int;
SELECT TOP 1 @ExamId = e.Id, @DetailId = d.Id, @ScheduleId = s.Id
FROM dbo.Exams e JOIN dbo.ExamDetails d ON d.ExamId = e.Id
JOIN dbo.ExamSchedules s ON s.ExamId = e.Id;
IF @UserId IS NULL OR @ScheduleId IS NULL THROW 51143, 'Clone fixture references missing', 1;

BEGIN TRANSACTION;
INSERT dbo.ExamAttempts(Id, UserId, ExamScheduleId, ExamId, ExamDetailId,
    StartedAtUtc, DeadlineUtc, State, Revision, SnapshotJson, DraftJson, PassMark, AllowViewResult)
VALUES (@AttemptId, @UserId, @ScheduleId, @ExamId, @DetailId,
    SYSUTCDATETIME(), DATEADD(minute, 20, SYSUTCDATETIME()), N'inProgress', 0, N'{}', N'[]', 0, 0);
INSERT dbo.AttemptVisibilityEvents(AttemptId, EventId, RecordedAtUtc, IsCounted)
VALUES (@AttemptId, @EventId, SYSUTCDATETIME(), 1);
IF (SELECT COUNT(*) FROM dbo.AttemptVisibilityEvents WHERE AttemptId = @AttemptId AND IsCounted = 1) <> 1
    THROW 51144, 'Visibility count mismatch', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.AttemptVisibilityEvents'))
    THROW 51145, 'Visibility FK missing', 1;
ROLLBACK TRANSACTION;
IF EXISTS (SELECT 1 FROM dbo.ExamAttempts WHERE Id = @AttemptId) OR
   EXISTS (SELECT 1 FROM dbo.AttemptVisibilityEvents WHERE AttemptId = @AttemptId)
    THROW 51146, 'Fixture cleanup failed', 1;
SELECT N'VISIBILITY_SQL_ROLLBACK_PASS' AS Result, DB_NAME() AS DbName;
