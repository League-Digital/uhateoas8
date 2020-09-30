using System.Configuration;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Web.Composing;
using Umbraco.Web.Routing;

namespace uHateoas
{
    public class ContentFinderByNiceUrlWithContentAccept : IContentLastChanceFinder
    {
        public bool TryFindContent(PublishedRequest contentRequest)
        {
            var node = Current.UmbracoContext.Content.GetByRoute(contentRequest.Uri.GetAbsolutePathDecoded());
            var templateAlias = GetTemplateAliasByContentAccept();
            if (!string.Equals(templateAlias, "unknown", System.StringComparison.OrdinalIgnoreCase))
            {
                var template = Umbraco.Core.Composing.Current.Services.FileService.GetTemplate(templateAlias);
                if (template != null && node != null)
                {
                    contentRequest.SetTemplate(template);
                }
            }
            return node == null;
        }

        private static string GetTemplateAliasByContentAccept()
        {
            var contentType = HttpContext.Current.Request.ContentType;
            string template = null;
            if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.{contentType}"))
                template = ConfigurationManager.AppSettings[$"{UExtensions.AppSettingsPrefix}.Templates.{contentType}"];

            return template ?? "unknown";
        }
    }
}
