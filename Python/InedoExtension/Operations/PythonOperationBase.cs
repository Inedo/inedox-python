using System.ComponentModel;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.Python.Operations
{
    [Tag("python")]
    [ScriptNamespace("Python")]
    public abstract class PythonOperationBase : ExecuteOperation
    {
        protected PythonOperationBase()
        {
        }

        [Category("Advanced")]
        [ScriptAlias("PythonPath")]
        [ScriptAlias("PythonExePath", Obsolete = true)]
        [DefaultValue("$PythonPath")]
        [DisplayName("Python path")]
        [Description("Full path to python/python.exe on the target server.")]
        public string PythonExePath { get; set; } = "$PythonPath";

        [Category("Advanced")]
        [ScriptAlias("VirtualEnv")]
        [DefaultValue("$PythonVirtualEnv")]
        public string VirtualEnv { get; set; } = "$PythonVirtualEnv";

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

        protected async Task<string> GetPythonExePathAsync(IOperationExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(this.PythonExePath))
            {
                this.LogDebug("PythonPath is not defined; searching for python...");

                string foundPath = null;

                var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
                if (fileOps.DirectorySeparator == '/')
                {
                    if (await fileOps.FileExistsAsync("/bin/python3"))
                        foundPath = "/bin/python3";
                }
                else
                {
                    var rubbish = await context.Agent.GetServiceAsync<IRemoteProcessExecuter>();
                    var programFilesDir = await rubbish.GetEnvironmentVariableValueAsync("ProgramFiles");

                    var path = await getBestVersionAsync(programFilesDir!);
                    if (path == null)
                    {
                        var userPythonDir = fileOps.CombinePath(await rubbish.GetEnvironmentVariableValueAsync("LocalAppData"), "Programs", "Python");
                        if (await fileOps.DirectoryExistsAsync(userPythonDir))
                            path = await getBestVersionAsync(userPythonDir);
                    }

                    foundPath = path;

                    async Task<string> getBestVersionAsync(string searchPath)
                    {
                        var dirs = from d in await fileOps.GetFileSystemInfosAsync(searchPath, new MaskingContext(new[] { "Python3*" }, Enumerable.Empty<string>()))
                                   where d is SlimDirectoryInfo && d.Name.StartsWith("Python3")
                                   let ver = AH.ParseInt(d.Name.Substring("Python3".Length))
                                   where ver.HasValue
                                   orderby ver descending
                                   select d.FullName;

                        foreach (var dir in dirs)
                        {
                            var path = fileOps.CombinePath(dir, "python.exe");
                            if (await fileOps.FileExistsAsync(path))
                                return path;
                        }

                        return null;
                    }
                }

                if (foundPath == null)
                    throw new ExecutionFailureException("Could not find python interpreter and $PythonPath configuration variable is not set.");

                this.LogDebug("Using python at: " + foundPath);
                return foundPath;
            }
            else
            {
                return context.ResolvePath(this.PythonExePath);
            }
        }
    }
}
