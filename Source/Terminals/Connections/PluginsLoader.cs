using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terminals.Configuration;
using Terminals.Common.Configuration;

namespace Terminals.Connections
{
    internal class PluginsLoader : IPluginsLoader
    {
        private readonly string[] enabledPlugins;

        public PluginsLoader(IPluginSettings settings)
        {
            this.enabledPlugins = settings.DisabledPlugins;
            ExternalExecutablePaths.Putty = settings.PuttyExecutablePath;
            ExternalExecutablePaths.Pageant = settings.PageantExecutablePath;
            SshClientPreference.Selected = SshClientPreference.Parse(settings.PreferredSshClient);
        }

        public IEnumerable<IConnectionPlugin> Load()
        {
            // The PuTTY assembly also supplies SSH. If disabled, retain only
            // SSH when Windows OpenSSH is selected; Telnet remains disabled.
            var availablePlugins = FindPluginAssemblies().SelectMany(path =>
            {
                if (!enabledPlugins.Contains(path))
                    return LoadAssemblyPlugins(path);
                return SshClientPreference.Selected == SshClientKind.WindowsOpenSSH &&
                       Path.GetFileName(path).Equals("Terminals.Plugins.Putty.dll", StringComparison.OrdinalIgnoreCase)
                    ? LoadAssemblyPlugins(path).Where(plugin => plugin.PortName == "SSH")
                    : Enumerable.Empty<IConnectionPlugin>();
            }).ToList();

            if (!availablePlugins.Any())
                throw new ApplicationException("No available protocol plugin was loaded.");

            return availablePlugins;
        }

        private static IEnumerable<IConnectionPlugin> LoadAssemblyPlugins(string pluginFile)
        {
            try
            {
                Assembly pluginAssembly = Assembly.LoadFrom(pluginFile);
                var types = FindAllPluginTypes(pluginAssembly);
                return types.Select(Activator.CreateInstance).OfType<IConnectionPlugin>();
            }
            catch (Exception exception)
            {
                string message = string.Format("Unable to load plugins from '{0}'.", pluginFile);
                Logging.Info(message, exception);
                return new IConnectionPlugin[0];
            }
        }

        public IEnumerable<PluginDefinition> FindAvailablePlugins()
        {
            IEnumerable<string> allPluginAssemblies = FindPluginAssemblies();
            return allPluginAssemblies.Select(LoadPlugDefinition)
                .ToList();
        }

        private static PluginDefinition LoadPlugDefinition(string pluginFile)
        {
            string description = GetAssemblyDescription(pluginFile);
            return new PluginDefinition(pluginFile, description);
        }

        private static string GetAssemblyDescription(string pluginFile)
        {
            Assembly assembly = Assembly.ReflectionOnlyLoadFrom(pluginFile);
            CustomAttributeData customData = assembly.GetCustomAttributesData()
                .FirstOrDefault(IsAssemblyDecriptionAttribute);

            if (customData == null)
                return string.Empty;

            return customData.ConstructorArguments[0].Value.ToString();
        }

        private static bool IsAssemblyDecriptionAttribute(CustomAttributeData attribute)
        {
            return attribute.Constructor.DeclaringType == typeof(AssemblyDescriptionAttribute) &&
                   attribute.ConstructorArguments.Count == 1;
        }

        private static IEnumerable<string> FindPluginAssemblies()
        {
            string[] pluginDirectories = FindPluginDirectories();
            return FindAllPluginAssemblies(pluginDirectories);
        }

        private static IEnumerable<Type> FindAllPluginTypes(Assembly pluginAssembly)
        {
            return pluginAssembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && t.GetInterface(typeof(IConnectionPlugin).FullName) != null);
        }

        private static string[] FindPluginDirectories()
        {
            try
            {
                string pluginsDirectory = FindBasePluginDirectory();
                return Directory.GetDirectories(pluginsDirectory);
            }
            catch (Exception exception)
            {
                Logging.Info("Unable to open plugins directory.", exception);
                return new string[0];
            }
        }

        internal static string FindBasePluginDirectory()
        {
            string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string pluginsDirectory = Path.Combine(applicationDirectory, "Plugins");
            return pluginsDirectory;
        }

        private static IEnumerable<string> FindAllPluginAssemblies(string[] pluginDirectories)
        {
            return pluginDirectories.SelectMany(FindPluginAssemblies)
                // An old installed plugin must not reactivate retired Microsoft Virtual Server support.
                .Where(path => !Path.GetFileName(path).Equals("Terminals.Plugins.Vmrc.dll",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static string[] FindPluginAssemblies(string pluginDirectory)
        {
            try
            {
                return Directory.GetFiles(pluginDirectory, "Terminals.Plugins.*.dll");
            }
            catch (Exception exception)
            {
                string message = string.Format("Unable to load plugins list from '{0}'.", pluginDirectory);
                Logging.Info(message, exception);
                return new string[0];
            }
        }
    }
}