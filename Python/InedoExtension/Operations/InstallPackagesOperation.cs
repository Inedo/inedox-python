using System.ComponentModel;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.PackageSources;
using Inedo.Web;

namespace Inedo.Extensions.Python.Operations
{
    [ScriptNamespace("Pip")]
    [ScriptAlias("Install-Packages")]
    [DisplayName("Install Pip Packages")]
    [Description("Installs packages using pip.")]
    public sealed class InstallPackagesOperation : PythonOperationBase
    {
        [ScriptAlias("InstallFromRequirements")]
        [DisplayName("Install from requirements.txt")]
        [Description("Install a list of requirements specified in the default requirements file (requirements.txt) by passing the \"-r requirements.txt\" argument to the commandline.")]
        [DefaultValue(true)]
        public bool InstallFromRequirements { get; set; }

        [ScriptAlias("Source")]
        [DisplayName("Package source")]
        [Description("If specified, this PyPI package index will be used to install packages from.")]
        [SuggestableValue(typeof(PyPiPackageSourceSuggestionProvider))]
        public string PackageSource { get; set; }

        [ScriptAlias("AdditionalArguments")]
        [DisplayName("Additional arguments")]
        public string AdditionalArguments { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            string indexUrl = null;

            if (!string.IsNullOrEmpty(this.PackageSource))
            {
                var sourceId = new PackageSourceId(this.PackageSource);
                if (sourceId.Format != PackageSourceIdFormat.Url)
                {
                    this.LogDebug($"Resolving package source \"{this.PackageSource}\"...");
                    var source = await AhPackages.GetPackageSourceAsync(sourceId, context, context.CancellationToken);
                    if (source == null)
                    {
                        this.LogError($"Package source \"{this.PackageSource}\" not found.");
                        return;
                    }

                    if (source is not IPyPiPackageSource pypi)
                    {
                        this.LogError($"Package source \"{this.PackageSource}\" is a {source.GetType().Name} source; it must be a PyPI source for use with this operation.");
                        return;
                    }

                    indexUrl = pypi.IndexUrl;
                }
                else
                {
                    indexUrl = sourceId.GetUrl();
                }
            }

            await PipFreezeAsync(context);
            await PipInstallAsync(context, indexUrl);
        }

        private async Task PipFreezeAsync(IOperationExecutionContext context)
        {
            var startInfo = new RemoteProcessStartInfo
            {
                FileName = this.PythonExePath,
                Arguments = "-m pip freeze --progress-bar=off --no-color",
                WorkingDirectory = context.WorkingDirectory,
                OutputFileName = "requirements.txt"
            };

            if (this.InstallFromRequirements)
                startInfo.Arguments += " -r requirements.txt";

            if (!string.IsNullOrWhiteSpace(this.AdditionalArguments))
                startInfo.Arguments += this.AdditionalArguments;

            await this.WrapInVirtualEnv(context, startInfo);
            await this.ExecuteCommandLineAsync(context, startInfo);
        }

        private async Task PipInstallAsync(IOperationExecutionContext context, string indexUrl)
        {
            var startInfo = new RemoteProcessStartInfo
            {
                FileName = this.PythonExePath,
                Arguments = "-m pip install --progress-bar=off --no-color",
                WorkingDirectory = context.WorkingDirectory
            };

            if (this.InstallFromRequirements)
                startInfo.Arguments += " -r requirements.txt";

            if (!string.IsNullOrWhiteSpace(indexUrl))
                startInfo.Arguments += $" -i \"{indexUrl}\"";

            if (!string.IsNullOrWhiteSpace(this.AdditionalArguments))
                startInfo.Arguments += this.AdditionalArguments;

            await this.WrapInVirtualEnv(context, startInfo);
            await this.ExecuteCommandLineAsync(context, startInfo);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(new RichDescription("Install Python Packages"));
        }
    }
}
