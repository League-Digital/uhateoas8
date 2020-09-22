using System.IO;
using System.Web.Mvc;

namespace uHateoas
{
    public partial class UHateoas
    {
        //public UHateoas(RenderModel model, bool simple = false)
        //{
        //    Initialise();
        //    foreach (var item in Process(model.Content, simple))
        //    {
        //        Add(item.Key, item.Value);
        //    }
        //}

        private class FakeView : IView
        {
            public void Render(ViewContext viewContext, TextWriter writer)
            {
            }
        }
    }
}