ALTER DATABASE CHARACTER SET utf8mb4;


CREATE TABLE `AccessSequences` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Prefix` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Year` int NOT NULL,
    `Month` int NOT NULL,
    `LastSequence` int NOT NULL,
    CONSTRAINT `PK_AccessSequences` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ApiKeys` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `AppName` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `KeyHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `KeyPrefix` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `LastUsedAt` datetime(6) NULL,
    `CreatedById` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApiKeys` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `AppLogs` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Timestamp` datetime(6) NOT NULL,
    `Level` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Category` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Source` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Message` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `Details` text CHARACTER SET utf8mb4 NULL,
    `UserId` longtext CHARACTER SET utf8mb4 NULL,
    `UserEmail` varchar(300) CHARACTER SET utf8mb4 NULL,
    `Action` varchar(200) CHARACTER SET utf8mb4 NULL,
    `EntityType` varchar(100) CHARACTER SET utf8mb4 NULL,
    `EntityId` int NULL,
    `IpAddress` varchar(45) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AppLogs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ApprovalTemplates` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `EntityType` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `EntitySubType` varchar(100) CHARACTER SET utf8mb4 NULL,
    `ApprovalMode` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'AllMustApprove',
    `AutoTriggerOn` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'None',
    `ConditionField` varchar(100) CHARACTER SET utf8mb4 NULL,
    `ConditionValue` varchar(200) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApprovalTemplates` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `AspNetRoles` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(256) CHARACTER SET utf8mb4 NULL,
    `NormalizedName` varchar(256) CHARACTER SET utf8mb4 NULL,
    `ConcurrencyStamp` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AspNetRoles` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `AspNetUsers` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `FullName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Department` longtext CHARACTER SET utf8mb4 NOT NULL,
    `UserName` varchar(256) CHARACTER SET utf8mb4 NULL,
    `NormalizedUserName` varchar(256) CHARACTER SET utf8mb4 NULL,
    `Email` varchar(256) CHARACTER SET utf8mb4 NULL,
    `NormalizedEmail` varchar(256) CHARACTER SET utf8mb4 NULL,
    `EmailConfirmed` tinyint(1) NOT NULL,
    `PasswordHash` longtext CHARACTER SET utf8mb4 NULL,
    `SecurityStamp` longtext CHARACTER SET utf8mb4 NULL,
    `ConcurrencyStamp` longtext CHARACTER SET utf8mb4 NULL,
    `PhoneNumber` longtext CHARACTER SET utf8mb4 NULL,
    `PhoneNumberConfirmed` tinyint(1) NOT NULL,
    `TwoFactorEnabled` tinyint(1) NOT NULL,
    `LockoutEnd` datetime(6) NULL,
    `LockoutEnabled` tinyint(1) NOT NULL,
    `AccessFailedCount` int NOT NULL,
    CONSTRAINT `PK_AspNetUsers` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ChangeRequestSequences` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Year` int NOT NULL,
    `Month` int NOT NULL,
    `LastSequence` int NOT NULL,
    CONSTRAINT `PK_ChangeRequestSequences` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `DocumentSequences` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Year` int NOT NULL,
    `Month` int NOT NULL,
    `LastSequence` int NOT NULL,
    CONSTRAINT `PK_DocumentSequences` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `HcmMappings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RawValue` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CanonicalName` longtext CHARACTER SET utf8mb4 NULL,
    `Classification` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SourceColumn` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Aliases` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_HcmMappings` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `IncidentSequences` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Year` int NOT NULL,
    `Month` int NOT NULL,
    `LastSequence` int NOT NULL,
    CONSTRAINT `PK_IncidentSequences` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ThemeSettings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PrimaryColor` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SidebarBg` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SidebarText` longtext CHARACTER SET utf8mb4 NOT NULL,
    `AppBg` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CardBg` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TopbarBg` longtext CHARACTER SET utf8mb4 NOT NULL,
    `AccentHover` longtext CHARACTER SET utf8mb4 NOT NULL,
    `FontFamily` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LogoText` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ThemeSettings` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `AspNetRoleClaims` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RoleId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ClaimType` longtext CHARACTER SET utf8mb4 NULL,
    `ClaimValue` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AspNetRoleClaims` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AspNetRoleClaims_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `AccessRequests` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SerialNumber` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `RequestedById` varchar(450) CHARACTER SET utf8mb4 NOT NULL,
    `EmployeeName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `EmployeeId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Department` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `JobTitle` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `SystemName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `AccessType` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `AccessDetails` text CHARACTER SET utf8mb4 NOT NULL,
    `Justification` text CHARACTER SET utf8mb4 NOT NULL,
    `RequestedByDate` datetime(6) NOT NULL,
    `AccessExpiryDate` datetime(6) NULL,
    `IsPrivileged` tinyint(1) NOT NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `ProvisionedAt` datetime(6) NULL,
    `ProvisionedById` varchar(450) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AccessRequests` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AccessRequests_AspNetUsers_ProvisionedById` FOREIGN KEY (`ProvisionedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_AccessRequests_AspNetUsers_RequestedById` FOREIGN KEY (`RequestedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `AccessReviews` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `Cycle` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Year` int NOT NULL,
    `Quarter` int NOT NULL,
    `ReviewPeriodStart` datetime(6) NOT NULL,
    `ReviewPeriodEnd` datetime(6) NOT NULL,
    `DueDate` datetime(6) NOT NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedById` varchar(450) CHARACTER SET utf8mb4 NOT NULL,
    `CompletedAt` datetime(6) NULL,
    `Notes` text CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AccessReviews` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AccessReviews_AspNetUsers_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ApprovalFlows` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EntityType` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `EntityId` int NOT NULL,
    `TemplateId` int NOT NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `CurrentStepOrder` int NOT NULL,
    `InitiatedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `InitiatedAt` datetime(6) NOT NULL,
    `ClosedAt` datetime(6) NULL,
    `ReturnComment` varchar(2000) CHARACTER SET utf8mb4 NULL,
    `ReturnedById` varchar(255) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApprovalFlows` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ApprovalFlows_ApprovalTemplates_TemplateId` FOREIGN KEY (`TemplateId`) REFERENCES `ApprovalTemplates` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ApprovalFlows_AspNetUsers_InitiatedById` FOREIGN KEY (`InitiatedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ApprovalFlows_AspNetUsers_ReturnedById` FOREIGN KEY (`ReturnedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ApprovalTemplateSteps` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TemplateId` int NOT NULL,
    `Order` int NOT NULL,
    `ApproverId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `RoleName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApprovalTemplateSteps` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ApprovalTemplateSteps_ApprovalTemplates_TemplateId` FOREIGN KEY (`TemplateId`) REFERENCES `ApprovalTemplates` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ApprovalTemplateSteps_AspNetUsers_ApproverId` FOREIGN KEY (`ApproverId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `AspNetUserClaims` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ClaimType` longtext CHARACTER SET utf8mb4 NULL,
    `ClaimValue` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AspNetUserClaims` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AspNetUserClaims_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `AspNetUserLogins` (
    `LoginProvider` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ProviderKey` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ProviderDisplayName` longtext CHARACTER SET utf8mb4 NULL,
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AspNetUserLogins` PRIMARY KEY (`LoginProvider`, `ProviderKey`),
    CONSTRAINT `FK_AspNetUserLogins_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `AspNetUserRoles` (
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `RoleId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AspNetUserRoles` PRIMARY KEY (`UserId`, `RoleId`),
    CONSTRAINT `FK_AspNetUserRoles_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AspNetUserRoles_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `AspNetUserTokens` (
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `LoginProvider` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Value` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AspNetUserTokens` PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
    CONSTRAINT `FK_AspNetUserTokens_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `Attachments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EntityType` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `EntityId` int NOT NULL,
    `OriginalFileName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `StoredFileName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `FilePath` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `ContentType` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `FileSizeBytes` bigint NOT NULL,
    `UploadedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Attachments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Attachments_AspNetUsers_UploadedById` FOREIGN KEY (`UploadedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ChangeRequests` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SerialNumber` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NOT NULL,
    `Type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Priority` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `RequestedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `TargetDate` datetime(6) NULL,
    `ImplementationNotes` text CHARACTER SET utf8mb4 NULL,
    `SystemsAffected` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `RiskDescription` text CHARACTER SET utf8mb4 NULL,
    `ImpactOnUsers` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `ProposedImplementationWindow` varchar(500) CHARACTER SET utf8mb4 NULL,
    `RollbackPlan` text CHARACTER SET utf8mb4 NULL,
    `RollbackTrigger` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `TestingSteps` text CHARACTER SET utf8mb4 NULL,
    `StagingTested` varchar(10) CHARACTER SET utf8mb4 NULL,
    `PirOutcome` varchar(30) CHARACTER SET utf8mb4 NULL,
    `PirActualDate` datetime(6) NULL,
    `PirIssuesEncountered` text CHARACTER SET utf8mb4 NULL,
    `PirLessonsLearned` text CHARACTER SET utf8mb4 NULL,
    `PirRollbackExecuted` tinyint(1) NULL,
    `PirClosureNotes` text CHARACTER SET utf8mb4 NULL,
    `PirCompletedById` varchar(255) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ChangeRequests` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ChangeRequests_AspNetUsers_PirCompletedById` FOREIGN KEY (`PirCompletedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_ChangeRequests_AspNetUsers_RequestedById` FOREIGN KEY (`RequestedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `DeprovisioningEvents` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SerialNumber` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `EmployeeName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `EmployeeId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Department` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `JobTitle` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Trigger` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `TriggerDetails` text CHARACTER SET utf8mb4 NOT NULL,
    `HrNotificationReceivedAt` datetime(6) NOT NULL,
    `SlaDeadline` datetime(6) NOT NULL,
    `SlaWarningEmailSentAt` datetime(6) NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `NotifiedById` varchar(450) CHARACTER SET utf8mb4 NOT NULL,
    `CompletedAt` datetime(6) NULL,
    `CompletedById` varchar(450) CHARACTER SET utf8mb4 NULL,
    `Notes` text CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_DeprovisioningEvents` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DeprovisioningEvents_AspNetUsers_CompletedById` FOREIGN KEY (`CompletedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_DeprovisioningEvents_AspNetUsers_NotifiedById` FOREIGN KEY (`NotifiedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `DocumentFolders` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `ParentId` int NULL,
    `CreatedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_DocumentFolders` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DocumentFolders_AspNetUsers_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_DocumentFolders_DocumentFolders_ParentId` FOREIGN KEY (`ParentId`) REFERENCES `DocumentFolders` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `Incidents` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SerialNumber` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NOT NULL,
    `Category` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Severity` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Priority` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `AffectedSystems` text CHARACTER SET utf8mb4 NULL,
    `ImpactDescription` text CHARACTER SET utf8mb4 NULL,
    `RootCause` text CHARACTER SET utf8mb4 NULL,
    `ResolutionSummary` text CHARACTER SET utf8mb4 NULL,
    `DetectedAt` datetime(6) NOT NULL,
    `ResolvedAt` datetime(6) NULL,
    `ClosedAt` datetime(6) NULL,
    `LinkedChangeRequestId` int NULL,
    `ReportedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `AssignedToId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Incidents` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Incidents_AspNetUsers_AssignedToId` FOREIGN KEY (`AssignedToId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_Incidents_AspNetUsers_ReportedById` FOREIGN KEY (`ReportedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `InvoiceLenders` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `CompanyType` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `DeductionCode` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedById` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_InvoiceLenders` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InvoiceLenders_AspNetUsers_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `InvoiceRecipients` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `IsDefault` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedById` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_InvoiceRecipients` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InvoiceRecipients_AspNetUsers_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `ServerRoomAccessRequests` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SerialNumber` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `RequestedById` varchar(450) CHARACTER SET utf8mb4 NOT NULL,
    `VisitorName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `VisitorTitle` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `VisitorCompany` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `IsExternal` tinyint(1) NOT NULL,
    `Purpose` text CHARACTER SET utf8mb4 NOT NULL,
    `PlannedEntryDateTime` datetime(6) NOT NULL,
    `PlannedExitDateTime` datetime(6) NOT NULL,
    `ActualEntryDateTime` datetime(6) NULL,
    `ActualExitDateTime` datetime(6) NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `WrittenRequestReference` varchar(500) CHARACTER SET utf8mb4 NULL,
    `EscortedBy` varchar(300) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ServerRoomAccessRequests` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ServerRoomAccessRequests_AspNetUsers_RequestedById` FOREIGN KEY (`RequestedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `AccessRequestComments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `AccessRequestId` int NOT NULL,
    `AuthorId` varchar(450) CHARACTER SET utf8mb4 NOT NULL,
    `Content` text CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AccessRequestComments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AccessRequestComments_AccessRequests_AccessRequestId` FOREIGN KEY (`AccessRequestId`) REFERENCES `AccessRequests` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AccessRequestComments_AspNetUsers_AuthorId` FOREIGN KEY (`AuthorId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `AccessReviewEntries` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `AccessReviewId` int NOT NULL,
    `EmployeeName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `Department` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `SystemName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `CurrentAccessLevel` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `Outcome` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `ReviewerNotes` text CHARACTER SET utf8mb4 NULL,
    `ReviewedById` varchar(450) CHARACTER SET utf8mb4 NULL,
    `ReviewedAt` datetime(6) NULL,
    `LinkedDeprovisioningEventId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AccessReviewEntries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AccessReviewEntries_AccessReviews_AccessReviewId` FOREIGN KEY (`AccessReviewId`) REFERENCES `AccessReviews` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AccessReviewEntries_AspNetUsers_ReviewedById` FOREIGN KEY (`ReviewedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `ApprovalSteps` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EntityType` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `EntityId` int NOT NULL,
    `FlowId` int NULL,
    `Order` int NOT NULL,
    `RoleName` longtext CHARACTER SET utf8mb4 NULL,
    `ApproverId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Comment` varchar(2000) CHARACTER SET utf8mb4 NULL,
    `ActedAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApprovalSteps` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ApprovalSteps_ApprovalFlows_FlowId` FOREIGN KEY (`FlowId`) REFERENCES `ApprovalFlows` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_ApprovalSteps_AspNetUsers_ApproverId` FOREIGN KEY (`ApproverId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ChangeRequestComments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ChangeRequestId` int NOT NULL,
    `AuthorId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Content` text CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ChangeRequestComments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ChangeRequestComments_AspNetUsers_AuthorId` FOREIGN KEY (`AuthorId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ChangeRequestComments_ChangeRequests_ChangeRequestId` FOREIGN KEY (`ChangeRequestId`) REFERENCES `ChangeRequests` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `DeprovisioningSystemEntries` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DeprovisioningEventId` int NOT NULL,
    `SystemName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `AccessDescription` text CHARACTER SET utf8mb4 NOT NULL,
    `IsDeactivated` tinyint(1) NOT NULL,
    `DeactivatedAt` datetime(6) NULL,
    `DeactivatedById` varchar(450) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_DeprovisioningSystemEntries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DeprovisioningSystemEntries_AspNetUsers_DeactivatedById` FOREIGN KEY (`DeactivatedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_DeprovisioningSystemEntries_DeprovisioningEvents_Deprovision~` FOREIGN KEY (`DeprovisioningEventId`) REFERENCES `DeprovisioningEvents` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `Documents` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SerialNumber` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NULL,
    `Tags` varchar(500) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `FolderId` int NULL,
    `OwnerId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ReviewPeriodDays` int NULL,
    `NextReviewDate` datetime(6) NULL,
    `LastReviewedAt` datetime(6) NULL,
    `LastReviewedById` varchar(255) CHARACTER SET utf8mb4 NULL,
    `ReviewAlertFlags` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Documents` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Documents_AspNetUsers_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Documents_AspNetUsers_LastReviewedById` FOREIGN KEY (`LastReviewedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_Documents_AspNetUsers_OwnerId` FOREIGN KEY (`OwnerId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Documents_DocumentFolders_FolderId` FOREIGN KEY (`FolderId`) REFERENCES `DocumentFolders` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `FolderPermissions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FolderId` int NOT NULL,
    `SubjectType` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `SubjectId` varchar(450) CHARACTER SET utf8mb4 NOT NULL,
    `AccessLevel` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_FolderPermissions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_FolderPermissions_DocumentFolders_FolderId` FOREIGN KEY (`FolderId`) REFERENCES `DocumentFolders` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `IncidentDocuments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `IncidentId` int NOT NULL,
    `DocumentId` int NOT NULL,
    `LinkedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_IncidentDocuments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_IncidentDocuments_AspNetUsers_LinkedById` FOREIGN KEY (`LinkedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_IncidentDocuments_Incidents_IncidentId` FOREIGN KEY (`IncidentId`) REFERENCES `Incidents` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `IncidentUpdates` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `IncidentId` int NOT NULL,
    `AuthorId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Content` text CHARACTER SET utf8mb4 NOT NULL,
    `UpdateType` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `OldStatus` varchar(20) CHARACTER SET utf8mb4 NULL,
    `NewStatus` varchar(20) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_IncidentUpdates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_IncidentUpdates_AspNetUsers_AuthorId` FOREIGN KEY (`AuthorId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_IncidentUpdates_Incidents_IncidentId` FOREIGN KEY (`IncidentId`) REFERENCES `Incidents` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `InvoiceSchedules` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `LenderId` int NOT NULL,
    `Frequency` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `DayOfWeek` int NULL,
    `DayOfMonth` int NULL,
    `TimeOfDay` time(0) NOT NULL,
    `SplitSheets` tinyint(1) NOT NULL,
    `IsEnabled` tinyint(1) NOT NULL,
    `LastRunAt` datetime(6) NULL,
    `NextRunAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedById` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_InvoiceSchedules` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InvoiceSchedules_AspNetUsers_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_InvoiceSchedules_InvoiceLenders_LenderId` FOREIGN KEY (`LenderId`) REFERENCES `InvoiceLenders` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ServerRoomAccessComments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ServerRoomAccessRequestId` int NOT NULL,
    `AuthorId` varchar(450) CHARACTER SET utf8mb4 NOT NULL,
    `Content` text CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ServerRoomAccessComments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ServerRoomAccessComments_AspNetUsers_AuthorId` FOREIGN KEY (`AuthorId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ServerRoomAccessComments_ServerRoomAccessRequests_ServerRoom~` FOREIGN KEY (`ServerRoomAccessRequestId`) REFERENCES `ServerRoomAccessRequests` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `DocumentPermissions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DocumentId` int NOT NULL,
    `SubjectType` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `SubjectId` varchar(450) CHARACTER SET utf8mb4 NOT NULL,
    `AccessLevel` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_DocumentPermissions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DocumentPermissions_Documents_DocumentId` FOREIGN KEY (`DocumentId`) REFERENCES `Documents` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `DocumentReviews` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DocumentId` int NOT NULL,
    `ReviewedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ReviewedAt` datetime(6) NOT NULL,
    `Notes` varchar(2000) CHARACTER SET utf8mb4 NULL,
    `NextReviewDate` datetime(6) NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_DocumentReviews` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DocumentReviews_AspNetUsers_ReviewedById` FOREIGN KEY (`ReviewedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_DocumentReviews_Documents_DocumentId` FOREIGN KEY (`DocumentId`) REFERENCES `Documents` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `DocumentVersions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DocumentId` int NOT NULL,
    `VersionNumber` int NOT NULL,
    `FileName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `OriginalFileName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `FilePath` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `ContentType` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `FileSizeBytes` bigint NOT NULL,
    `ChangeNotes` text CHARACTER SET utf8mb4 NULL,
    `IsCurrentVersion` tinyint(1) NOT NULL,
    `UploadedById` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `UploadedAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_DocumentVersions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DocumentVersions_AspNetUsers_UploadedById` FOREIGN KEY (`UploadedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_DocumentVersions_Documents_DocumentId` FOREIGN KEY (`DocumentId`) REFERENCES `Documents` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `InvoiceHcmRefFiles` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ScheduleId` int NOT NULL,
    `MonthYear` varchar(7) CHARACTER SET utf8mb4 NOT NULL,
    `FilePath` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `OriginalFileName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `UploadedAt` datetime(6) NOT NULL,
    `UploadedById` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_InvoiceHcmRefFiles` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InvoiceHcmRefFiles_AspNetUsers_UploadedById` FOREIGN KEY (`UploadedById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_InvoiceHcmRefFiles_InvoiceSchedules_ScheduleId` FOREIGN KEY (`ScheduleId`) REFERENCES `InvoiceSchedules` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `InvoiceRuns` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ScheduleId` int NOT NULL,
    `TriggeredAt` datetime(6) NOT NULL,
    `TriggeredById` varchar(255) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `ErrorMessage` text CHARACTER SET utf8mb4 NULL,
    `FilePath` varchar(500) CHARACTER SET utf8mb4 NULL,
    `FileName` varchar(300) CHARACTER SET utf8mb4 NULL,
    `IppsRowCount` int NOT NULL,
    `HcmRowCount` int NOT NULL,
    `FinalRowCount` int NOT NULL,
    `CompletedAt` datetime(6) NULL,
    CONSTRAINT `PK_InvoiceRuns` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InvoiceRuns_AspNetUsers_TriggeredById` FOREIGN KEY (`TriggeredById`) REFERENCES `AspNetUsers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_InvoiceRuns_InvoiceSchedules_ScheduleId` FOREIGN KEY (`ScheduleId`) REFERENCES `InvoiceSchedules` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `InvoiceScheduleRecipients` (
    `InvoiceScheduleId` int NOT NULL,
    `InvoiceRecipientId` int NOT NULL,
    CONSTRAINT `PK_InvoiceScheduleRecipients` PRIMARY KEY (`InvoiceScheduleId`, `InvoiceRecipientId`),
    CONSTRAINT `FK_InvoiceScheduleRecipients_InvoiceRecipients_InvoiceRecipient~` FOREIGN KEY (`InvoiceRecipientId`) REFERENCES `InvoiceRecipients` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_InvoiceScheduleRecipients_InvoiceSchedules_InvoiceScheduleId` FOREIGN KEY (`InvoiceScheduleId`) REFERENCES `InvoiceSchedules` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE INDEX `IX_AccessRequestComments_AccessRequestId` ON `AccessRequestComments` (`AccessRequestId`);


CREATE INDEX `IX_AccessRequestComments_AuthorId` ON `AccessRequestComments` (`AuthorId`);


CREATE INDEX `IX_AccessRequests_ProvisionedById` ON `AccessRequests` (`ProvisionedById`);


CREATE INDEX `IX_AccessRequests_RequestedById` ON `AccessRequests` (`RequestedById`);


CREATE UNIQUE INDEX `IX_AccessRequests_SerialNumber` ON `AccessRequests` (`SerialNumber`);


CREATE INDEX `IX_AccessReviewEntries_AccessReviewId` ON `AccessReviewEntries` (`AccessReviewId`);


CREATE INDEX `IX_AccessReviewEntries_ReviewedById` ON `AccessReviewEntries` (`ReviewedById`);


CREATE INDEX `IX_AccessReviews_CreatedById` ON `AccessReviews` (`CreatedById`);


CREATE UNIQUE INDEX `IX_AccessSequences_Prefix_Year_Month` ON `AccessSequences` (`Prefix`, `Year`, `Month`);


CREATE UNIQUE INDEX `IX_ApiKeys_KeyHash` ON `ApiKeys` (`KeyHash`);


CREATE INDEX `IX_AppLogs_Source_Level` ON `AppLogs` (`Source`, `Level`);


CREATE INDEX `IX_AppLogs_Timestamp` ON `AppLogs` (`Timestamp`);


CREATE INDEX `IX_ApprovalFlows_EntityType_EntityId` ON `ApprovalFlows` (`EntityType`, `EntityId`);


CREATE INDEX `IX_ApprovalFlows_InitiatedById` ON `ApprovalFlows` (`InitiatedById`);


CREATE INDEX `IX_ApprovalFlows_ReturnedById` ON `ApprovalFlows` (`ReturnedById`);


CREATE INDEX `IX_ApprovalFlows_TemplateId` ON `ApprovalFlows` (`TemplateId`);


CREATE INDEX `IX_ApprovalSteps_ApproverId` ON `ApprovalSteps` (`ApproverId`);


CREATE INDEX `IX_ApprovalSteps_EntityType_EntityId` ON `ApprovalSteps` (`EntityType`, `EntityId`);


CREATE INDEX `IX_ApprovalSteps_FlowId` ON `ApprovalSteps` (`FlowId`);


CREATE INDEX `IX_ApprovalTemplateSteps_ApproverId` ON `ApprovalTemplateSteps` (`ApproverId`);


CREATE INDEX `IX_ApprovalTemplateSteps_TemplateId` ON `ApprovalTemplateSteps` (`TemplateId`);


CREATE INDEX `IX_AspNetRoleClaims_RoleId` ON `AspNetRoleClaims` (`RoleId`);


CREATE UNIQUE INDEX `RoleNameIndex` ON `AspNetRoles` (`NormalizedName`);


CREATE INDEX `IX_AspNetUserClaims_UserId` ON `AspNetUserClaims` (`UserId`);


CREATE INDEX `IX_AspNetUserLogins_UserId` ON `AspNetUserLogins` (`UserId`);


CREATE INDEX `IX_AspNetUserRoles_RoleId` ON `AspNetUserRoles` (`RoleId`);


CREATE INDEX `EmailIndex` ON `AspNetUsers` (`NormalizedEmail`);


CREATE UNIQUE INDEX `UserNameIndex` ON `AspNetUsers` (`NormalizedUserName`);


CREATE INDEX `IX_Attachments_EntityType_EntityId` ON `Attachments` (`EntityType`, `EntityId`);


CREATE INDEX `IX_Attachments_UploadedById` ON `Attachments` (`UploadedById`);


CREATE INDEX `IX_ChangeRequestComments_AuthorId` ON `ChangeRequestComments` (`AuthorId`);


CREATE INDEX `IX_ChangeRequestComments_ChangeRequestId` ON `ChangeRequestComments` (`ChangeRequestId`);


CREATE INDEX `IX_ChangeRequests_PirCompletedById` ON `ChangeRequests` (`PirCompletedById`);


CREATE INDEX `IX_ChangeRequests_RequestedById` ON `ChangeRequests` (`RequestedById`);


CREATE UNIQUE INDEX `IX_ChangeRequests_SerialNumber` ON `ChangeRequests` (`SerialNumber`);


CREATE UNIQUE INDEX `IX_ChangeRequestSequences_Year_Month` ON `ChangeRequestSequences` (`Year`, `Month`);


CREATE INDEX `IX_DeprovisioningEvents_CompletedById` ON `DeprovisioningEvents` (`CompletedById`);


CREATE INDEX `IX_DeprovisioningEvents_NotifiedById` ON `DeprovisioningEvents` (`NotifiedById`);


CREATE UNIQUE INDEX `IX_DeprovisioningEvents_SerialNumber` ON `DeprovisioningEvents` (`SerialNumber`);


CREATE INDEX `IX_DeprovisioningSystemEntries_DeactivatedById` ON `DeprovisioningSystemEntries` (`DeactivatedById`);


CREATE INDEX `IX_DeprovisioningSystemEntries_DeprovisioningEventId` ON `DeprovisioningSystemEntries` (`DeprovisioningEventId`);


CREATE INDEX `IX_DocumentFolders_CreatedById` ON `DocumentFolders` (`CreatedById`);


CREATE INDEX `IX_DocumentFolders_ParentId` ON `DocumentFolders` (`ParentId`);


CREATE INDEX `IX_DocumentPermissions_DocumentId` ON `DocumentPermissions` (`DocumentId`);


CREATE INDEX `IX_DocumentReviews_DocumentId` ON `DocumentReviews` (`DocumentId`);


CREATE INDEX `IX_DocumentReviews_ReviewedById` ON `DocumentReviews` (`ReviewedById`);


CREATE INDEX `IX_Documents_CreatedById` ON `Documents` (`CreatedById`);


CREATE INDEX `IX_Documents_FolderId` ON `Documents` (`FolderId`);


CREATE INDEX `IX_Documents_LastReviewedById` ON `Documents` (`LastReviewedById`);


CREATE INDEX `IX_Documents_OwnerId` ON `Documents` (`OwnerId`);


CREATE UNIQUE INDEX `IX_Documents_SerialNumber` ON `Documents` (`SerialNumber`);


CREATE INDEX `IX_DocumentVersions_DocumentId` ON `DocumentVersions` (`DocumentId`);


CREATE INDEX `IX_DocumentVersions_UploadedById` ON `DocumentVersions` (`UploadedById`);


CREATE INDEX `IX_FolderPermissions_FolderId` ON `FolderPermissions` (`FolderId`);


CREATE UNIQUE INDEX `IX_IncidentDocuments_IncidentId_DocumentId` ON `IncidentDocuments` (`IncidentId`, `DocumentId`);


CREATE INDEX `IX_IncidentDocuments_LinkedById` ON `IncidentDocuments` (`LinkedById`);


CREATE INDEX `IX_Incidents_AssignedToId` ON `Incidents` (`AssignedToId`);


CREATE INDEX `IX_Incidents_ReportedById` ON `Incidents` (`ReportedById`);


CREATE INDEX `IX_IncidentUpdates_AuthorId` ON `IncidentUpdates` (`AuthorId`);


CREATE INDEX `IX_IncidentUpdates_IncidentId` ON `IncidentUpdates` (`IncidentId`);


CREATE UNIQUE INDEX `IX_InvoiceHcmRefFiles_ScheduleId_MonthYear` ON `InvoiceHcmRefFiles` (`ScheduleId`, `MonthYear`);


CREATE INDEX `IX_InvoiceHcmRefFiles_UploadedById` ON `InvoiceHcmRefFiles` (`UploadedById`);


CREATE INDEX `IX_InvoiceLenders_CreatedById` ON `InvoiceLenders` (`CreatedById`);


CREATE INDEX `IX_InvoiceRecipients_CreatedById` ON `InvoiceRecipients` (`CreatedById`);


CREATE INDEX `IX_InvoiceRuns_ScheduleId` ON `InvoiceRuns` (`ScheduleId`);


CREATE INDEX `IX_InvoiceRuns_TriggeredById` ON `InvoiceRuns` (`TriggeredById`);


CREATE INDEX `IX_InvoiceScheduleRecipients_InvoiceRecipientId` ON `InvoiceScheduleRecipients` (`InvoiceRecipientId`);


CREATE INDEX `IX_InvoiceSchedules_CreatedById` ON `InvoiceSchedules` (`CreatedById`);


CREATE INDEX `IX_InvoiceSchedules_LenderId` ON `InvoiceSchedules` (`LenderId`);


CREATE INDEX `IX_ServerRoomAccessComments_AuthorId` ON `ServerRoomAccessComments` (`AuthorId`);


CREATE INDEX `IX_ServerRoomAccessComments_ServerRoomAccessRequestId` ON `ServerRoomAccessComments` (`ServerRoomAccessRequestId`);


CREATE INDEX `IX_ServerRoomAccessRequests_RequestedById` ON `ServerRoomAccessRequests` (`RequestedById`);


CREATE UNIQUE INDEX `IX_ServerRoomAccessRequests_SerialNumber` ON `ServerRoomAccessRequests` (`SerialNumber`);


