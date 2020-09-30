using System.IO;
using System.Web.Mvc;
using Umbraco.Core.Models.PublishedContent;

namespace uHateoas.Models
{
    public class FakeView : IView
    {
        public void Render(ViewContext viewContext, TextWriter writer)
        {
        }
    }
}