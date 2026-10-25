-- Holidays, breaks, exams, PD days, etc. on a calendar.
CREATE TABLE dbo.CalendarEvents
(
    Id                 UNIQUEIDENTIFIER NOT NULL,
    CalendarId         UNIQUEIDENTIFIER NOT NULL,
    Title              NVARCHAR(200)    NOT NULL,
    Description        NVARCHAR(2000)   NULL,
    Type               NVARCHAR(40)     NOT NULL,
    StartDate          DATE             NOT NULL,
    EndDate            DATE             NOT NULL,
    IsInstructionalDay BIT              NOT NULL,
    CreatedAt          DATETIME2        NOT NULL,
    UpdatedAt          DATETIME2        NULL,
    CONSTRAINT PK_CalendarEvents PRIMARY KEY (Id),
    CONSTRAINT FK_CalendarEvents_Calendars_CalendarId FOREIGN KEY (CalendarId) REFERENCES dbo.Calendars (Id) ON DELETE CASCADE,
    CONSTRAINT CK_CalendarEvents_DateRange CHECK (EndDate >= StartDate),
    -- Values are the names of the CalendarEventType enum. Adding an enum value needs a new script that alters this constraint.
    CONSTRAINT CK_CalendarEvents_Type CHECK (Type IN (
        N'TermStart', N'TermEnd', N'Holiday', N'Break', N'Exam', N'ProfessionalDevelopment',
        N'EarlyDismissal', N'ParentTeacherConference', N'SchoolEvent', N'Other'))
);

CREATE INDEX IX_CalendarEvents_CalendarId_StartDate ON dbo.CalendarEvents (CalendarId, StartDate);
