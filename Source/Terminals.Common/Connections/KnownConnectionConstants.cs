namespace Terminals.Common.Connections
{
    public class KnownConnectionConstants
    {
        public const int RDPPort = 3389;

        // Retained for legacy favorites; no HTTP connection target is registered.
        public const int HTTPPort = 80;

        public const string HTTP = "HTTP";

        // SQL insert/update procedures accept only nvarchar(5), even when
        // the table itself allows longer values.
        public const string CITRIX_WORKSPACE = "CTXWS";
        public const string LEGACY_ICA_SQL = "ICA C";

        public const string HTTPS = "HTTPS";

        public const string RAS = "RAS";

        public const string RDP = "RDP";
    }
}