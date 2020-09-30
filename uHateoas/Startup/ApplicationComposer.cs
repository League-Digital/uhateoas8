using System;
using System.Configuration;
using System.Linq;
using System.Web.Configuration;
using Umbraco.Core.Composing;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace uHateoas
{
    public class ApplicationComposer : ComponentComposer<ApplicationComponent>, IUserComposer
    {
        public override void Compose(Composition composition)
        {
            base.Compose(composition);

            if (CheckAppSettings())
            {
                var hypermediaTemplates = ConfigurationManager.AppSettings[$"{UExtensions.AppSettingsPrefix}.Templates.enabled"] == "true";
                if (hypermediaTemplates)
                {
                    composition.ContentFinders().InsertBefore<ContentFinderByUrl, ContentFinderByUrlAndTemplate>();
                    composition.ContentFinders().Remove<ContentFinderByUrl>();
                    composition.ContentFinders().Append<ContentFinderByNiceUrlWithContentAccept>();
                    composition.ContentFinders().Append<ContentFinderByUrl>();
                }
            }
        }

        private static bool CheckAppSettings()
        {
            try
            {
                if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.enabled"))
                    return true;

                var changes = false;
                var webConfigApp = WebConfigurationManager.OpenWebConfiguration("~");

                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.enabled"))
                {
                    webConfigApp.AppSettings.Settings.Add($"{UExtensions.AppSettingsPrefix}.Templates.enabled", "true");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.text/umbraco+json"))
                {
                    webConfigApp.AppSettings.Settings.Add($"{UExtensions.AppSettingsPrefix}.Templates.text/umbraco+json", "uhateoas");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.text/json"))
                {
                    webConfigApp.AppSettings.Settings.Add($"{UExtensions.AppSettingsPrefix}.Templates.text/json", "ujson");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.text/xml"))
                {
                    webConfigApp.AppSettings.Settings.Add($"{UExtensions.AppSettingsPrefix}.Templates.text/xml", "uxml");
                    changes = true;
                }

                if (changes)
                    webConfigApp.Save();
            }
            catch (Exception)
            {
                //Logger.Error(MethodBase.GetCurrentMethod().DeclaringType, $"UHateoas CheckAppSettings Error: \"{ex.Message}\"", ex);
                return false;
            }
            return true;
        }
    }
}
