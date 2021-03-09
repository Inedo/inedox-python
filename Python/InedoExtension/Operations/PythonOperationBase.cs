using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Web;

namespace Inedo.Extensions.Python.Operations
{
    [Tag("python")]
    [ScriptNamespace("Python")]
    public abstract class PythonOperationBase : ExecuteOperation
    {
        protected PythonOperationBase()
        {
        }

        [ScriptAlias("PythonExePath")]
        [DefaultValue("python")]
        [FieldEditMode(FieldEditMode.ServerFilePath)]
        public string PythonExePath { get; set; } = "python";

        [ScriptAlias("VirtualEnv")]
        [PlaceholderText("(use system scope)")]
        [FieldEditMode(FieldEditMode.ServerFilePath)]
        public string VirtualEnv { get; set; }

        protected Task WrapInVirtualEnv(IOperationExecutionContext context, RemoteProcessStartInfo startInfo) => WrapInVirtualEnv(this, context, startInfo, this.PythonExePath, this.VirtualEnv);
        internal static async Task WrapInVirtualEnv(ILogSink logger, IOperationExecutionContext context, RemoteProcessStartInfo startInfo, string pythonExePath, string virtualEnv)
        {
            if (string.IsNullOrEmpty(virtualEnv))
                return;

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
            if (!await fileOps.DirectoryExistsAsync(virtualEnv))
            {
                var procExec = await context.Agent.GetServiceAsync<IRemoteProcessExecuter>();
                logger.LogDebug($"Virtual environment in {virtualEnv} is not present. Attempting venv (Python 3.3+)...");
                var success = false;
                using (var process = procExec.CreateProcess(new RemoteProcessStartInfo
                {
                    FileName = pythonExePath,
                    WorkingDirectory = context.WorkingDirectory,
                    Arguments = "-m venv -- " + virtualEnv,
                }))
                {
                    process.OutputDataReceived += (s, e) => logger.LogDebug("(venv) " + e.Data);
                    process.ErrorDataReceived += (s, e) => logger.LogDebug("(venv) " + e.Data);
                    await process.WaitAsync(context.CancellationToken);
                    success = process.ExitCode == 0;
                }

                if (!success)
                {
                    logger.LogDebug("Attempting virtualenv (any Python version, but requires separate installation)...");
                    using var process = procExec.CreateProcess(
                        new RemoteProcessStartInfo
                        {
                            FileName = "virtualenv",
                            WorkingDirectory = context.WorkingDirectory,
                            Arguments = "-- " + virtualEnv,
                            EnvironmentVariables =
                            {
                                ["VIRTUALENV_PYTHON"] = pythonExePath
                            }
                        }
                    );

                    process.OutputDataReceived += (s, e) => logger.LogDebug("(virtualenv) " + e.Data);
                    process.ErrorDataReceived += (s, e) => logger.LogDebug("(virtualenv) " + e.Data);
                    await process.WaitAsync(context.CancellationToken);
                    success = process.ExitCode == 0;
                }

                if (!success)
                    throw new ExecutionFailureException("Could not create a virtual environment. See debug logs from this operation for more information.");
            }

            startInfo.FileName = (await fileOps.GetFileInfoAsync(fileOps.CombinePath(virtualEnv, "bin", "python"))).FullName;
        }
    }
}
