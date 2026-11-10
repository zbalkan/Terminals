using System;
using Terminals.Common.Converters;

namespace Terminals.Data
{
    [Serializable]
    public sealed class CitrixWorkspaceOptions : ProtocolOptions, IRelativeUrlProvider
    {
        // Cloud: SelfService.exe; StoreFront: storebrowse.exe.
        public string LaunchMode { get; set; } = "Cloud";
        public string ResourceName { get; set; } = string.Empty;
        public string RelativeUrl { get; set; } = string.Empty;

        public override ProtocolOptions Copy()
        {
            return new CitrixWorkspaceOptions
            {
                LaunchMode = LaunchMode,
                ResourceName = ResourceName,
                RelativeUrl = RelativeUrl
            };
        }
    }
}
