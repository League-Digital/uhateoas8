using System.Configuration;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;

namespace uHateoas
{
    public class ContentFinderByNiceUrlWithContentAccept : IContentLastChanceFinder
    {
        private readonly IDomainService _domainService;

        public IFileService FileService { get; set; }

        public ContentFinderByNiceUrlWithContentAccept(IDomainService domainService)
        {
            _domainService = domainService;
        }

        public bool TryFindContent(PublishedRequest contentRequest)
        {
            var url = contentRequest.Uri.ToString();
            var allDomains = _domainService.GetAll(true);
            var domain = allDomains?.Where(f => f.DomainName == contentRequest.Uri.Authority || f.DomainName == contentRequest.Uri.AbsoluteUri).FirstOrDefault();
            var siteId = domain != null ? domain.RootContentId : (allDomains.Any() ? allDomains.FirstOrDefault().RootContentId : null);
            var siteRoot = contentRequest.UmbracoContext.Content.GetById(false, siteId ?? -1);
            if (siteRoot == null) { siteRoot = contentRequest.UmbracoContext.Content.GetAtRoot().FirstOrDefault(); }
            if (siteRoot == null)
            {
                return false;
            }

            var route = contentRequest.Uri.GetAbsolutePathDecoded();
            var node = contentRequest.PublishedContent; //FindContent(contentRequest, route);

            var templateAlias = GetTemplateAliasByContentAccept();
            if (!string.Equals(templateAlias, "unknown", System.StringComparison.OrdinalIgnoreCase))
            {
                var template = Umbraco.Core.Composing.Current.Services.FileService.GetTemplate(templateAlias);

                if (template != null)
                {
                    //Logger.Debug($"Valid template: \"{templateAlias}\"");
                    if (node != null)
                        contentRequest.SetTemplate(template);
                }
                else
                {
                    //Logger.Warn($"Not a valid template: \"{templateAlias}\"");
                }
            }

            // false - returns original request 
            // true - returns 404 at the moment but should return ujson template 
            //var result = node != null;

            return false;
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
