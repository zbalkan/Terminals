using System;
using Terminals.Common.Connections;
using Terminals.Data;

namespace Terminals.Common.Converters
{
    /// <summary>
    /// Because general properties are no able to inject controls to simplify the UI.
    /// </summary>
    public class UrlConverter
    {
        public static Uri TryParseUrl(string url)
        {
            try
            {
                return new Uri(url);
            }
            catch (Exception ex)
            {
                Logging.Error("Web URL Parse Failed", ex);
                return null;
            }
        }

        public static Uri TryParseHttpsUrl(string url)
        {
            Uri result;
            return Uri.TryCreate(url, UriKind.Absolute, out result) &&
                   result.Scheme == Uri.UriSchemeHttps &&
                   !string.IsNullOrEmpty(result.Host) &&
                   string.IsNullOrEmpty(result.UserInfo) ? result : null;
        }

        public static void UpdateFavoriteUrl(IFavorite destination, string newAbsoluteUrl)
        {
            var webOptions = destination.ProtocolProperties as IRelativeUrlProvider;
            if (webOptions != null)
            {
                UpdateMyFavoriteUrl(destination, webOptions, newAbsoluteUrl);
            }
        }

        private static void UpdateMyFavoriteUrl(IFavorite destination, IRelativeUrlProvider relativeUrl, string newAbsoluteUrl)
        {
            bool httpsOnly = destination.Protocol == KnownConnectionConstants.HTTPS ||
                destination.Protocol == KnownConnectionConstants.CITRIX_WORKSPACE;
            Uri url = httpsOnly ? TryParseHttpsUrl(newAbsoluteUrl) : TryParseUrl(newAbsoluteUrl);
            if (url == null)
            {
                if (httpsOnly)
                    throw new ArgumentException("An absolute HTTPS URL is required.", "newAbsoluteUrl");
                return;
            }

            destination.ServerName = url.Host;
            destination.Port = url.Port; // would also manage default port for us
            string pathAndQuery = url.PathAndQuery.Equals(@"/") ? String.Empty : url.PathAndQuery;
            relativeUrl.RelativeUrl = pathAndQuery;
        }

        public static string ExtractAbsoluteUrl(IFavorite source)
        {
            try
            {
                return TryFormatAbsoluteUrl(source);
            }
            catch // UriBuilder fails on at least ICA Citrix as unknown service, use stupid URI formatting in this case
            {
                return String.Format(@"{0}://{1}:{2}/", source.Protocol, source.ServerName, source.Port);
            }
        }

        private static string TryFormatAbsoluteUrl(IFavorite source)
        {
            var webOptions = source.ProtocolProperties as IRelativeUrlProvider;
            string relativeUrl = String.Empty;
            if (webOptions != null)
                relativeUrl = webOptions.RelativeUrl;

            string protocol = source.Protocol == KnownConnectionConstants.CITRIX_WORKSPACE
                ? Uri.UriSchemeHttps : source.Protocol.ToLowerInvariant();
            var uriBuilder = new UriBuilder(protocol, source.ServerName, source.Port);
            // Citrix store URLs are HTTPS; avoid an extra slash between the host
            // and the stored path, while preserving case and query arguments.
            if (source.Protocol == KnownConnectionConstants.CITRIX_WORKSPACE)
                return uriBuilder.ToString().TrimEnd('/') +
                    (string.IsNullOrEmpty(relativeUrl) ? "/" :
                        (relativeUrl.StartsWith("/") ? relativeUrl : "/" + relativeUrl));
            // using relative url in uriBuilder constructor encodes ? character. So we have to make an workaround
            return uriBuilder + relativeUrl;
        }
    }
}
