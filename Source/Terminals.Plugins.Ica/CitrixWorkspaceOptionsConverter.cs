using Terminals.Common.Connections;
using Terminals.Data;

namespace Terminals.Plugins.Ica
{
    internal sealed class CitrixWorkspaceOptionsConverter : OptionsConverterTemplate<CitrixWorkspaceOptions>, IOptionsConverter
    {
        protected override void FromConfigFavorite(FavoriteConfigurationElement source, CitrixWorkspaceOptions options)
        {
            options.LaunchMode = source.CitrixWorkspaceMode;
            options.ResourceName = source.CitrixWorkspaceResource;
            options.RelativeUrl = source.CitrixWorkspaceRelativeUrl;
        }

        protected override void ToConfigFavorite(FavoriteConfigurationElement destination, CitrixWorkspaceOptions options)
        {
            destination.CitrixWorkspaceMode = options.LaunchMode;
            destination.CitrixWorkspaceResource = options.ResourceName;
            destination.CitrixWorkspaceRelativeUrl = options.RelativeUrl;
        }
    }
}
