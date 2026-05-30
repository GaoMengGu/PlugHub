using Autodesk.Revit.UI;
using System;
using System.Reflection;

namespace PlugHub.Revit2020
{
    internal interface ICommandAssemblyLoader
    {
        IExternalCommand Create(string assemblyPath, string commandTypeName);
    }

    internal sealed class Net48DirectCommandAssemblyLoader : ICommandAssemblyLoader
    {
        public IExternalCommand Create(string assemblyPath, string commandTypeName)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath)) throw new ArgumentException("Command assembly path is required.", nameof(assemblyPath));
            if (string.IsNullOrWhiteSpace(commandTypeName)) throw new ArgumentException("Command type is required.", nameof(commandTypeName));

            var commandType = Assembly.LoadFrom(assemblyPath).GetType(commandTypeName, throwOnError: false);
            if (commandType == null || !typeof(IExternalCommand).IsAssignableFrom(commandType))
            {
                throw new InvalidOperationException("Command type was not found or does not implement IExternalCommand: " + commandTypeName);
            }

            return (IExternalCommand)Activator.CreateInstance(commandType)!;
        }
    }
}
