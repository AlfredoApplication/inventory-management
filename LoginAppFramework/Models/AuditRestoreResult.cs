namespace LoginAppFramework
{
    public sealed class AuditRestoreResult
    {
        public bool Success { get; init; }
        public string Message { get; init; }
        public int? RestoredAssetId { get; init; }

        public static AuditRestoreResult Ok(int assetId, string message)
            => new() { Success = true, RestoredAssetId = assetId, Message = message };

        public static AuditRestoreResult Fail(string message)
            => new() { Success = false, Message = message };
    }
}
