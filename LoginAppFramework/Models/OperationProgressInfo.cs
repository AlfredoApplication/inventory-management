namespace LoginAppFramework
{
    public sealed class OperationProgressInfo
    {
        public OperationProgressInfo(
            string message,
            int current,
            int total,
            string detail = null)
        {
            Message = message;
            Current = current;
            Total = total;
            Detail = detail;
        }

        public string Message { get; }
        public int Current { get; }
        public int Total { get; }
        public string Detail { get; }

        public double Percent =>
            Total <= 0
                ? 0
                : System.Math.Clamp(Current * 100d / Total, 0, 100);
    }
}
