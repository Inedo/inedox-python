namespace Inedo.Extensions.Python.Operations
{
    public enum EventType
    {
        StartSuite,
        StopSuite,
        StartCase,
        StopCase,
        Error,
        Failure,
        Success,
        Skip,
        ExpectedFailure,
        UnexpectedSuccess
    }
}
