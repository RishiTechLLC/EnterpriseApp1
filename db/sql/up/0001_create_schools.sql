-- One-time script: never edit after it has been deployed. Add a new numbered script instead.
CREATE TABLE dbo.Schools
(
    Id          UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Address     NVARCHAR(500)    NULL,
    TimeZone    NVARCHAR(64)     NOT NULL,
    CreatedBy   NVARCHAR(128)    NOT NULL,
    CreatedAt   DATETIME2        NOT NULL,
    UpdatedAt   DATETIME2        NULL,
    CONSTRAINT PK_Schools PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_Schools_Code ON dbo.Schools (Code);
