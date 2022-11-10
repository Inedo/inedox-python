using System.Text.Json.Serialization;

namespace Inedo.Extensions.Python.Operations
{
    public sealed class TestEvent
    {
        public EventType Type { get; set; }

        [JsonIgnore]
        public DateTimeOffset NowTime => DateTimeOffset.UnixEpoch.AddSeconds(this.Now);

        // epoch time (seconds since unix epoch)
        public double Now { get; set; }
        // monotonic time (seconds since arbitrary time, does not change when system clock is altered)
        public double Time { get; set; }

        // for Skip
        public string Message { get; set; }

        // for StopCase
        public string Output { get; set; }
        public string Error { get; set; }

        // for Error, Failure, and ExpectedFailure
        public string Err { get; set; }

        // for every type except StartSuite and StopSuite
        public TestCaseID Test { get; set; }
    }
}
