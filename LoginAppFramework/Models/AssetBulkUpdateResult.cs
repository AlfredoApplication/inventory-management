using System.Collections.Generic;

namespace LoginAppFramework
{
    public sealed class AssetBulkUpdateResult
    {
        public int UpdatedCount { get; init; }
        public IReadOnlyList<Asset> SkippedStatusAssets { get; init; } = new List<Asset>();
        public bool HasChanges => UpdatedCount > 0;
    }
}
