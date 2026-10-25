-- Anytime script (re-run on change). Rolls events from last year's calendar into a new one,
-- shifting dates by the difference between the two calendars' start dates (or @ShiftDays).
-- Events that would fall outside the target calendar, or already exist there (same title and start date), are skipped.
-- Returns the number of events copied.
CREATE OR ALTER PROCEDURE dbo.usp_CopyCalendarEvents
    @SourceCalendarId UNIQUEIDENTIFIER,
    @TargetCalendarId UNIQUEIDENTIFIER,
    @ShiftDays        INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @SourceStart DATE, @TargetStart DATE, @TargetEnd DATE;
    SELECT @SourceStart = StartDate FROM dbo.Calendars WHERE Id = @SourceCalendarId;
    SELECT @TargetStart = StartDate, @TargetEnd = EndDate FROM dbo.Calendars WHERE Id = @TargetCalendarId;

    IF @SourceStart IS NULL OR @TargetStart IS NULL
        THROW 50001, 'Source or target calendar does not exist.', 1;

    SET @ShiftDays = ISNULL(@ShiftDays, DATEDIFF(DAY, @SourceStart, @TargetStart));

    BEGIN TRANSACTION;

    INSERT INTO dbo.CalendarEvents (Id, CalendarId, Title, Description, Type, StartDate, EndDate, IsInstructionalDay, CreatedAt)
    SELECT NEWID(), @TargetCalendarId, src.Title, src.Description, src.Type,
           DATEADD(DAY, @ShiftDays, src.StartDate), DATEADD(DAY, @ShiftDays, src.EndDate),
           src.IsInstructionalDay, SYSUTCDATETIME()
    FROM dbo.CalendarEvents src
    WHERE src.CalendarId = @SourceCalendarId
      AND DATEADD(DAY, @ShiftDays, src.StartDate) >= @TargetStart
      AND DATEADD(DAY, @ShiftDays, src.EndDate) <= @TargetEnd
      AND NOT EXISTS (
          SELECT 1 FROM dbo.CalendarEvents existing
          WHERE existing.CalendarId = @TargetCalendarId
            AND existing.Title = src.Title
            AND existing.StartDate = DATEADD(DAY, @ShiftDays, src.StartDate)
      );

    DECLARE @Copied INT = @@ROWCOUNT;

    UPDATE dbo.Calendars SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @TargetCalendarId AND @Copied > 0;

    COMMIT TRANSACTION;

    SELECT @Copied AS EventsCopied;
END;
