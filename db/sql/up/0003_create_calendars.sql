-- One calendar per program per academic year (e.g. "High School 2026-2027").
CREATE TABLE dbo.Calendars
(
    Id           UNIQUEIDENTIFIER NOT NULL,
    ProgramId    UNIQUEIDENTIFIER NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    AcademicYear NVARCHAR(9)      NOT NULL,
    StartDate    DATE             NOT NULL,
    EndDate      DATE             NOT NULL,
    Description  NVARCHAR(2000)   NULL,
    Status       NVARCHAR(20)     NOT NULL,
    PublishedAt  DATETIME2        NULL,
    CreatedAt    DATETIME2        NOT NULL,
    UpdatedAt    DATETIME2        NULL,
    CONSTRAINT PK_Calendars PRIMARY KEY (Id),
    CONSTRAINT FK_Calendars_Programs_ProgramId FOREIGN KEY (ProgramId) REFERENCES dbo.Programs (Id) ON DELETE CASCADE,
    CONSTRAINT CK_Calendars_DateRange CHECK (EndDate >= StartDate),
    CONSTRAINT CK_Calendars_AcademicYear CHECK (AcademicYear LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
    -- Values are the names of the CalendarStatus enum (stored as strings by EF Core).
    CONSTRAINT CK_Calendars_Status CHECK (Status IN (N'Draft', N'Published'))
);

CREATE INDEX IX_Calendars_ProgramId_AcademicYear ON dbo.Calendars (ProgramId, AcademicYear);
