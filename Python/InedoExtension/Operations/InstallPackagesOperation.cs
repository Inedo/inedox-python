using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Python.Operations
{
    [DisplayName("Install Python Packages")]
    [ScriptAlias("Install-Packages")]
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
            var startInfo = new RemoteProcessStartInfo
            {
                FileName = this.PythonExePath,
                Arguments = "-m pip install --progress-bar=off --no-color",
                WorkingDirectory = context.WorkingDirectory
            };

            if (this.InstallFromRequirements)
            {
                startInfo.Arguments += " -r requirements.txt";
            }

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
