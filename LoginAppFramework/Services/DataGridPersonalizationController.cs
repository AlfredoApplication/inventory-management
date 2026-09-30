using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace LoginAppFramework
{
    public sealed class DataGridPersonalizationController
    {
        private readonly DataGrid _grid;
        private readonly Button _columnsButton;
        private readonly string _layoutKey;
        private readonly IReadOnlyDictionary<string, string> _labels;
        private readonly List<GridColumnState> _defaultState;
        private readonly ContextMenu _menu;
        private bool _applying;

        public DataGridPersonalizationController(
            DataGrid grid,
            Button columnsButton,
            string layoutKey,
            IReadOnlyDictionary<string, string> labels)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _columnsButton = columnsButton ?? throw new ArgumentNullException(nameof(columnsButton));
            _layoutKey = layoutKey ?? throw new ArgumentNullException(nameof(layoutKey));
            _labels = labels ?? new Dictionary<string, string>();

            _defaultState = CaptureState(useActualWidth: false);
            ApplyStoredState();

            _menu = BuildMenu();
            _columnsButton.ContextMenu = _menu;
            _columnsButton.Click += ColumnsButton_Click;

            _grid.ColumnReordered += (_, _) => SaveCurrentState();
            _grid.Unloaded += (_, _) => SaveCurrentState();
            _grid.AddHandler(
                Thumb.DragCompletedEvent,
                new DragCompletedEventHandler((_, _) => SaveCurrentState()),
                true);
        }

        private void ColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshMenuChecks();
            _menu.PlacementTarget = _columnsButton;
            _menu.Placement = PlacementMode.Bottom;
            _menu.IsOpen = true;
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();

            foreach (var column in CustomizableColumns())
            {
                string key = GetColumnKey(column);
                if (key == null)
                    continue;

                var item = new MenuItem
                {
                    Header = GetLabel(key),
                    IsCheckable = true,
                    IsChecked = column.Visibility == Visibility.Visible,
                    Tag = key,
                    StaysOpenOnClick = true
                };

                item.Click += ColumnVisibilityItem_Click;
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());

            var reset = new MenuItem
            {
                Header = "Standart görünüşü bərpa et"
            };
            reset.Click += (_, _) =>
            {
                ApplyState(_defaultState);
                UiPreferenceStore.RemoveGridLayout(_layoutKey);
                RefreshMenuChecks();

                NotificationService.Info(
                    Window.GetWindow(_grid),
                    "Sütun görünüşü standart vəziyyətə qaytarıldı.",
                    title: "Sütunlar");
            };
            menu.Items.Add(reset);

            return menu;
        }

        private void ColumnVisibilityItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item || item.Tag is not string key)
                return;

            var column = FindColumn(key);
            if (column == null)
                return;

            bool wantsVisible = item.IsChecked;

            if (!wantsVisible &&
                CustomizableColumns().Count(c => c.Visibility == Visibility.Visible) <= 1)
            {
                item.IsChecked = true;
                NotificationService.Info(
                    Window.GetWindow(_grid),
                    "Ən azı bir məlumat sütunu görünməlidir.",
                    title: "Sütunlar");
                return;
            }

            column.Visibility =
                wantsVisible ? Visibility.Visible : Visibility.Collapsed;

            SaveCurrentState();
        }

        private void ApplyStoredState()
        {
            var stored = UiPreferenceStore.LoadGridLayout(_layoutKey);
            if (stored.Count == 0)
                return;

            ApplyState(stored);
        }

        private void ApplyState(IReadOnlyCollection<GridColumnState> states)
        {
            _applying = true;
            try
            {
                foreach (var state in states)
                {
                    var column = FindColumn(state.Key);
                    if (column == null)
                        continue;

                    column.Visibility =
                        state.IsVisible ? Visibility.Visible : Visibility.Collapsed;

                    if (state.WidthValue > 0)
                    {
                        var unit = Enum.TryParse(
                            state.WidthUnitType,
                            true,
                            out DataGridLengthUnitType parsed)
                                ? parsed
                                : DataGridLengthUnitType.Pixel;

                        double value = state.WidthValue;
                        if (unit == DataGridLengthUnitType.Pixel)
                        {
                            value = Math.Max(column.MinWidth, value);
                            if (!double.IsInfinity(column.MaxWidth))
                                value = Math.Min(column.MaxWidth, value);
                        }

                        column.Width = new DataGridLength(value, unit);
                    }
                }

                foreach (var state in states.OrderBy(s => s.DisplayIndex))
                {
                    var column = FindColumn(state.Key);
                    if (column == null)
                        continue;

                    int maxIndex = Math.Max(0, _grid.Columns.Count - 1);
                    int firstCustomizableIndex =
                        _grid.Columns.Count(column => GetColumnKey(column) == null);
                    int target = Math.Clamp(
                        state.DisplayIndex,
                        Math.Min(firstCustomizableIndex, maxIndex),
                        maxIndex);

                    try
                    {
                        column.DisplayIndex = target;
                    }
                    catch (ArgumentException)
                    {
                        // Ignore stale/invalid indexes from older layouts.
                    }
                }

                if (!CustomizableColumns().Any(c => c.Visibility == Visibility.Visible))
                {
                    var first = CustomizableColumns().FirstOrDefault();
                    if (first != null)
                        first.Visibility = Visibility.Visible;
                }
            }
            finally
            {
                _applying = false;
            }
        }

        private void SaveCurrentState()
        {
            if (_applying)
                return;

            UiPreferenceStore.SaveGridLayout(
                _layoutKey,
                CaptureState(useActualWidth: true));
        }

        private List<GridColumnState> CaptureState(bool useActualWidth)
        {
            return CustomizableColumns()
                .Select(column =>
                {
                    string key = GetColumnKey(column);
                    var width = column.Width;

                    double widthValue;
                    string widthUnit;

                    if (useActualWidth && column.ActualWidth > 0)
                    {
                        widthValue = column.ActualWidth;
                        widthUnit = DataGridLengthUnitType.Pixel.ToString();
                    }
                    else
                    {
                        widthValue = width.Value;
                        widthUnit = width.UnitType.ToString();
                    }

                    return new GridColumnState
                    {
                        Key = key,
                        DisplayIndex = column.DisplayIndex,
                        WidthValue = widthValue,
                        WidthUnitType = widthUnit,
                        IsVisible = column.Visibility == Visibility.Visible
                    };
                })
                .Where(state => !string.IsNullOrWhiteSpace(state.Key))
                .ToList();
        }

        private IEnumerable<DataGridColumn> CustomizableColumns()
            => _grid.Columns.Where(column => GetColumnKey(column) != null);

        private DataGridColumn FindColumn(string key)
            => CustomizableColumns().FirstOrDefault(
                column => string.Equals(
                    GetColumnKey(column),
                    key,
                    StringComparison.OrdinalIgnoreCase));

        private string GetLabel(string key)
            => _labels.TryGetValue(key, out string label)
                ? label
                : key;

        private static string GetColumnKey(DataGridColumn column)
        {
            if (!string.IsNullOrWhiteSpace(column.SortMemberPath))
                return column.SortMemberPath;

            if (column is DataGridBoundColumn boundColumn &&
                boundColumn.Binding is Binding binding &&
                binding.Path != null &&
                !string.IsNullOrWhiteSpace(binding.Path.Path))
            {
                return binding.Path.Path;
            }

            return null;
        }

        private void RefreshMenuChecks()
        {
            foreach (var item in _menu.Items.OfType<MenuItem>())
            {
                if (item.Tag is not string key)
                    continue;

                var column = FindColumn(key);
                if (column != null)
                    item.IsChecked = column.Visibility == Visibility.Visible;
            }
        }
    }

    public sealed class GridColumnState
    {
        public string Key { get; set; }
        public int DisplayIndex { get; set; }
        public double WidthValue { get; set; }
        public string WidthUnitType { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    internal static class UiPreferenceStore
    {
        private static readonly object Sync = new();

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InventoryManagement",
            "ui-preferences.json");

        private sealed class PreferenceRoot
        {
            public Dictionary<string, Dictionary<string, List<GridColumnState>>> Users { get; set; }
                = new(StringComparer.OrdinalIgnoreCase);
        }

        public static List<GridColumnState> LoadGridLayout(string layoutKey)
        {
            lock (Sync)
            {
                var root = LoadRoot();
                string userKey = CurrentUserKey();

                if (root.Users.TryGetValue(userKey, out var layouts) &&
                    layouts.TryGetValue(layoutKey, out var state))
                {
                    return state ?? new List<GridColumnState>();
                }

                return new List<GridColumnState>();
            }
        }

        public static void SaveGridLayout(
            string layoutKey,
            List<GridColumnState> state)
        {
            lock (Sync)
            {
                try
                {
                    var root = LoadRoot();
                    string userKey = CurrentUserKey();

                    if (!root.Users.TryGetValue(userKey, out var layouts))
                    {
                        layouts = new Dictionary<string, List<GridColumnState>>(
                            StringComparer.OrdinalIgnoreCase);
                        root.Users[userKey] = layouts;
                    }

                    layouts[layoutKey] = state ?? new List<GridColumnState>();
                    SaveRoot(root);
                }
                catch
                {
                    // Preferences are best-effort and must never block the app.
                }
            }
        }

        public static void RemoveGridLayout(string layoutKey)
        {
            lock (Sync)
            {
                try
                {
                    var root = LoadRoot();
                    string userKey = CurrentUserKey();

                    if (root.Users.TryGetValue(userKey, out var layouts))
                        layouts.Remove(layoutKey);

                    SaveRoot(root);
                }
                catch
                {
                    // Preferences are best-effort and must never block the app.
                }
            }
        }

        private static string CurrentUserKey()
            => string.IsNullOrWhiteSpace(SessionManager.CurrentUser?.Username)
                ? "default"
                : SessionManager.CurrentUser.Username.Trim().ToLowerInvariant();

        private static PreferenceRoot LoadRoot()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new PreferenceRoot();

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<PreferenceRoot>(json)
                    ?? new PreferenceRoot();
            }
            catch
            {
                return new PreferenceRoot();
            }
        }

        private static void SaveRoot(PreferenceRoot root)
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(
                root,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(FilePath, json);
        }
    }
}
