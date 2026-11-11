namespace Terminals.Configuration
{
    internal interface IPluginSettings
    {
        string[] DisabledPlugins { get; set; }
        string PreferredSshClient { get; set; }
        string PuttyExecutablePath { get; set; }
        string PageantExecutablePath { get; set; }
        string VncViewerExecutablePath { get; set; }
        string PreferredVncViewer { get; set; }
        string UltraVncExecutablePath { get; set; }
        string TigerVncExecutablePath { get; set; }
        string RealVncExecutablePath { get; set; }
        string CitrixSelfServiceExecutablePath { get; set; }
        string CitrixStorebrowseExecutablePath { get; set; }
    }
}