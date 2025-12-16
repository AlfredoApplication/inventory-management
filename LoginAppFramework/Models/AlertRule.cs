namespace LoginAppFramework
{
    public class AlertRule
    {
        public int Id { get; set; }
        public string RuleName { get; set; }
        public int AlertType { get; set; }
        public int TargetType { get; set; }
        public bool IsEnabled { get; set; }
        public decimal? ThresholdValue { get; set; }
        public string AlertTitle { get; set; }
        public string AlertMessageTemplate { get; set; }
    }
}