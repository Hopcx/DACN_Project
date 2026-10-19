-- sqlcmd -v Mode=DRYRUN|APPLY|ROLLBACK BatchId=<guid> TargetUserA=<guid> TargetUserB=<guid>
-- Source is read only. Target is always the named disposable copy.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Mode varchar(8) = '$(Mode)', @BatchId uniqueidentifier = '$(BatchId)';
DECLARE @TargetA uniqueidentifier = '$(TargetUserA)', @TargetB uniqueidentifier = '$(TargetUserB)';
DECLARE @Marker nvarchar(100) = N'QA_F20F22_20261019';
DECLARE @SourceDb sysname = N'ProjectDACN_LegacyFixture_Test';
DECLARE @SourceMigration nvarchar(150) = N'20260628110050_2806_updaetmơi11';
IF DB_NAME() <> N'ProjectDACN_Task04Task09_Test' THROW 51200, 'Wrong target database', 1;
IF @Mode NOT IN ('DRYRUN','APPLY','ROLLBACK') THROW 51201, 'Invalid mode', 1;
IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) <> 8 OR NOT EXISTS
    (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20261019090000_Task12LegacyProjection')
    THROW 51202, 'Target migration history mismatch', 1;
IF @Mode='ROLLBACK'
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM dbo.LegacyImportBatches WHERE Id=@BatchId AND SourceSystem=N'syntheticLegacyFixture'
            AND SourceDatabase=@SourceDb AND Marker=@Marker) THROW 51212, 'Batch provenance mismatch', 1;
        DELETE a FROM dbo.LegacyAnswerSelections a JOIN dbo.LegacySubmissionRecords s ON s.Id=a.LegacySubmissionRecordId WHERE s.BatchId=@BatchId;
        DELETE FROM dbo.LegacySubmissionRecords WHERE BatchId=@BatchId;
        DELETE FROM dbo.LegacyUserMaps WHERE BatchId=@BatchId;
        DELETE FROM dbo.LegacyImportBatches WHERE Id=@BatchId;
        COMMIT TRANSACTION;
        SELECT 'ROLLED_BACK' Result, @BatchId BatchId;
        RETURN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
IF DB_ID(@SourceDb) IS NULL OR
   (SELECT COUNT(*) FROM ProjectDACN_LegacyFixture_Test.dbo.__EFMigrationsHistory) <> 1 OR NOT EXISTS
   (SELECT 1 FROM ProjectDACN_LegacyFixture_Test.dbo.__EFMigrationsHistory WHERE MigrationId=@SourceMigration)
    THROW 51203, 'Source database/history mismatch', 1;
IF @TargetA = @TargetB THROW 51204, 'Target users must differ', 1;
IF (SELECT COUNT(*) FROM dbo.Users WHERE Id IN (@TargetA,@TargetB) AND LevelId=4) <> 2
    THROW 51205, 'Two mapped target Student accounts are required', 1;

DECLARE @SourceA uniqueidentifier = (SELECT Id FROM ProjectDACN_LegacyFixture_Test.dbo.Users WHERE UserName=@Marker+N'_A');
DECLARE @SourceB uniqueidentifier = (SELECT Id FROM ProjectDACN_LegacyFixture_Test.dbo.Users WHERE UserName=@Marker+N'_B');
IF @SourceA IS NULL OR @SourceB IS NULL OR @SourceA=@SourceB THROW 51206, 'Source user mapping incomplete', 1;
IF (SELECT COUNT(*) FROM ProjectDACN_LegacyFixture_Test.dbo.Submissions WHERE Note LIKE @Marker+N'%') <> 4
    THROW 51207, 'Source submission count mismatch', 1;
IF (SELECT COUNT(*) FROM ProjectDACN_LegacyFixture_Test.dbo.AnswerSubmissions a
    JOIN ProjectDACN_LegacyFixture_Test.dbo.Submissions s ON s.Id=a.SubmissionId WHERE s.Note LIKE @Marker+N'%') <> 3
    THROW 51208, 'Source answer count mismatch', 1;

SELECT s.Id SourceId, s.UserId SourceUserId,
       CASE s.UserId WHEN @SourceA THEN @TargetA WHEN @SourceB THEN @TargetB END TargetUserId,
       d.ExamId SourceExamId, s.ExamDetailId SourceExamDetailId, s.ExamScheduleId SourceScheduleId,
       e.Name ExamName, sch.Title ScheduleTitle, s.SubmitTime SubmitTimeUnknown, s.TimeTaken,
       s.TotalMark, s.IsPassed, s.Status LegacyStatus, e.AllowViewResult, s.Answered, s.UnAnswered,
       HASHBYTES('SHA2_256', CONCAT(s.Id,N'|',s.UserId,N'|',d.ExamId,N'|',s.ExamDetailId,N'|',s.ExamScheduleId,N'|',
           e.Name,N'|',sch.Title,N'|',CONVERT(nvarchar(33),s.SubmitTime,126),N'|',CONVERT(nvarchar(20),s.TimeTaken),N'|',
           CONVERT(nvarchar(32),s.TotalMark,3),N'|',CONVERT(int,s.IsPassed),N'|',COALESCE(CONVERT(nvarchar(2),CONVERT(int,s.Status)),N'NULL'),
           N'|',CONVERT(int,e.AllowViewResult),N'|',s.Answered,N'|',s.UnAnswered)) SourceChecksum
INTO #Source
FROM ProjectDACN_LegacyFixture_Test.dbo.Submissions s
JOIN ProjectDACN_LegacyFixture_Test.dbo.ExamDetails d ON d.Id=s.ExamDetailId
JOIN ProjectDACN_LegacyFixture_Test.dbo.Exams e ON e.Id=d.ExamId
JOIN ProjectDACN_LegacyFixture_Test.dbo.ExamSchedules sch ON sch.Id=s.ExamScheduleId
WHERE s.Note LIKE @Marker+N'%';
IF (SELECT COUNT(*) FROM #Source) <> 4 OR EXISTS (SELECT 1 FROM #Source WHERE TargetUserId IS NULL)
    THROW 51209, 'Orphan or unmapped submission', 1;
IF EXISTS (SELECT 1 FROM #Source WHERE SourceExamId <> (SELECT ExamId FROM ProjectDACN_LegacyFixture_Test.dbo.ExamSchedules WHERE Id=SourceScheduleId))
    THROW 51210, 'Exam/schedule mismatch', 1;
SELECT a.Id SourceId, a.SubmissionId SourceSubmissionId, a.QuestionId SourceQuestionId, a.AnswerId SourceAnswerId,
       HASHBYTES('SHA2_256', CONCAT(a.Id,N'|',a.SubmissionId,N'|',a.QuestionId,N'|',a.AnswerId)) SourceChecksum
INTO #Answers
FROM ProjectDACN_LegacyFixture_Test.dbo.AnswerSubmissions a JOIN #Source s ON s.SourceId=a.SubmissionId
JOIN ProjectDACN_LegacyFixture_Test.dbo.Answers ans ON ans.Id=a.AnswerId AND ans.QuestionId=a.QuestionId
JOIN ProjectDACN_LegacyFixture_Test.dbo.Questions q ON q.Id=a.QuestionId;
IF (SELECT COUNT(*) FROM #Answers) <> 3 THROW 51211, 'Answer FK mismatch', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM dbo.LegacyImportBatches WHERE SourceSystem=N'syntheticLegacyFixture' AND SourceDatabase=@SourceDb
        AND Marker=@Marker AND Id<>@BatchId) THROW 51213, 'Marker imported under another batch', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.LegacyImportBatches WHERE Id=@BatchId)
    BEGIN
        INSERT dbo.LegacyImportBatches(Id,SourceSystem,SourceDatabase,SourceMigration,Marker,ImportedAtUtc)
        VALUES(@BatchId,N'syntheticLegacyFixture',@SourceDb,@SourceMigration,@Marker,SYSUTCDATETIME());
        INSERT dbo.LegacyUserMaps(BatchId,SourceUserId,TargetUserId)
        VALUES(@BatchId,@SourceA,@TargetA),(@BatchId,@SourceB,@TargetB);
        INSERT dbo.LegacySubmissionRecords(BatchId,SourceTable,SourceId,SourceUserId,TargetUserId,SourceExamId,
            SourceExamDetailId,SourceScheduleId,ExamName,ScheduleTitle,SubmitTimeUnknown,TimeTaken,TotalMark,
            IsPassed,LegacyStatus,AllowViewResult,Answered,UnAnswered,SourceChecksum)
        SELECT @BatchId,N'Submissions',SourceId,SourceUserId,TargetUserId,SourceExamId,SourceExamDetailId,
            SourceScheduleId,ExamName,ScheduleTitle,SubmitTimeUnknown,TimeTaken,TotalMark,IsPassed,LegacyStatus,
            AllowViewResult,Answered,UnAnswered,SourceChecksum FROM #Source;
        INSERT dbo.LegacyAnswerSelections(LegacySubmissionRecordId,SourceTable,SourceId,SourceQuestionId,SourceAnswerId,SourceChecksum)
        SELECT r.Id,N'AnswerSubmissions',a.SourceId,a.SourceQuestionId,a.SourceAnswerId,a.SourceChecksum
        FROM #Answers a JOIN dbo.LegacySubmissionRecords r ON r.BatchId=@BatchId AND r.SourceTable=N'Submissions' AND r.SourceId=a.SourceSubmissionId;
    END;
    IF NOT EXISTS (SELECT 1 FROM dbo.LegacyImportBatches WHERE Id=@BatchId AND SourceSystem=N'syntheticLegacyFixture'
        AND SourceDatabase=@SourceDb AND SourceMigration=@SourceMigration AND Marker=@Marker)
        THROW 51214, 'Batch provenance changed', 1;
    IF (SELECT COUNT(*) FROM dbo.LegacyUserMaps WHERE BatchId=@BatchId)<>2 OR EXISTS
        (SELECT 1 FROM dbo.LegacyUserMaps WHERE BatchId=@BatchId AND NOT
            ((SourceUserId=@SourceA AND TargetUserId=@TargetA) OR (SourceUserId=@SourceB AND TargetUserId=@TargetB)))
        THROW 51215, 'User mapping changed', 1;
    IF (SELECT COUNT(*) FROM dbo.LegacySubmissionRecords WHERE BatchId=@BatchId)<>4 OR EXISTS
        (SELECT SourceId,SourceChecksum,SourceUserId,TargetUserId,SourceExamId,SourceExamDetailId,SourceScheduleId,
            ExamName,ScheduleTitle,SubmitTimeUnknown,TimeTaken,TotalMark,IsPassed,LegacyStatus,AllowViewResult,Answered,UnAnswered FROM #Source
         EXCEPT SELECT SourceId,SourceChecksum,SourceUserId,TargetUserId,SourceExamId,SourceExamDetailId,SourceScheduleId,
            ExamName,ScheduleTitle,SubmitTimeUnknown,TimeTaken,TotalMark,IsPassed,LegacyStatus,AllowViewResult,Answered,UnAnswered
            FROM dbo.LegacySubmissionRecords WHERE BatchId=@BatchId)
        THROW 51216, 'Submission count/checksum/score/status mismatch', 1;
    IF (SELECT COUNT(*) FROM dbo.LegacyAnswerSelections a JOIN dbo.LegacySubmissionRecords r ON r.Id=a.LegacySubmissionRecordId WHERE r.BatchId=@BatchId)<>3 OR EXISTS
        (SELECT SourceId,SourceChecksum,SourceQuestionId,SourceAnswerId FROM #Answers EXCEPT
         SELECT a.SourceId,a.SourceChecksum,a.SourceQuestionId,a.SourceAnswerId FROM dbo.LegacyAnswerSelections a
         JOIN dbo.LegacySubmissionRecords r ON r.Id=a.LegacySubmissionRecordId WHERE r.BatchId=@BatchId)
        THROW 51217, 'Answer count/checksum mismatch', 1;
    IF @Mode='DRYRUN' ROLLBACK TRANSACTION ELSE COMMIT TRANSACTION;
    SELECT @Mode Result,@BatchId BatchId,4 SubmissionCount,3 AnswerCount,2 UserMapCount,
        (SELECT SUM(TotalMark) FROM #Source) StoredScoreSum;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
