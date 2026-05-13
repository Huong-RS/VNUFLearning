IF COL_LENGTH('dbo.Exams', 'SourceFileUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Exams ADD SourceFileUrl nvarchar(500) NULL;
END;

IF COL_LENGTH('dbo.Exams', 'SourceFileObjectName') IS NULL
BEGIN
    ALTER TABLE dbo.Exams ADD SourceFileObjectName nvarchar(500) NULL;
END;

IF COL_LENGTH('dbo.Exams', 'SourceFileName') IS NULL
BEGIN
    ALTER TABLE dbo.Exams ADD SourceFileName nvarchar(255) NULL;
END;

IF COL_LENGTH('dbo.Exams', 'SourceFileType') IS NULL
BEGIN
    ALTER TABLE dbo.Exams ADD SourceFileType nvarchar(50) NULL;
END;

IF COL_LENGTH('dbo.Exams', 'SourceFileSize') IS NULL
BEGIN
    ALTER TABLE dbo.Exams ADD SourceFileSize bigint NULL;
END;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260513000000_AddExamSourceFileMetadata'
   )
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260513000000_AddExamSourceFileMetadata', N'8.0.0');
END;
