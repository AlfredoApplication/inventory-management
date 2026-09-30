namespace LoginAppFramework
{
    public sealed class AuditDiffItem
    {
        public string Field { get; init; }
        public string OldValue { get; init; }
        public string NewValue { get; init; }
    }
}
