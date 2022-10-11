using System.ComponentModel;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

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

        [ScriptAlias("AdditionalArguments")]
        [DisplayName("Additional arguments")]
        public string AdditionalArguments { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            await PipFreezeAsync(context);
            await PipInstallAsync(context);
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

        private async Task PipInstallAsync(IOperationExecutionContext context)
        {
            var startInfo = new RemoteProcessStartInfo
            {
                FileName = this.PythonExePath,
                Arguments = "-m pip install --progress-bar=off --no-color",
                WorkingDirectory = context.WorkingDirectory
            };

            if (this.InstallFromRequirements)
                startInfo.Arguments += " -r requirements.txt";

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
