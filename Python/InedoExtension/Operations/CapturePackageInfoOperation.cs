using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Newtonsoft.Json;

namespace Inedo.Extensions.Python.Operations
{
    [DisplayName("Capture Python Package Info")]
    [ScriptAlias("Capture-PackageInfo")]
    public sealed class CapturePackageInfoOperation : PythonOperationBase
    {
        [ScriptAlias("AdditionalArguments")]
        [DisplayName("Additional arguments")]
        public string AdditionalArguments { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var startInfo = new RemoteProcessStartInfo
            {
                FileName = this.PythonExePath,
                Arguments = "-m pip list --local --not-required --format=json",
                WorkingDirectory = context.WorkingDirectory
            };

            if (!string.IsNullOrWhiteSpace(this.AdditionalArguments))
                startInfo.Arguments += this.AdditionalArguments;

            await this.WrapInVirtualEnv(context, startInfo);

            var procExec = await context.Agent.GetServiceAsync<IRemoteProcessExecuter>();
            var output = new StringBuilder();
            using (var process = procExec.CreateProcess(startInfo))
            {
                process.OutputDataReceived += (s, e) => output.Append(e.Data);
                process.ErrorDataReceived += (s, e) => this.LogWarning(e.Data);
                await process.WaitAsync(context.CancellationToken);
                if (process.ExitCode != 0)
                {
                    this.LogError($"Process exited with code {process.ExitCode}");
                    return;
                }
            }

            var installedPackages = JsonConvert.DeserializeAnonymousType(output.ToString(), new[] { new { name = string.Empty, version = string.Empty } });
            this.LogInformation("Installed packages:");
            foreach (var package in installedPackages)
                this.LogInformation($"{package.name} v{package.version}");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(new RichDescription("Capture Python Package Info"));
        }
    }
}
