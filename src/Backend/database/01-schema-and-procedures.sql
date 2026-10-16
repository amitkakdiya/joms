/*
  JOMS local demo schema.
  Run this script in the jomsdb database.
  If your existing OMS already has users/orders/roles tables, map the procedures to those tables
  instead of creating duplicate business tables.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.tblAppUser', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblAppUser
    (
        UserId BIGINT IDENTITY(1,1) PRIMARY KEY,
        emailId NVARCHAR(150) NULL,
        password NVARCHAR(150) NULL,
        Username NVARCHAR(100) NULL,
        PasswordHash VARCHAR(128) NULL,
        PasswordSalt VARCHAR(64) NULL,
        DisplayName NVARCHAR(150) NOT NULL,
        RoleName VARCHAR(30) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblAppUser_IsActive DEFAULT (1)
    );
END;
GO

IF COL_LENGTH('dbo.tblAppUser', 'emailId') IS NULL
    ALTER TABLE dbo.tblAppUser ADD emailId NVARCHAR(150) NULL;
GO
IF COL_LENGTH('dbo.tblAppUser', 'password') IS NULL
    ALTER TABLE dbo.tblAppUser ADD password NVARCHAR(150) NULL;
GO
IF COL_LENGTH('dbo.tblAppUser', 'Username') IS NULL
    ALTER TABLE dbo.tblAppUser ADD Username NVARCHAR(100) NULL;
GO
IF COL_LENGTH('dbo.tblAppUser', 'PasswordHash') IS NULL
    ALTER TABLE dbo.tblAppUser ADD PasswordHash VARCHAR(128) NULL;
GO
IF COL_LENGTH('dbo.tblAppUser', 'PasswordSalt') IS NULL
    ALTER TABLE dbo.tblAppUser ADD PasswordSalt VARCHAR(64) NULL;
GO
IF COL_LENGTH('dbo.tblAppUser', 'DisplayName') IS NULL
    ALTER TABLE dbo.tblAppUser ADD DisplayName NVARCHAR(150) NULL;
GO
UPDATE dbo.tblAppUser
SET Username = LEFT(LTRIM(RTRIM(emailId)), 100)
WHERE Username IS NULL AND emailId IS NOT NULL AND LTRIM(RTRIM(emailId)) <> N'';
UPDATE dbo.tblAppUser
SET DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(Username)), N''), CONCAT(N'User ', UserId))
WHERE DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = N'';
ALTER TABLE dbo.tblAppUser ALTER COLUMN DisplayName NVARCHAR(150) NOT NULL;
GO
IF COL_LENGTH('dbo.tblAppUser', 'UsernameNormalized') IS NULL
    ALTER TABLE dbo.tblAppUser ADD UsernameNormalized AS UPPER(Username) PERSISTED;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tblAppUser') AND name = 'UX_tblAppUser_UsernameNormalized')
    CREATE UNIQUE INDEX UX_tblAppUser_UsernameNormalized
        ON dbo.tblAppUser(UsernameNormalized)
        WHERE Username IS NOT NULL;
GO

IF OBJECT_ID('dbo.tblOrder', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblOrder
    (
        OrderId BIGINT IDENTITY(1,1) PRIMARY KEY,
        CustomerUserId BIGINT NOT NULL,
        Description NVARCHAR(1000) NULL,
        Status VARCHAR(40) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_tblOrder_Customer FOREIGN KEY (CustomerUserId) REFERENCES dbo.tblAppUser(UserId)
    );
END;
GO

IF OBJECT_ID('dbo.tblOrderAssignment', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblOrderAssignment
    (
        AssignmentId BIGINT IDENTITY(1,1) PRIMARY KEY,
        OrderId BIGINT NOT NULL,
        AssignedToUserId BIGINT NOT NULL,
        AssignedByUserId BIGINT NOT NULL,
        AssignedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        IsActive BIT NOT NULL DEFAULT (1),
        CONSTRAINT FK_tblOrderAssignment_Order FOREIGN KEY (OrderId) REFERENCES dbo.tblOrder(OrderId),
        CONSTRAINT FK_tblOrderAssignment_AssignedTo FOREIGN KEY (AssignedToUserId) REFERENCES dbo.tblAppUser(UserId),
        CONSTRAINT FK_tblOrderAssignment_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES dbo.tblAppUser(UserId)
    );
    CREATE INDEX IX_tblOrderAssignment_OrderActive ON dbo.tblOrderAssignment(OrderId, IsActive);
END;
GO

IF OBJECT_ID('dbo.tblNotification', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblNotification
    (
        NotificationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblNotification PRIMARY KEY,
        UserId BIGINT NOT NULL,
        OrderId BIGINT NOT NULL,
        EventType VARCHAR(50) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(500) NOT NULL,
        ActionUrl NVARCHAR(500) NULL,
        IsRead BIT NOT NULL CONSTRAINT DF_tblNotification_IsRead DEFAULT (0),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_tblNotification_CreatedAt DEFAULT SYSUTCDATETIME(),
        ReadAt DATETIME2(3) NULL,
        CONSTRAINT FK_tblNotification_User FOREIGN KEY (UserId) REFERENCES dbo.tblAppUser(UserId),
        CONSTRAINT FK_tblNotification_Order FOREIGN KEY (OrderId) REFERENCES dbo.tblOrder(OrderId)
    );
    CREATE INDEX IX_tblNotification_UserReadCreated
        ON dbo.tblNotification(UserId, IsRead, CreatedAt DESC, NotificationId DESC);
END;
GO

IF OBJECT_ID('dbo.tblNotificationOutbox', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblNotificationOutbox
    (
        OutboxId BIGINT IDENTITY(1,1) PRIMARY KEY,
        NotificationId BIGINT NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ProcessedAt DATETIME2(3) NULL,
        ClaimedUntil DATETIME2(3) NULL,
        Attempts INT NOT NULL DEFAULT (0),
        NextAttemptAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        LastError NVARCHAR(1000) NULL,
        CONSTRAINT FK_tblNotificationOutbox_Notification
            FOREIGN KEY (NotificationId) REFERENCES dbo.tblNotification(NotificationId)
    );
    CREATE INDEX IX_tblNotificationOutbox_Pending
        ON dbo.tblNotificationOutbox(NextAttemptAt, OutboxId)
        WHERE ProcessedAt IS NULL;
END;
GO

IF COL_LENGTH('dbo.tblNotificationOutbox', 'ClaimedUntil') IS NULL
    ALTER TABLE dbo.tblNotificationOutbox ADD ClaimedUntil DATETIME2(3) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Notification_Create
    @UserId BIGINT, @OrderId BIGINT, @EventType VARCHAR(50),
    @Title NVARCHAR(200), @Message NVARCHAR(500), @ActionUrl NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    INSERT dbo.tblNotification(UserId, OrderId, EventType, Title, Message, ActionUrl)
    VALUES(@UserId, @OrderId, @EventType, @Title, @Message, @ActionUrl);
    DECLARE @NotificationId BIGINT = CONVERT(BIGINT, SCOPE_IDENTITY());
    INSERT dbo.tblNotificationOutbox(NotificationId) VALUES(@NotificationId);
    COMMIT;
    SELECT NotificationId, UserId, OrderId, EventType, Title, Message, ActionUrl, IsRead, CreatedAt, ReadAt
    FROM dbo.tblNotification WHERE NotificationId = @NotificationId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Notification_Get
    @UserId BIGINT, @Page INT = 1, @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    SET @Page = CASE WHEN @Page < 1 THEN 1 ELSE @Page END;
    SET @PageSize = CASE WHEN @PageSize < 1 THEN 20 WHEN @PageSize > 100 THEN 100 ELSE @PageSize END;
    SELECT NotificationId, UserId, OrderId, EventType, Title, Message, ActionUrl, IsRead, CreatedAt, ReadAt
    FROM dbo.tblNotification WHERE UserId = @UserId
    ORDER BY CreatedAt DESC, NotificationId DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
    SELECT COUNT_BIG(1) FROM dbo.tblNotification WHERE UserId = @UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Notification_UnreadCount @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(1) FROM dbo.tblNotification WHERE UserId = @UserId AND IsRead = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Notification_MarkRead @UserId BIGINT, @NotificationId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tblNotification SET IsRead = 1, ReadAt = COALESCE(ReadAt, SYSUTCDATETIME())
    WHERE UserId = @UserId AND NotificationId = @NotificationId AND IsRead = 0;
    DECLARE @UpdatedRows INT = @@ROWCOUNT;
    IF @UpdatedRows = 0 AND EXISTS
       (SELECT 1 FROM dbo.tblNotification WHERE UserId = @UserId AND NotificationId = @NotificationId)
        SELECT 1;
    ELSE
        SELECT @UpdatedRows;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Notification_MarkAllRead @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tblNotification SET IsRead = 1, ReadAt = SYSUTCDATETIME()
    WHERE UserId = @UserId AND IsRead = 0;
    SELECT CAST(@@ROWCOUNT AS INT);
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_User_GetIdsByRole @Role VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId FROM dbo.tblAppUser WHERE RoleName = @Role AND IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_User_GetCadUsers
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, DisplayName
    FROM dbo.tblAppUser
    WHERE RoleName = 'CAD' AND IsActive = 1
    ORDER BY DisplayName, UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_User_GetProductionUsers
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, DisplayName
    FROM dbo.tblAppUser
    WHERE RoleName = 'Production' AND IsActive = 1
    ORDER BY DisplayName, UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_User_GetBillingUsers
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, DisplayName
    FROM dbo.tblAppUser
    WHERE RoleName = 'Billing' AND IsActive = 1
    ORDER BY DisplayName, UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_User_GetForAuthentication @Username NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, Username, PasswordHash, PasswordSalt, password AS LegacyPassword,
           DisplayName, RoleName
    FROM dbo.tblAppUser
    WHERE UsernameNormalized = UPPER(LTRIM(RTRIM(@Username))) AND IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_GetForUser
    @UserId BIGINT,
    @RoleName VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    IF @RoleName NOT IN ('Admin', 'Customer', 'CAD', 'Production', 'Billing')
        THROW 50014, 'Unsupported order-list role.', 1;
    SELECT o.OrderId, o.CustomerUserId, o.Description, o.Status,
           TODATETIMEOFFSET(o.CreatedAt, '+00:00') AS CreatedAt,
           TODATETIMEOFFSET(o.UpdatedAt, '+00:00') AS UpdatedAt,
           CASE WHEN assigned.RoleName = 'CAD' THEN a.AssignedToUserId END AS AssignedCadUserId,
           CASE WHEN assigned.RoleName = 'CAD' THEN assigned.DisplayName END AS AssignedCadDisplayName,
           a.AssignedToUserId AS AssignedUserId,
           assigned.DisplayName AS AssignedDisplayName,
           assigned.RoleName AS AssignedRole
    FROM dbo.tblOrder o
    LEFT JOIN dbo.tblOrderAssignment a
        ON a.OrderId = o.OrderId AND a.IsActive = 1
    LEFT JOIN dbo.tblAppUser assigned
        ON assigned.UserId = a.AssignedToUserId
    WHERE @RoleName = 'Admin'
       OR (@RoleName = 'Customer' AND o.CustomerUserId = @UserId)
       OR EXISTS
          (SELECT 1
           FROM dbo.tblOrderAssignment mine
           JOIN dbo.tblAppUser workflowUser ON workflowUser.UserId = mine.AssignedToUserId
           WHERE mine.OrderId = o.OrderId
             AND mine.IsActive = 1
             AND mine.AssignedToUserId = @UserId
             AND workflowUser.RoleName = @RoleName
             AND workflowUser.IsActive = 1)
    ORDER BY o.CreatedAt DESC, o.OrderId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_User_UpdateCredentials
    @UserId BIGINT,
    @PasswordHash VARCHAR(128),
    @PasswordSalt VARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tblAppUser
    SET PasswordHash = @PasswordHash,
        PasswordSalt = @PasswordSalt,
        password = NULL
    WHERE UserId = @UserId AND IsActive = 1;
    IF @@ROWCOUNT = 0
        THROW 50013, 'Active user was not found while upgrading credentials.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_User_CreateWithCredentials
    @Username NVARCHAR(100),
    @PasswordHash VARCHAR(128),
    @PasswordSalt VARCHAR(64),
    @DisplayName NVARCHAR(150),
    @RoleName VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Username = LTRIM(RTRIM(@Username));
    IF @Username = N'' OR @DisplayName = N''
        THROW 50011, 'Username and display name are required.', 1;
    IF @RoleName NOT IN ('Admin', 'Customer', 'CAD', 'Production', 'Billing')
        THROW 50012, 'Unsupported user role.', 1;

    BEGIN TRY
        INSERT dbo.tblAppUser(emailId, Username, PasswordHash, PasswordSalt, DisplayName, RoleName)
        VALUES(@Username, @Username, @PasswordHash, @PasswordSalt, @DisplayName, @RoleName);
        SELECT CONVERT(BIGINT, SCOPE_IDENTITY());
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() IN (2601, 2627)
            SELECT CAST(NULL AS BIGINT);
        ELSE
            THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Notification_CreateForTarget
    @UserId BIGINT = NULL,
    @RoleName VARCHAR(30) = NULL,
    @OrderId BIGINT,
    @EventType VARCHAR(50),
    @Title NVARCHAR(200),
    @Message NVARCHAR(500),
    @ActionUrl NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @UserId IS NULL AND @RoleName IS NULL
        THROW 50010, 'A notification recipient user or role is required.', 1;

    DECLARE @Created TABLE (NotificationId BIGINT NOT NULL);

    INSERT dbo.tblNotification(UserId, OrderId, EventType, Title, Message, ActionUrl)
    OUTPUT inserted.NotificationId INTO @Created(NotificationId)
    SELECT u.UserId, @OrderId, @EventType, @Title, @Message, @ActionUrl
    FROM dbo.tblAppUser u
    WHERE u.IsActive = 1
      AND ((@UserId IS NOT NULL AND u.UserId = @UserId)
        OR (@RoleName IS NOT NULL AND u.RoleName = @RoleName));

    INSERT dbo.tblNotificationOutbox(NotificationId)
    SELECT NotificationId FROM @Created;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_NotificationOutbox_Claim
    @BatchSize INT = 25,
    @LeaseSeconds INT = 60
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @BatchSize = CASE WHEN @BatchSize < 1 THEN 1 WHEN @BatchSize > 100 THEN 100 ELSE @BatchSize END;
    SET @LeaseSeconds = CASE WHEN @LeaseSeconds < 10 THEN 10 WHEN @LeaseSeconds > 300 THEN 300 ELSE @LeaseSeconds END;

    DECLARE @Claimed TABLE
    (
        OutboxId BIGINT NOT NULL,
        NotificationId BIGINT NOT NULL,
        Attempts INT NOT NULL
    );

    BEGIN TRANSACTION;
    ;WITH Pending AS
    (
        SELECT TOP (@BatchSize) OutboxId, NotificationId, Attempts, ClaimedUntil
        FROM dbo.tblNotificationOutbox WITH (UPDLOCK, READPAST, ROWLOCK, READCOMMITTEDLOCK)
        WHERE ProcessedAt IS NULL
          AND NextAttemptAt <= SYSUTCDATETIME()
          AND (ClaimedUntil IS NULL OR ClaimedUntil <= SYSUTCDATETIME())
        ORDER BY OutboxId
    )
    UPDATE Pending
       SET Attempts = Attempts + 1,
           ClaimedUntil = DATEADD(SECOND, @LeaseSeconds, SYSUTCDATETIME())
    OUTPUT inserted.OutboxId, inserted.NotificationId, inserted.Attempts
      INTO @Claimed(OutboxId, NotificationId, Attempts);
    COMMIT;

    SELECT c.OutboxId, n.NotificationId, n.UserId, n.OrderId, n.EventType, n.Title,
           n.Message, n.ActionUrl, n.IsRead, n.CreatedAt, n.ReadAt, c.Attempts
    FROM @Claimed c
    JOIN dbo.tblNotification n ON n.NotificationId = c.NotificationId
    ORDER BY c.OutboxId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_NotificationOutbox_MarkProcessed @OutboxId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tblNotificationOutbox
       SET ProcessedAt = SYSUTCDATETIME(), ClaimedUntil = NULL, LastError = NULL
    WHERE OutboxId = @OutboxId AND ProcessedAt IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_NotificationOutbox_ScheduleRetry
    @OutboxId BIGINT,
    @Error NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tblNotificationOutbox
       SET ClaimedUntil = NULL,
           NextAttemptAt = DATEADD(SECOND,
               CASE WHEN Attempts >= 9 THEN 300 ELSE CONVERT(INT, POWER(2, Attempts)) END,
               SYSUTCDATETIME()),
           LastError = @Error
    WHERE OutboxId = @OutboxId AND ProcessedAt IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_Submit @CustomerId BIGINT, @Description NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblAppUser WHERE UserId = @CustomerId AND RoleName = 'Customer' AND IsActive = 1)
        THROW 50001, 'Active customer user was not found.', 1;
    BEGIN TRANSACTION;
    INSERT dbo.tblOrder(CustomerUserId, Description, Status)
    VALUES(@CustomerId, @Description, 'Submitted');
    DECLARE @OrderId BIGINT = CONVERT(BIGINT, SCOPE_IDENTITY());
    DECLARE @Message NVARCHAR(500) = CONCAT(N'Customer ', @CustomerId, N' submitted order #', @OrderId, N'.');
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId);
    EXEC dbo.SP_Notification_CreateForTarget
        @RoleName = 'Admin', @OrderId = @OrderId, @EventType = 'OrderSubmitted',
        @Title = N'New order submitted',
        @Message = @Message, @ActionUrl = @ActionUrl;
    COMMIT;
    SELECT @OrderId AS OrderId, CAST('Submitted' AS VARCHAR(40)) AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_AssignCad
    @OrderId BIGINT, @AdminId BIGINT, @CadUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblAppUser WHERE UserId = @AdminId AND RoleName = 'Admin' AND IsActive = 1)
        THROW 50002, 'Active admin user was not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblAppUser WHERE UserId = @CadUserId AND RoleName = 'CAD' AND IsActive = 1)
        THROW 50003, 'Active CAD user was not found.', 1;
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblOrder WITH (UPDLOCK, ROWLOCK) WHERE OrderId = @OrderId AND Status = 'Submitted')
    BEGIN
        ROLLBACK;
        THROW 50004, 'Order not found or not in Submitted status.', 1;
    END;
    UPDATE dbo.tblOrder SET Status = 'AssignedToCad', UpdatedAt = SYSUTCDATETIME() WHERE OrderId = @OrderId;
    UPDATE dbo.tblOrderAssignment SET IsActive = 0 WHERE OrderId = @OrderId AND IsActive = 1;
    INSERT dbo.tblOrderAssignment(OrderId, AssignedToUserId, AssignedByUserId)
    VALUES(@OrderId, @CadUserId, @AdminId);
    DECLARE @CustomerId BIGINT;
    SELECT @CustomerId = CustomerUserId FROM dbo.tblOrder WHERE OrderId = @OrderId;
    DECLARE @Message NVARCHAR(500) = CONCAT(N'Order #', @OrderId, N' has been assigned to you.');
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId);
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @CadUserId, @OrderId = @OrderId, @EventType = 'OrderAssignedToCad',
        @Title = N'New CAD assignment',
        @Message = @Message, @ActionUrl = @ActionUrl;
    SELECT @Message = CONCAT(N'Your order #', @OrderId, N' has been assigned to the design team.');
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @CustomerId,
        @OrderId = @OrderId, @EventType = 'OrderAssignedToCad',
        @Title = N'Order update',
        @Message = @Message, @ActionUrl = @ActionUrl;
    COMMIT;
    SELECT @OrderId AS OrderId, CAST('AssignedToCad' AS VARCHAR(40)) AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_SubmitDesign @OrderId BIGINT, @CadUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.tblOrder o WITH (UPDLOCK, ROWLOCK)
        JOIN dbo.tblOrderAssignment a ON a.OrderId = o.OrderId AND a.IsActive = 1 AND a.AssignedToUserId = @CadUserId
        WHERE o.OrderId = @OrderId AND o.Status IN ('AssignedToCad', 'DesignRejected'))
    BEGIN
        ROLLBACK;
        THROW 50005, 'Order is not assigned to this CAD user or has an invalid status.', 1;
    END;
    UPDATE dbo.tblOrder SET Status = 'DesignSubmitted', UpdatedAt = SYSUTCDATETIME() WHERE OrderId = @OrderId;
    DECLARE @CustomerId BIGINT;
    SELECT @CustomerId = CustomerUserId FROM dbo.tblOrder WHERE OrderId = @OrderId;
    DECLARE @Message NVARCHAR(500) = CONCAT(N'Design for order #', @OrderId, N' is ready for approval.');
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId, N'/design');
    EXEC dbo.SP_Notification_CreateForTarget
        @RoleName = 'Admin', @OrderId = @OrderId, @EventType = 'DesignSubmitted',
        @Title = N'Design submitted',
        @Message = @Message, @ActionUrl = @ActionUrl;
    SET @Message = CONCAT(N'Design for order #', @OrderId, N' is ready for your approval.');
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @CustomerId, @OrderId = @OrderId, @EventType = 'DesignSubmitted',
        @Title = N'Design ready for approval',
        @Message = @Message, @ActionUrl = @ActionUrl;
    COMMIT;
    SELECT @OrderId AS OrderId, CAST('DesignSubmitted' AS VARCHAR(40)) AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_DesignDecision
    @OrderId BIGINT, @CustomerId BIGINT, @Approved BIT, @Comment NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @NewStatus VARCHAR(40) = CASE WHEN @Approved = 1 THEN 'DesignApproved' ELSE 'DesignRejected' END;
    BEGIN TRANSACTION;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.tblOrder WITH (UPDLOCK, ROWLOCK)
        WHERE OrderId = @OrderId AND CustomerUserId = @CustomerId AND Status = 'DesignSubmitted')
    BEGIN
        ROLLBACK;
        THROW 50006, 'Order not found, not owned by this customer, or design is not awaiting approval.', 1;
    END;
    UPDATE dbo.tblOrder SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME() WHERE OrderId = @OrderId;
    DECLARE @Message NVARCHAR(500);
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId);
    IF @Approved = 1
    BEGIN
        SET @Message = CONCAT(N'Order #', @OrderId, N' is approved for production.');
        EXEC dbo.SP_Notification_CreateForTarget
            @RoleName = 'Admin', @OrderId = @OrderId, @EventType = 'DesignApproved',
            @Title = N'Design approved',
            @Message = @Message, @ActionUrl = @ActionUrl;
        EXEC dbo.SP_Notification_CreateForTarget
            @RoleName = 'Production', @OrderId = @OrderId, @EventType = 'DesignApproved',
            @Title = N'Design approved',
            @Message = @Message, @ActionUrl = @ActionUrl;
    END
    ELSE
    BEGIN
        DECLARE @CadUserId BIGINT;
        SELECT TOP (1) @CadUserId = AssignedToUserId
        FROM dbo.tblOrderAssignment
        WHERE OrderId = @OrderId AND IsActive = 1
        ORDER BY AssignmentId DESC;
        SET @Message = LEFT(CONCAT(N'Design for order #', @OrderId, N' was rejected. ', @Comment), 500);
        SET @ActionUrl = CONCAT(N'/orders/', @OrderId, N'/design');
        EXEC dbo.SP_Notification_CreateForTarget
            @RoleName = 'Admin', @OrderId = @OrderId, @EventType = 'DesignRejected',
            @Title = N'Design needs revision',
            @Message = @Message, @ActionUrl = @ActionUrl;
        IF @CadUserId IS NOT NULL
            EXEC dbo.SP_Notification_CreateForTarget
                @UserId = @CadUserId, @OrderId = @OrderId, @EventType = 'DesignRejected',
                @Title = N'Design needs revision',
                @Message = @Message, @ActionUrl = @ActionUrl;
    END;
    COMMIT;
    SELECT @OrderId AS OrderId, @NewStatus AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_GetOwner @OrderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CustomerUserId FROM dbo.tblOrder WHERE OrderId = @OrderId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_GetAssignedCad @OrderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) AssignedToUserId FROM dbo.tblOrderAssignment
    WHERE OrderId = @OrderId AND IsActive = 1 ORDER BY AssignmentId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_AssignProduction
    @OrderId BIGINT, @AdminId BIGINT, @ProductionUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblAppUser WHERE UserId = @AdminId AND RoleName = 'Admin' AND IsActive = 1)
        THROW 50015, 'Active admin user was not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblAppUser WHERE UserId = @ProductionUserId AND RoleName = 'Production' AND IsActive = 1)
        THROW 50016, 'Active production user was not found.', 1;

    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblOrder WITH (UPDLOCK, ROWLOCK) WHERE OrderId = @OrderId AND Status = 'DesignApproved')
    BEGIN
        ROLLBACK;
        THROW 50017, 'Order not found or design is not approved for production.', 1;
    END;
    UPDATE dbo.tblOrder SET Status = 'AssignedToProduction', UpdatedAt = SYSUTCDATETIME()
    WHERE OrderId = @OrderId;
    UPDATE dbo.tblOrderAssignment SET IsActive = 0 WHERE OrderId = @OrderId AND IsActive = 1;
    INSERT dbo.tblOrderAssignment(OrderId, AssignedToUserId, AssignedByUserId)
    VALUES(@OrderId, @ProductionUserId, @AdminId);

    DECLARE @CustomerId BIGINT;
    SELECT @CustomerId = CustomerUserId FROM dbo.tblOrder WHERE OrderId = @OrderId;
    DECLARE @Message NVARCHAR(500) = CONCAT(N'Order #', @OrderId, N' has been assigned to production.');
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId);
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @ProductionUserId, @OrderId = @OrderId, @EventType = 'OrderAssignedToProduction',
        @Title = N'New production assignment', @Message = @Message, @ActionUrl = @ActionUrl;
    SET @Message = CONCAT(N'Your approved order #', @OrderId, N' has been assigned to production.');
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @CustomerId, @OrderId = @OrderId, @EventType = 'OrderAssignedToProduction',
        @Title = N'Order update', @Message = @Message, @ActionUrl = @ActionUrl;
    COMMIT;
    SELECT @OrderId AS OrderId, CAST('AssignedToProduction' AS VARCHAR(40)) AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_AdvanceProduction
    @OrderId BIGINT, @ProductionUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @CurrentStatus VARCHAR(40);
    DECLARE @NewStatus VARCHAR(40);
    DECLARE @CustomerId BIGINT;

    BEGIN TRANSACTION;
    SELECT @CurrentStatus = o.Status, @CustomerId = o.CustomerUserId
    FROM dbo.tblOrder o WITH (UPDLOCK, ROWLOCK)
    JOIN dbo.tblOrderAssignment a ON a.OrderId = o.OrderId AND a.IsActive = 1
        AND a.AssignedToUserId = @ProductionUserId
    JOIN dbo.tblAppUser u ON u.UserId = a.AssignedToUserId AND u.RoleName = 'Production' AND u.IsActive = 1
    WHERE o.OrderId = @OrderId;

    SET @NewStatus = CASE @CurrentStatus
        WHEN 'AssignedToProduction' THEN 'ProductionStarted'
        WHEN 'ProductionStarted' THEN 'BagGenerated'
        WHEN 'BagGenerated' THEN 'Production'
        WHEN 'Production' THEN 'Casting'
        WHEN 'Casting' THEN 'Rhodium'
        WHEN 'Rhodium' THEN 'FinishBag'
        ELSE NULL
    END;
    IF @NewStatus IS NULL
    BEGIN
        ROLLBACK;
        THROW 50018, 'Order is not assigned to this production user or has no remaining production stage.', 1;
    END;

    UPDATE dbo.tblOrder SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME() WHERE OrderId = @OrderId;
    DECLARE @Message NVARCHAR(500) = CONCAT(N'Order #', @OrderId, N' completed the ', @NewStatus, N' stage.');
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId);
    EXEC dbo.SP_Notification_CreateForTarget
        @RoleName = 'Admin', @OrderId = @OrderId, @EventType = 'ProductionStageUpdated',
        @Title = N'Production stage updated', @Message = @Message, @ActionUrl = @ActionUrl;
    SET @Message = CONCAT(N'Your order #', @OrderId, N' completed the ', @NewStatus, N' stage.');
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @CustomerId, @OrderId = @OrderId, @EventType = 'ProductionStageUpdated',
        @Title = N'Production update', @Message = @Message, @ActionUrl = @ActionUrl;
    COMMIT;
    SELECT @OrderId AS OrderId, @NewStatus AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_AssignBilling
    @OrderId BIGINT, @ProductionUserId BIGINT, @BillingUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblAppUser WHERE UserId = @ProductionUserId AND RoleName = 'Production' AND IsActive = 1)
        THROW 50019, 'Active production user was not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblAppUser WHERE UserId = @BillingUserId AND RoleName = 'Billing' AND IsActive = 1)
        THROW 50020, 'Active billing user was not found.', 1;

    BEGIN TRANSACTION;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.tblOrder o WITH (UPDLOCK, ROWLOCK)
        JOIN dbo.tblOrderAssignment a ON a.OrderId = o.OrderId AND a.IsActive = 1
            AND a.AssignedToUserId = @ProductionUserId
        JOIN dbo.tblAppUser productionUser ON productionUser.UserId = a.AssignedToUserId
            AND productionUser.RoleName = 'Production' AND productionUser.IsActive = 1
        WHERE o.OrderId = @OrderId AND o.Status = 'FinishBag')
    BEGIN
        ROLLBACK;
        THROW 50021, 'Order is not assigned to this production user or production is not finished.', 1;
    END;
    UPDATE dbo.tblOrder SET Status = 'AssignedToBilling', UpdatedAt = SYSUTCDATETIME()
    WHERE OrderId = @OrderId;
    UPDATE dbo.tblOrderAssignment SET IsActive = 0 WHERE OrderId = @OrderId AND IsActive = 1;
    INSERT dbo.tblOrderAssignment(OrderId, AssignedToUserId, AssignedByUserId)
    VALUES(@OrderId, @BillingUserId, @ProductionUserId);

    DECLARE @CustomerId BIGINT;
    SELECT @CustomerId = CustomerUserId FROM dbo.tblOrder WHERE OrderId = @OrderId;
    DECLARE @Message NVARCHAR(500) = CONCAT(N'Order #', @OrderId, N' is ready for final billing.');
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId);
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @BillingUserId, @OrderId = @OrderId, @EventType = 'OrderAssignedToBilling',
        @Title = N'Order ready for final billing', @Message = @Message, @ActionUrl = @ActionUrl;
    SET @Message = CONCAT(N'Production is complete for order #', @OrderId, N'. Final billing is underway.');
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @CustomerId, @OrderId = @OrderId, @EventType = 'OrderAssignedToBilling',
        @Title = N'Production complete', @Message = @Message, @ActionUrl = @ActionUrl;
    COMMIT;
    SELECT @OrderId AS OrderId, CAST('AssignedToBilling' AS VARCHAR(40)) AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_CompleteBilling
    @OrderId BIGINT, @BillingUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @CustomerId BIGINT;
    BEGIN TRANSACTION;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.tblOrder o WITH (UPDLOCK, ROWLOCK)
        JOIN dbo.tblOrderAssignment a ON a.OrderId = o.OrderId AND a.IsActive = 1
            AND a.AssignedToUserId = @BillingUserId
        JOIN dbo.tblAppUser u ON u.UserId = a.AssignedToUserId AND u.RoleName = 'Billing' AND u.IsActive = 1
        WHERE o.OrderId = @OrderId AND o.Status = 'AssignedToBilling')
    BEGIN
        ROLLBACK;
        THROW 50022, 'Order is not awaiting billing with this billing user.', 1;
    END;
    UPDATE dbo.tblOrder SET Status = 'ReadyForDispatch', UpdatedAt = SYSUTCDATETIME()
    WHERE OrderId = @OrderId;
    SELECT @CustomerId = CustomerUserId FROM dbo.tblOrder WHERE OrderId = @OrderId;
    DECLARE @Message NVARCHAR(500) = CONCAT(N'Final billing is complete for order #', @OrderId, N'. It is ready for dispatch.');
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId);
    EXEC dbo.SP_Notification_CreateForTarget
        @RoleName = 'Admin', @OrderId = @OrderId, @EventType = 'FinalBillingCompleted',
        @Title = N'Final billing completed', @Message = @Message, @ActionUrl = @ActionUrl;
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @CustomerId, @OrderId = @OrderId, @EventType = 'FinalBillingCompleted',
        @Title = N'Order ready for dispatch', @Message = @Message, @ActionUrl = @ActionUrl;
    COMMIT;
    SELECT @OrderId AS OrderId, CAST('ReadyForDispatch' AS VARCHAR(40)) AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_Order_MarkDispatched
    @OrderId BIGINT, @BillingUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @CustomerId BIGINT;
    BEGIN TRANSACTION;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.tblOrder o WITH (UPDLOCK, ROWLOCK)
        JOIN dbo.tblOrderAssignment a ON a.OrderId = o.OrderId AND a.IsActive = 1
            AND a.AssignedToUserId = @BillingUserId
        JOIN dbo.tblAppUser u ON u.UserId = a.AssignedToUserId AND u.RoleName = 'Billing' AND u.IsActive = 1
        WHERE o.OrderId = @OrderId AND o.Status = 'ReadyForDispatch')
    BEGIN
        ROLLBACK;
        THROW 50023, 'Order is not ready for dispatch with this billing user.', 1;
    END;
    UPDATE dbo.tblOrder SET Status = 'Dispatched', UpdatedAt = SYSUTCDATETIME() WHERE OrderId = @OrderId;
    SELECT @CustomerId = CustomerUserId FROM dbo.tblOrder WHERE OrderId = @OrderId;
    DECLARE @Message NVARCHAR(500) = CONCAT(N'Order #', @OrderId, N' has been dispatched.');
    DECLARE @ActionUrl NVARCHAR(500) = CONCAT(N'/orders/', @OrderId);
    EXEC dbo.SP_Notification_CreateForTarget
        @RoleName = 'Admin', @OrderId = @OrderId, @EventType = 'OrderDispatched',
        @Title = N'Order dispatched', @Message = @Message, @ActionUrl = @ActionUrl;
    EXEC dbo.SP_Notification_CreateForTarget
        @UserId = @CustomerId, @OrderId = @OrderId, @EventType = 'OrderDispatched',
        @Title = N'Your order has been dispatched', @Message = @Message, @ActionUrl = @ActionUrl;
    COMMIT;
    SELECT @OrderId AS OrderId, CAST('Dispatched' AS VARCHAR(40)) AS Status;
END;
GO
