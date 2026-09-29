# Inventory Management

Windows desktop inventory management application built with WPF, .NET 8 and SQL Server.

## Main capabilities

- asset inventory and assignment tracking
- worker synchronization and worker/asset management
- lifecycle and depreciation reporting
- Excel import/export
- barcode printing
- assignment and asset audit history
- audit-based restore for deleted assets
- role-based Admin / ReadOnly access
- PDF and dashboard reporting

## Technology

- .NET 8 / WPF
- Entity Framework Core + SQL Server
- MahApps.Metro
- ClosedXML
- QuestPDF
- LiveCharts
- ZXing.Net

## Database configuration

Database credentials are **not stored in the repository**.

On first launch the application displays the database setup screen. Enter the SQL Server address, SQL username and password there. The application tests the connection and stores the password encrypted with Windows DPAPI under the current user's application-data directory.

The login process and all application data access use that configured connection.

> If credentials have ever been committed to Git history, removing them from the latest file is not sufficient. Rotate those database credentials and, when appropriate, rewrite repository history.

## Build

From the repository root:

```powershell
dotnet restore LoginAppFramework.sln
dotnet build LoginAppFramework.sln --configuration Release --no-restore
```

## Tests

```powershell
dotnet test LoginAppFramework.sln --configuration Release --no-build
```

The test project covers critical domain behavior including:

- lifecycle semantics for unspecified useful life
- worker assignment / unassignment source-of-truth behavior
- assignment history generation
- audit snapshot parsing
- password hashing and verification
- Excel import parsing
- connection-settings secret serialization
- service-level authorization guards

GitHub Actions runs restore, build and tests for pushes and pull requests.

## Architecture

The application is being organized around the following flow:

```text
WPF View
   |
ViewModel
   |
Application Service
   |
AppData cache / DataAccess
   |
EF Core / SQL Server
```

Key application services include:

- `AssetService`
- `WorkerService`
- `AuditService`
- `UserService`
- `AssetExcelService`
- `AuthorizationService`

`WorkerId + Worker` is the application source of truth for asset ownership. Older personnel columns remain mapped only as database/audit compatibility snapshots until a dedicated schema migration removes them.

## Cache behavior

`AppData.LoadAllData()` is reserved for startup/recovery. Normal CRUD operations use targeted cache refreshes instead of reloading all assets, workers, lookup values and category data.

## Security notes

- SQL credentials are stored locally with DPAPI, not as plaintext repository configuration.
- mutation services enforce permissions in addition to UI button restrictions.
- audit records are treated as immutable.
- deleted-asset restore does not delete the source audit record.
- ReadOnly users should not be granted mutation access through application services.

## Pull request

The current stabilization/refactor work is tracked in PR #1.
