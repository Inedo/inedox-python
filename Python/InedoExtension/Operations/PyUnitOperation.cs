using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Newtonsoft.Json;

namespace Inedo.Extensions.Python.Operations
{
    [ScriptAlias("Execute-PyUnit")]
    [DisplayName("Execute Python Unit Tests")]
    [Description("Executes Python unit tests.")]
    [Tag("unit-tests")]
    [AppliesTo(InedoProduct.BuildMaster)]
    public sealed class PyUnitOperation : PythonOperationBase
    {
        private List<TestEvent> Events { get; } = new List<TestEvent>();

        [ScriptAlias("Arguments")]
        [DefaultValue("discover")]
        public string Arguments { get; set; } = "discover";
        [ScriptAlias("Verbose")]
        [DefaultValue(true)]
        public bool Verbose { get; set; } = true;
        [ScriptAlias("FailFast")]
        [DefaultValue(false)]
        public bool FailFast { get; set; } = false;
        [ScriptAlias("RecordOutput")]
        [DefaultValue(true)]
        public bool RecordOutput { get; set; } = true;

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var recorder = await context.TryGetServiceAsync<IUnitTestRecorder>() ?? throw new ExecutionFailureException("This operation requires a unit test recorder.");

            await this.RunTestsAsync(context);

            foreach (var test in this.Events.Where(e => e.Test != null).GroupBy(e => e.Test))
            {
                await recorder.RecordUnitTestAsync(
                    groupName: test.Key.Group,
                    testName: test.Key.Name,
                    testStatus: getStatus(test),
                    testResult: getTestLog(test),
                    startTime: getStartTime(test),
                    duration: getDuration(test)
                );
            }

            UnitTestStatus getStatus(IGrouping<TestCaseID, TestEvent> test)
            {
                foreach (var e in test)
                {
                    switch (e.Type)
                    {
                        case EventType.Success:
                        case EventType.ExpectedFailure:
                            return UnitTestStatus.Passed;
                        case EventType.Error:
                        case EventType.Failure:
                        case EventType.UnexpectedSuccess:
                            return UnitTestStatus.Failed;
                        case EventType.Skip:
                            return UnitTestStatus.Inconclusive;
                    }
                }

                return UnitTestStatus.Inconclusive;
            }

            string getTestLog(IGrouping<TestCaseID, TestEvent> test)
            {
                var end = test.FirstOrDefault(e => e.Type == EventType.StopCase);
                var result = (test.FirstOrDefault(e => e.Type == EventType.Error || e.Type == EventType.Failure || e.Type == EventType.UnexpectedSuccess || e.Type == EventType.Skip)
                    ?? test.FirstOrDefault(e => e.Type == EventType.Success || e.Type == EventType.ExpectedFailure))?.Type;

                var stdout = end?.Output;
                var stderr = end?.Error;
                var skipReason = test.FirstOrDefault(e => e.Type == EventType.Skip)?.Message;
                var exceptions = test.Select(e => e.Err).Where(e => e != null).ToArray();

                return "Test: " + test.Key.ID + AH.ConcatNE("\n", test.Key.Desc) +
                    "\n\nResult: " + AH.CoalesceString(result, "Unknown") + AH.ConcatNE(" (", skipReason, ")") +
                    AH.ConcatNE("\n\nOutput:\n", stdout) + AH.ConcatNE("\n\nError:\n", stderr) +
                    (exceptions.Any() ? "\n\nExceptions:\n\n" + string.Join("\n\n", exceptions) : string.Empty) +
                    "\n";
            }

            DateTimeOffset getStartTime(IGrouping<TestCaseID, TestEvent> test)
            {
                return (test.FirstOrDefault(e => e.Type == EventType.StartCase) ?? test.First()).NowTime;
            }

            TimeSpan getDuration(IGrouping<TestCaseID, TestEvent> test)
            {
                var start = (test.FirstOrDefault(e => e.Type == EventType.StartCase) ?? test.First()).Time;
                var end = (test.FirstOrDefault(e => e.Type == EventType.StopCase) ?? test.Last()).Time;
                return TimeSpan.FromSeconds(end - start);
            }
        }

        private async Task RunTestsAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();

            var scriptsDir = fileOps.CombinePath(await fileOps.GetBaseWorkingDirectoryAsync(), "scripts");
            await fileOps.CreateDirectoryAsync(scriptsDir);
            var runnerFileName = fileOps.CombinePath(scriptsDir, $"BuildMasterTestRunner_{Guid.NewGuid().ToString("N")}.py");
            using (var fileStream = await fileOps.OpenFileAsync(runnerFileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            using (var stream = typeof(PyUnitOperation).Assembly.GetManifestResourceStream("Inedo.Extensions.Python.BuildMasterTestRunner.py"))
            {
                await stream.CopyToAsync(fileStream);
            }

            try
            {
                var startInfo = new RemoteProcessStartInfo
                {
                    FileName = this.PythonExePath,
                    Arguments = $"{runnerFileName} {this.Arguments}{(this.Verbose ? " -v" : string.Empty)}{(this.FailFast ? " -f" : string.Empty)}{(this.RecordOutput ? " -b" : string.Empty)}",
                    WorkingDirectory = context.WorkingDirectory
                };
                await this.WrapInVirtualEnv(context, startInfo);
                var exit = await this.ExecuteCommandLineAsync(context, startInfo);

                if (exit != 0)
                {
                    this.LogError($"Exited with code {exit}");
                }
            }
            finally
            {
                try { await fileOps.DeleteFileAsync(runnerFileName); } catch { }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(new RichDescription("Run PyUnit tests"));
        }

        protected override void LogProcessOutput(string text)
        {
            if (text.StartsWith("__BuildMasterPythonTestRunner__"))
            {
                this.Events.Add(JsonConvert.DeserializeObject<TestEvent>(text.Substring("__BuildMasterPythonTestRunner__".Length)));
                return;
            }

            base.LogProcessOutput(text);
        }

        protected override void LogProcessError(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                this.LogInformation(text);
            }
        }
    }

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

    public sealed class TestEvent
    {
        public EventType Type { get; set; }

        private static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).ToLocalTime();
        [JsonIgnore]
        public DateTimeOffset NowTime => TestEvent.UnixEpoch.AddSeconds(this.Now);

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

    public sealed class TestCaseID
    {
        [JsonIgnore]
        public string Group => this.ID.LastIndexOf('.') == -1 ? null : this.ID.Substring(0, this.ID.LastIndexOf('.'));
        [JsonIgnore]
        public string Name => this.ID.Substring(this.ID.LastIndexOf('.') + 1);

        public string ID { get; set; }
        public string Desc { get; set; }

        public override bool Equals(object obj) => obj != null && this.ID == (obj as TestCaseID)?.ID;
        public override int GetHashCode() => EqualityComparer<string>.Default.GetHashCode(ID);
        public static bool operator ==(TestCaseID case1, TestCaseID case2) => EqualityComparer<TestCaseID>.Default.Equals(case1, case2);
        public static bool operator !=(TestCaseID case1, TestCaseID case2) => !(case1 == case2);
    }
}
