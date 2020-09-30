using Umbraco.Core;
using Umbraco.Web.Routing;

namespace uHateoas
{
    public class ContentFinderResolver : IContentFinder
    {
        public bool TryFindContent(PublishedRequest request)
        {
            var path = request.Uri.GetAbsolutePathDecoded();
            if (!path.StartsWith("/ujson"))
                return false;

            var content = request.UmbracoContext.Content.GetById(request.Domain.ContentId);
            if (content == null) return false;

            request.PublishedContent = content;
            return true;
        }
    }
}
