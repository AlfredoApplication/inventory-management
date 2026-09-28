IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AppUsers] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(max) NULL,
    [PasswordHash] nvarchar(max) NULL,
    [FullName] nvarchar(max) NULL,
    [EmployeeCode] nvarchar(max) NULL,
    [Role] nvarchar(max) NULL,
    CONSTRAINT [PK_AppUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [Assets_upd_del] (
    [Id] int NOT NULL IDENTITY,
    [VesaitinKodu] nvarchar(max) NULL,
    [VesaitinAdi] nvarchar(max) NULL,
    [ITAvadanliqlarininSeriyaNomresi] nvarchar(max) NULL,
    [Kateqoriya] nvarchar(max) NULL,
    [TehkimOlunanEmekdas] nvarchar(max) NULL,
    [Vezifesi] nvarchar(max) NULL,
    [BolmeShobeDepartment] nvarchar(max) NULL,
    [YerleshmeYeri] nvarchar(max) NULL,
    [Erazi] nvarchar(max) NULL,
    [status] nvarchar(max) NULL,
    [ChangeDate] datetime2 NOT NULL,
    [ChangeDetails] nvarchar(max) NULL,
    [ChangedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_Assets_upd_del] PRIMARY KEY ([Id])
);

CREATE TABLE [L_AlertRules] (
    [Id] int NOT NULL IDENTITY,
    [RuleName] nvarchar(max) NULL,
    [AlertType] int NOT NULL,
    [TargetType] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [ThresholdValue] decimal(18,2) NULL,
    [AlertTitle] nvarchar(max) NULL,
    [AlertMessageTemplate] nvarchar(max) NULL,
    CONSTRAINT [PK_L_AlertRules] PRIMARY KEY ([Id])
);

CREATE TABLE [L_AssetStatuses] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    [ColorHexCode] nvarchar(max) NULL,
    CONSTRAINT [PK_L_AssetStatuses] PRIMARY KEY ([Id])
);

CREATE TABLE [L_CategoryCustomFields] (
    [Id] int NOT NULL IDENTITY,
    [CategoryName] nvarchar(max) NULL,
    [FieldName] nvarchar(max) NULL,
    CONSTRAINT [PK_L_CategoryCustomFields] PRIMARY KEY ([Id])
);

CREATE TABLE [L_Departments] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    CONSTRAINT [PK_L_Departments] PRIMARY KEY ([Id])
);

CREATE TABLE [L_DeviceCategories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    [Icon] nvarchar(max) NULL,
    [DefaultUsefulLifeYears] int NULL,
    [ParentId] int NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_L_DeviceCategories] PRIMARY KEY ([Id])
);

CREATE TABLE [Suppliers] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    [ContactPerson] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [Website] nvarchar(max) NULL,
    CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
);

CREATE TABLE [UnifiedHistoryEntries] (
    [Timestamp] datetime2 NOT NULL,
    [Description] nvarchar(max) NULL,
    [ChangedBy] nvarchar(max) NULL,
    [Status] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL
);

CREATE TABLE [Workers] (
    [Id] int NOT NULL IDENTITY,
    [per_kod] nvarchar(max) NULL,
    [per_adiper_soyadi] nvarchar(max) NULL,
    [pgk_gorev_adi] nvarchar(max) NULL,
    [pdp_adi] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Workers] PRIMARY KEY ([Id])
);

CREATE TABLE [Assets] (
    [Id] int NOT NULL IDENTITY,
    [VesaitinKodu] nvarchar(max) NULL,
    [VesaitinAdi] nvarchar(max) NULL,
    [ITAvadanliqlarininSeriyaNomresi] nvarchar(max) NULL,
    [Kateqoriya] nvarchar(max) NULL,
    [TehkimOlunanEmekdas] nvarchar(max) NULL,
    [Vezifesi] nvarchar(max) NULL,
    [BolmeShobeDepartment] nvarchar(max) NULL,
    [YerleshmeYeri] nvarchar(max) NULL,
    [Erazi] nvarchar(max) NULL,
    [Status] nvarchar(max) NULL,
    [PurchaseCost] decimal(18,2) NOT NULL,
    [PurchaseDate] datetime2 NOT NULL,
    [UsefulLifeInYears] int NOT NULL,
    [Supplier] nvarchar(max) NULL,
    [WarrantyExpirationDate] datetime2 NOT NULL,
    [WorkerId] int NULL,
    [LifecycleStatus] int NOT NULL,
    [CustomFields] nvarchar(max) NULL,
    [MaintenanceHistory] nvarchar(max) NULL,
    CONSTRAINT [PK_Assets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Assets_Workers_WorkerId] FOREIGN KEY ([WorkerId]) REFERENCES [Workers] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [AssignmentHistory] (
    [Id] int NOT NULL IDENTITY,
    [AssetId] int NOT NULL,
    [Action] int NOT NULL,
    [FromWorkerName] nvarchar(max) NULL,
    [ToWorkerName] nvarchar(max) NULL,
    [ChangedBy] nvarchar(max) NULL,
    [ChangeDate] datetime2 NOT NULL,
    CONSTRAINT [PK_AssignmentHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssignmentHistory_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Assets_WorkerId] ON [Assets] ([WorkerId]);

CREATE INDEX [IX_AssignmentHistory_AssetId] ON [AssignmentHistory] ([AssetId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251224073019_AddUserRoleColumn', N'9.0.7');

COMMIT;
GO

