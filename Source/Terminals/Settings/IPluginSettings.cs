namespace Terminals.Configuration
{
    internal interface IPluginSettings
    {
        string[] DisabledPlugins { get; set; }
        string PuttyExecutablePath { get; set; }
        string PageantExecutablePath { get; set; }
    }
}