using System.Configuration;
using System.Linq;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;

namespace uHateoas
{
    public class SiteStartup : IComponent
    {
        public ILogger Logger { get; set; }

        public void Initialize()
        {
            ContentService.Published += ContentServicePublishEvent;
            ContentService.Unpublished += ContentServicePublishEvent;
            ContentService.Deleted += ContentServiceOnDeleted;
        }

        public void Terminate()
        {
        }

        private void EmptyCache(string alias)
        {
            Logger.Info(GetType(), "Emptying uHateoas cache");
            if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.CacheDocTypes"))
            {
                Umbraco.Core.Composing.Current.AppCaches.RuntimeCache.ClearByKey(UExtensions.CachePrefix + alias + "-");
            }
            else
            {
                Umbraco.Core.Composing.Current.AppCaches.RuntimeCache.ClearByKey(UExtensions.CachePrefix);
            }
        }

        private void ContentServiceOnDeleted(IContentService sender, DeleteEventArgs<IContent> e)
        {
            foreach (var item in e.DeletedEntities)
            {
                EmptyCache(item.ContentType.Alias);
            }
        }

        private void ContentServicePublishEvent(IContentService sender, PublishEventArgs<IContent> args)
        {
            foreach (var item in args.PublishedEntities)
            {
                EmptyCache(item.ContentType.Alias);
            }
        }
    }
}
