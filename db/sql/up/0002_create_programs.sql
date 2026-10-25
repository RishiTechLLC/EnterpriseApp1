-- A program offered by a school (Elementary, High School, IB Diploma, Adult Education, ...).
CREATE TABLE dbo.Programs
(
    Id          UNIQUEIDENTIFIER NOT NULL,
    SchoolId    UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Description NVARCHAR(1000)   NULL,
    GradeLevels NVARCHAR(100)    NULL,
    CreatedAt   DATETIME2        NOT NULL,
    UpdatedAt   DATETIME2        NULL,
    CONSTRAINT PK_Programs PRIMARY KEY (Id),
    CONSTRAINT FK_Programs_Schools_SchoolId FOREIGN KEY (SchoolId) REFERENCES dbo.Schools (Id) ON DELETE CASCADE
);

-- Program codes are unique within a school; also serves the SchoolId foreign key.
CREATE UNIQUE INDEX IX_Programs_SchoolId_Code ON dbo.Programs (SchoolId, Code);
