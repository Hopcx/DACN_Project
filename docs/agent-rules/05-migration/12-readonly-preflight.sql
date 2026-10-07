-- Task 12: run only on an authorized SQL Server test copy; read-only inventory.
SELECT DB_NAME() AS DatabaseName, @@VERSION AS ServerVersion;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;

SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS SqlType,
       c.is_nullable AS IsNullable, c.max_length AS MaxLength
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.name IN (N'Exams', N'ExamSchedules', N'ClassExamSchedule', N'Submissions')
ORDER BY t.name, c.column_id;

SELECT fk.name AS ForeignKeyName, OBJECT_NAME(fk.parent_object_id) AS ChildTable,
       OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) IN (N'Exams', N'ExamSchedules', N'ClassExamSchedule', N'Submissions')
ORDER BY ChildTable, ForeignKeyName;

IF OBJECT_ID(N'dbo.Exams', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.ExamSchedules', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ExamSchedules', N'ExamId') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Exams', N'ExamScheduleId') IS NOT NULL
        EXEC(N'SELECT N''legacy'' AS RelationSchema,
             (SELECT COUNT_BIG(*) FROM dbo.Exams) AS ExamCount,
             (SELECT COUNT_BIG(*) FROM dbo.ExamSchedules) AS ScheduleCount,
             (SELECT COUNT_BIG(*) FROM dbo.Exams WHERE ExamScheduleId IS NOT NULL) AS ExamsWithScheduleFk,
             (SELECT COUNT_BIG(*) FROM dbo.ExamSchedules s LEFT JOIN dbo.Exams e ON e.Id = s.ExamId WHERE e.Id IS NULL) AS MissingScheduleExamId,
             (SELECT COUNT_BIG(*) FROM dbo.ExamSchedules s JOIN dbo.Exams e ON e.Id = s.ExamId
              WHERE e.ExamScheduleId IS NOT NULL AND e.ExamScheduleId <> s.Id) AS ConflictingLinks,
             (SELECT COUNT_BIG(*) FROM dbo.Exams e LEFT JOIN dbo.ExamSchedules s ON s.Id = e.ExamScheduleId
              WHERE e.ExamScheduleId IS NOT NULL AND (s.Id IS NULL OR s.ExamId <> e.Id)) AS ReverseLinkMismatch;');
    ELSE
        SELECT N'target' AS RelationSchema,
               (SELECT COUNT_BIG(*) FROM dbo.Exams) AS ExamCount,
               (SELECT COUNT_BIG(*) FROM dbo.ExamSchedules) AS ScheduleCount,
               (SELECT COUNT_BIG(*) FROM dbo.ExamSchedules s LEFT JOIN dbo.Exams e ON e.Id = s.ExamId WHERE e.Id IS NULL) AS MissingScheduleExamId;
END;
IF OBJECT_ID(N'dbo.ClassExamSchedule', N'U') IS NOT NULL
    SELECT COUNT_BIG(*) AS AssignmentCount FROM dbo.ClassExamSchedule;
IF OBJECT_ID(N'dbo.Submissions', N'U') IS NOT NULL
    SELECT COUNT_BIG(*) AS SubmissionCount FROM dbo.Submissions;
IF OBJECT_ID(N'dbo.ExamSchedules', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.DoingExams', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Submissions', N'U') IS NOT NULL
BEGIN
    SELECT s.Id AS ScheduleId, s.ExamId, s.StartTime, s.EndTime,
           (SELECT COUNT_BIG(*) FROM dbo.DoingExams d WHERE d.ExamScheduleId = s.Id) AS AttemptCount,
           (SELECT COUNT_BIG(*) FROM dbo.Submissions sub WHERE sub.ExamScheduleId = s.Id) AS SubmissionCount
    FROM dbo.ExamSchedules s
    WHERE EXISTS (SELECT 1 FROM dbo.DoingExams d WHERE d.ExamScheduleId = s.Id)
       OR EXISTS (SELECT 1 FROM dbo.Submissions sub WHERE sub.ExamScheduleId = s.Id)
    ORDER BY s.Id;
END;
