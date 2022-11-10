using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

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
        private readonly JsonSerializerOptions jsonSerializerOptions = new() { Converters = { new JsonStringEnumConverter() } };

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
                        case EventType.Success or EventType.ExpectedFailure:
                            return UnitTestStatus.Passed;
                        case EventType.Error or EventType.Failure or EventType.UnexpectedSuccess:
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

                return "Test: " + test.Key.ID + ConcatNE("\n", test.Key.Desc) +
                    "\n\nResult: " + AH.CoalesceString(result, "Unknown") + ConcatNE(" (", skipReason, ")") +
                    ConcatNE("\n\nOutput:\n", stdout) + ConcatNE("\n\nError:\n", stderr) +
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

            static string ConcatNE(string a, string b, string c = "")
            {
                if (!string.IsNullOrEmpty(b))
                    return a + b + c;
                else
                    return string.Empty;
            }
        }

        private async Task RunTestsAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();

            var scriptsDir = fileOps.CombinePath(await fileOps.GetBaseWorkingDirectoryAsync(), "scripts");
            await fileOps.CreateDirectoryAsync(scriptsDir);
            var runnerFileName = fileOps.CombinePath(scriptsDir, $"BuildMasterTestRunner_{Guid.NewGuid():N}.py");
            using (var fileStream = await fileOps.OpenFileAsync(runnerFileName, FileMode.Create, FileAccess.Write))
            using (var stream = typeof(PyUnitOperation).Assembly.GetManifestResourceStream("Inedo.Extensions.Python.BuildMasterTestRunner.py")!)
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
                this.Events.Add(JsonSerializer.Deserialize<TestEvent>(text, this.jsonSerializerOptions));
                return;
            }

            base.LogProcessOutput(text);
        }

        protected override void LogProcessError(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                this.LogInformation(text);
        }
    }
}
