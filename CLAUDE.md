# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**LoginAppFramework** is a WPF desktop application for enterprise asset inventory management. It's written in C# targeting .NET 8.0 with a SQL Server backend. The application manages IT assets, workers, assignments, and lifecycle tracking with features like QR code generation, bulk operations, and comprehensive audit logging.

## Technology Stack

- **Framework**: .NET 8.0 (WPF for Windows Desktop)
- **Database**: SQL Server with Entity Framework Core 9.0.7
- **UI**: WPF with MahApps.Metro for modern UI styling
- **Key Libraries**:
  - ClosedXML (Excel import/export)
  - QRCoder (QR code generation)
  - QuestPDF (PDF report generation)
  - LiveCharts & OxyPlot (data visualization)
  - Entity Framework Core (ORM)

## Building and Running

```bash
# Build the solution
dotnet build LoginAppFramework.sln

# Run in debug mode
dotnet run --project LoginAppFramework/LoginAppFramework.csproj

# Build for release
dotnet build -c Release
```

The application is configured for ClickOnce deployment with desktop shortcut creation.

## Architecture

### Core Data Flow Pattern

1. **SessionManager** handles authentication and maintains current user session
2. **AppData** (static class) serves as an in-memory data cache loaded at login
3. **DataAccess** provides static methods for all database operations
4. **InventoryDbContext** is the EF Core DbContext, instantiated per-operation with connection strings
5. **ConnectionManager** manages encrypted database connection settings stored in `%AppData%/InventoryApp`

### Key Architectural Patterns

**Centralized Data Cache Pattern**:
- All data is loaded once at login via `AppData.LoadAllData()`
- Changes flow through AppData methods like `SaveAndRefreshAsset()` which update DB then reload cache
- This ensures all windows always see consistent data without manual refresh logic

**Worker-Asset Relationship**:
- Assets have a foreign key `WorkerId` linking to the Workers table
- When an asset is assigned to a worker, the system:
  - Sets `asset.WorkerId = worker.Id`
  - Updates derived properties (`TehkimOlunanEmekdas`, `Vezifesi`, `BolmeShobeDepartment`) from worker data
  - Sets `Status = "İstifadədədir"` (In Use)
  - Creates an `AssignmentHistoryEntry` record
- Workers are synchronized from a remote HR database (`SRV50_29.MikroDB_V16_01`) via `SynchronizeWorkersFromRemoteAsync()`

**Session Context for Audit Logging**:
- Database trigger `Trigger_Assets_AuditLog` captures all asset changes to `Assets_upd_del` table
- Before save operations, `sp_set_session_context` is called to set the current user's name
- The trigger reads this context to log who made changes

### Critical Code Patterns

**Always use SessionManager.CurrentUserConnectionString for database operations**:
```csharp
using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
```

**Navigation pattern** (via NavigationManager):
```csharp
await NavigationManager.GoToDashboard();
await NavigationManager.GoToAssetWindow();
```

**Saving assets with audit trail**:
```csharp
AppData.SaveAndRefreshAsset(asset); // Single asset
AppData.BulkSaveAndRefreshAssets(assets); // Multiple assets
```

**Worker synchronization from remote DB**:
```csharp
await DataAccess.SynchronizeWorkersFromRemoteAsync();
```

### Database Schema Highlights

**Core Tables**:
- `Assets` - Main inventory table with JSON columns for CustomFields and MaintenanceHistory
- `Workers` - Employee data (synchronized from external HR system)
- `AssignmentHistory` - Tracks asset assignment changes
- `AppUsers` - Application users with hashed passwords (PBKDF2)
- `Assets_upd_del` - Audit log of all asset modifications (populated by trigger)
- `L_*` tables - Lookup tables (AssetStatuses, DeviceCategories, Departments, etc.)

**Important**: The Workers table has an `IsActive` flag set to 0 when employees are removed from the HR system.

### ViewModel Pattern

**AssetCheckableViewModel**:
- Wraps `Asset` objects with an `IsChecked` property for DataGrid selection
- Used in AssetWindow for bulk operations (assign, edit, delete, QR code generation)
- Pattern: `var checkedAssets = _allCheckableAssets.Where(vm => vm.IsChecked).Select(vm => vm.Asset).ToList();`

**AssetFilterViewModel**:
- Manages all filtering logic for the AssetWindow
- Supports hierarchical category filtering with parent-child relationships
- Includes search text, status, department, and date range filters
- **Column-Specific Filters**: Dictionary of `ColumnFilterViewModel` for Excel-like filtering on individual columns
- `FilterChanged` event triggers `ApplyFilters()` in AssetWindow

**ColumnFilterViewModel**:
- Manages filter state for individual DataGrid columns
- Supports search within filter values
- Tracks selected/unselected items with `ColumnFilterItem` collection
- `HasActiveFilters` property indicates if any filters are applied
- Pattern: `var selectedValues = filterViewModel.GetSelectedValues();`

**DashboardViewModel**:
- Loads aggregated statistics and chart data from cached assets/workers
- Provides data binding for LiveCharts visualizations

### Window Hierarchy

**MainWindow** → (Login) → **DashboardWindow** → Opens:
- AssetWindow (main asset management)
- WorkerListWindow (employee management)
- HistoryLogWindow (unified history view)
- LifecycleReportWindow (asset lifecycle/depreciation)
- ReportsWindow (various reports)
- UserManagementWindow (app user administration)

### Security & Encryption

- **Password Hashing**: Uses PBKDF2 (100,000 iterations) via `PasswordHasher.cs`
- **Connection String Encryption**: SQL passwords are encrypted using `EncryptionHelper.cs` before storing in `settings.json`
- **Service Account Pattern**: App connects to SQL Server using a dedicated service account (from appsettings.json), not per-user accounts

### Configuration Files

**appsettings.json** (in project root and copied to output):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=InventoryDB;User ID=...;Password=...;",
    "RemoteHRConnection": "Server=...;Database=MikroDB_V16_01;User ID=...;Password=..."
  }
}
```

**settings.json** (user AppData folder):
- Created during initial setup via ConnectionManager
- Stores encrypted SQL credentials for dynamic connection

### Localization

The application uses Azerbaijani language (az-Latn-AZ culture) for:
- UI strings (hardcoded in XAML and code)
- Date formatting
- Status values (e.g., "İstifadədədir", "Anbarda və İşlək")

### Important Business Logic

**Asset Status Auto-Update**:
- When `WorkerId` is set, status automatically becomes "İstifadədədir"
- When `WorkerId` is null, status reverts to "Anbarda və İşlək" (if was previously in use)
- This logic runs on every data load in `AppData.LoadAllData()`

**Bulk Operations**:
- **Bulk Edit**: Updated to properly handle worker assignments with validation
  - Skips status changes for assigned assets
  - Skips worker changes for already-assigned assets
  - Creates assignment history entries for worker assignments
  - Optimized to use `BulkSaveAndRefreshAssets()` for better performance
- **Bulk Assign**: Optimized to use batch save instead of loop
  - Processes all asset updates in memory first
  - Single `BulkSaveAndRefreshAssets()` call for all assets
  - Changed from O(n) database calls to O(1) for improved performance
- All bulk operations log changes through the same audit mechanism

**Category Hierarchy**:
- Device categories support parent-child relationships via `ParentId`
- Custom fields can be defined per category
- Icons and default lifecycle years are configurable per category

**Asset Lifecycle/Depreciation**:
- Calculated properties: `AnnualDepreciation`, `CurrentValue`, `EndOfLifeDate`
- Formula: `CurrentValue = PurchaseCost - (AnnualDepreciation * AgeInYears)`
- Supports custom useful life per asset or category defaults

## Common Development Commands

### Entity Framework Migrations

When modifying database models, use EF Core migrations:

```bash
# Add a new migration
dotnet ef migrations add MigrationName --project LoginAppFramework

# Update database
dotnet ef database update --project LoginAppFramework

# Generate SQL script
dotnet ef migrations script --project LoginAppFramework
```

### Testing Database Connectivity

The app includes a built-in connection test in the setup flow. For manual testing:
```csharp
var settings = new ConnectionSettings { ServerAddress = "...", SqlUsername = "...", SqlPassword = "..." };
bool success = await ConnectionManager.TestConnection(settings);
```

## Code Style Notes

- Use explicit types over `var` for clarity (except LINQ queries and obvious cases)
- Database operations should be wrapped in using statements
- Always refresh `AppData` after modifications: `AppData.LoadAllData()` or use convenience methods
- WPF controls follow naming pattern: `ControlTypeDescriptiveName` (e.g., `AssetDataGrid`, `AddAssetButton`)
- ViewModels typically don't inherit from base classes; `INotifyPropertyChanged` is implemented directly where needed

## Working with the Codebase

**When adding new asset properties**:
1. Add to `Asset.cs` model
2. Update `DataAccess` save/load methods if needed
3. Add EF migration
4. Update Excel import/export mappings in `AssetWindow.xaml.cs`
5. Update UI in `AddEditAssetWindow.xaml`

**When adding new lookup tables**:
1. Create model in `/Models` folder
2. Add `DbSet<T>` to `InventoryDbContext`
3. Map table name in `OnModelCreating` if different from class name
4. Add caching in `AppData` if frequently accessed

**When adding new windows**:
1. Create window with `.xaml` and `.xaml.cs`
2. Add navigation method to `NavigationManager` if it exists (verify first)
3. Follow the menu overlay pattern from `DashboardWindow` for consistency
4. Wire up session user display with `UpdateUserDisplay()`

## Recent Changes (2025)

### Excel-Style Column Filtering (Added)
- **New Files**:
  - `ColumnFilterControl.xaml` / `.xaml.cs` - Reusable WPF UserControl for column filtering UI
  - `Models/ColumnFilterViewModel.cs` - ViewModel managing filter state and search
- **Modified Files**:
  - `AssetWindow.xaml` - Added filter controls to 7 DataGrid column headers (Vəsaitin Kodu, Vəsaitin Adı, Kateqoriya, Təhkim Olunan Əməkdaş, Bölmə/Şöbə/Departament, Yerləşmə Yeri, Ərazi)
  - `AssetWindow.xaml.cs` - Added `InitializeColumnFilters()` method and filter application logic
  - `AssetFilterViewModel.cs` - Added `ColumnFilters` dictionary property
- **Features**:
  - Filter icon changes color when filters are active
  - Search box within filter popup for finding values
  - "Select All" / "Clear All" buttons
  - Filters persist until explicitly cleared or changed
  - Integrates seamlessly with existing left-panel filters

### Bulk Operations Improvements
- **BulkEditButton_Click** (AssetWindow.xaml.cs):
  - Fixed worker assignment handling with proper validation
  - Added assignment history entry creation
  - Separated tracking for skipped items (status changes vs worker changes)
  - Improved error messages and user feedback
- **BulkAssignButton_Click** (AssetWindow.xaml.cs):
  - Optimized from loop with individual saves to single batch save
  - Performance improved from O(n) to O(1) database operations
  - All assets prepared in memory before single `BulkSaveAndRefreshAssets()` call

### Bug Fixes (January 2025)
- **ReportGenerator.cs:186**: Fixed typo `private2` → `private` that was causing IDE1007 and CS1585 compilation errors
- **Namespace Resolution**: Fixed XDG0008 errors for `ColumnFilterControl`, `InvertedBooleanConverter`, and `AssetDetailControl` by performing clean rebuild
  - All controls exist in the correct `LoginAppFramework` namespace
  - IDE cache was stale and required rebuild to recognize newly created files
- **Bulk Assign Reassignment** (AssetWindow.xaml.cs:94-146): Removed restriction preventing reassignment of already-assigned assets
  - Removed blocking validation check that showed "bunlar artıq təhkim olunub" error message
  - Now supports full reassignment workflow in bulk assign operation
  - Properly tracks assignment history with `AssignmentAction.Reassigned` for already-assigned assets
  - Determines `isReassignment` by checking `asset.WorkerId.HasValue` before assignment
  - Creates appropriate history entries for both new assignments and reassignments
- **Bulk Edit Worker Assignment** (DataAccess.cs:218-248): Fixed worker assignment changes not persisting to database
  - Changed `BulkSaveAssets()` from using `Attach()` + `EntityState.Modified` to proper entity loading
  - Now loads original asset from database with `Include(a => a.History)` to properly track changes
  - Uses `context.Entry(originalAsset).CurrentValues.SetValues(asset)` to update scalar properties
  - Explicitly sets `originalAsset.WorkerId = asset.WorkerId` to ensure foreign key is updated
  - Properly handles history entries by checking `h_ui.Id == 0` and adding new entries to tracked entity
  - Mirrors the pattern used in single `SaveAsset()` method for consistency
  - Fixes issue where Entity Framework was confused by detached Worker navigation property
- **Bulk Edit Reassignment** (AssetWindow.xaml.cs:438-491): Improved worker assignment handling in bulk edit
  - Now supports full reassignment workflow (assign, unassign, reassign)
  - Properly tracks assignment history with `AssignmentAction.Reassigned`
  - Creates appropriate history entries for all scenarios (Assigned, Unassigned, Reassigned)
  - Works correctly now that `BulkSaveAssets()` properly persists worker assignments
- **Excel-like Column Filters** (AssetWindow.xaml.cs:622-668): Fixed filters not applying correctly
  - Added `HasActiveFilters` check before applying each column filter
  - Ensures filters only apply when user has manually unchecked items
  - Fixed `RefreshDataAndSelection` to reinitialize column filters after data reload
  - Column filters now properly refresh with updated data after bulk operations
