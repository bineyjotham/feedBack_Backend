-- Create Users table
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Email NVARCHAR(200) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100) NULL,
    Role NVARCHAR(50) NULL DEFAULT 'User',
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastLoginAt DATETIME2 NULL
);

-- Create Feedbacks table
CREATE TABLE Feedbacks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TicketNumber NVARCHAR(50) NOT NULL UNIQUE,
    Source INT NOT NULL CHECK (Source IN (0, 1, 2)),
    Rating INT NOT NULL CHECK (Rating BETWEEN 0 AND 10),
    Comment NVARCHAR(2000) NULL,
    CustomerName NVARCHAR(200) NULL,
    CustomerEmail NVARCHAR(320) NULL,
    Institution NVARCHAR(200) NULL,
    Category NVARCHAR(100) NULL,
    PersonnelName NVARCHAR(200) NULL,
    UserAgent NVARCHAR(500) NULL,
    IpAddress NVARCHAR(45) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Status INT NOT NULL DEFAULT 0 CHECK (Status IN (0, 1, 2, 3)),
    ReviewedAt DATETIME2 NULL,
    ReviewedByUserId INT NULL,
    Notes NVARCHAR(500) NULL,
    FOREIGN KEY (ReviewedByUserId) REFERENCES Users(Id)
);

-- Create EmailQueue table
CREATE TABLE EmailQueues (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TicketNumber NVARCHAR(50) NOT NULL,
    ToEmail NVARCHAR(500) NOT NULL,
    Subject NVARCHAR(500) NOT NULL,
    HtmlBody NVARCHAR(MAX) NOT NULL,
    FromEmail NVARCHAR(500) NULL,
    ReplyTo NVARCHAR(500) NULL,
    AttemptCount INT NOT NULL DEFAULT 0,
    MaxAttempts INT NOT NULL DEFAULT 3,
    Status INT NOT NULL DEFAULT 0 CHECK (Status IN (0, 1, 2, 3)),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastAttemptAt DATETIME2 NULL,
    SentAt DATETIME2 NULL,
    ErrorMessage NVARCHAR(MAX) NULL
);

-- Create indexes for performance
CREATE INDEX IX_Feedbacks_TicketNumber ON Feedbacks(TicketNumber);
CREATE INDEX IX_Feedbacks_CustomerEmail ON Feedbacks(CustomerEmail);
CREATE INDEX IX_Feedbacks_CreatedAt ON Feedbacks(CreatedAt);
CREATE INDEX IX_Feedbacks_Source ON Feedbacks(Source);
CREATE INDEX IX_Feedbacks_Status ON Feedbacks(Status);
CREATE INDEX IX_Feedbacks_PersonnelName ON Feedbacks(PersonnelName);
CREATE INDEX IX_EmailQueues_Status ON EmailQueues(Status);
CREATE INDEX IX_EmailQueues_CreatedAt ON EmailQueues(CreatedAt);
CREATE INDEX IX_EmailQueues_StatusAttempts ON EmailQueues(Status, AttemptCount);

-- Create view for dashboard stats
CREATE OR ALTER VIEW vw_DashboardStats AS
SELECT 
    COUNT(*) as TotalFeedbacks,
    AVG(CAST(Rating AS FLOAT)) as AverageRating,
    SUM(CASE WHEN Rating >= 9 THEN 1 ELSE 0 END) as Promoters,
    SUM(CASE WHEN Rating BETWEEN 7 AND 8 THEN 1 ELSE 0 END) as Passives,
    SUM(CASE WHEN Rating <= 6 THEN 1 ELSE 0 END) as Detractors,
    CAST(
        (SUM(CASE WHEN Rating >= 9 THEN 1 ELSE 0 END) - SUM(CASE WHEN Rating <= 6 THEN 1 ELSE 0 END)) * 100.0 / NULLIF(COUNT(*), 0)
        AS INT
    ) as NPS
FROM Feedbacks;

-- Insert default admin user (password: Admin123!)
INSERT INTO Users (Email, PasswordHash, FullName, Role, IsActive)
VALUES ('admin@xdsdata.com', '$2a$11$K8.Xa5vZcJqZJqZJqZJqZuX5vZcJqZJqZJqZJqZJq', 'Administrator', 'Admin', 1);