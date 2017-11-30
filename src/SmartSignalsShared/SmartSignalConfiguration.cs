namespace Microsoft.Azure.Monitoring.SmartSignals.Shared
{
    using NCrontab;

    /// <summary>
    /// Holds a smart signal configuration
    /// </summary>
    public class SmartSignalConfiguration
    {
        public string SignalId { get; set; }

        public ResourceType ResourceType { get; set; }

        public CrontabSchedule Schedule { get; set; }
    }
}