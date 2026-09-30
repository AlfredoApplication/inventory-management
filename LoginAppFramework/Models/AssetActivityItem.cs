using System;

namespace LoginAppFramework
{
    public enum AssetActivityKind
    {
        Purchase,
        Assigned,
        Unassigned,
        Reassigned,
        Maintenance
    }

    public sealed class AssetActivityItem
    {
        public DateTime Date { get; init; }
        public AssetActivityKind Kind { get; init; }
        public AppIconKind Icon { get; init; }
        public string Title { get; init; }
        public string Description { get; init; }
        public string Actor { get; init; }
    }
}
