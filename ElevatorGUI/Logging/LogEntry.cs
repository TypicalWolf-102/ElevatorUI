using System;

namespace ElevatorGUI
{
    public sealed class LogEntry
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string EventType { get; init; } = "";
        public int Floor { get; init; }
        public string Status { get; init; } = "";
        public int? TravelMs { get; init; }
    }
}
