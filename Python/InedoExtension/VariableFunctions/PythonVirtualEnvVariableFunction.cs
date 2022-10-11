using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.Python.VariableFunctions
{
    [Tag("python")]
    [ScriptAlias("PythonVirtualEnv")]
    [Description("Virtual environment to use for Python and Pip operations.")]
    [ExtensionConfigurationVariable(Required = false)]
    public sealed class PythonVirtualEnvVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context) => InedoLib.ApplicationName == "BuildMaster" ? "venv" : string.Empty;
    }
}
