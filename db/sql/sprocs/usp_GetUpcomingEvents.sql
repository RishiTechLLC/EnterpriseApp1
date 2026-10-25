-- Anytime script (re-run on change). Upcoming events across every published calendar of a school,
-- e.g. for a "this month at school" newsletter.
CREATE OR ALTER PROCEDURE dbo.usp_GetUpcomingEvents
    @SchoolId UNIQUEIDENTIFIER,
    @FromDate DATE = NULL,
    @Days     INT  = 30
AS
BEGIN
    SET NOCOUNT ON;

    SET @FromDate = ISNULL(@FromDate, CAST(SYSUTCDATETIME() AS DATE));
    DECLARE @ToDate DATE = DATEADD(DAY, @Days, @FromDate);

    SELECT ProgramName, CalendarName, EventId, Title, Description, Type, StartDate, EndDate, IsInstructionalDay
    FROM dbo.vw_CalendarEvents
    WHERE SchoolId = @SchoolId
      AND CalendarStatus = N'Published'
      AND EndDate >= @FromDate
      AND StartDate <= @ToDate
    ORDER BY StartDate, ProgramName, Title;
END;
