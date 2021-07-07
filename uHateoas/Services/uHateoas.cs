using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using uHateoas.Models;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Composing;
using Umbraco.Web.Security;

namespace uHateoas.Services
{

    [Serializable]
    public partial class UHateoas : Dictionary<string, object>
    {
        private HttpContext Context { get; set; }
        private IContentTypeService ContentTypeService { get; set; }
        private IContentService ContentService { get; set; }
        private IDataTypeService DataTypeService { get; set; }
        private UmbracoHelper UmbracoHelper { get; set; }
        private IUser CurrentUser { get; set; }
        private List<object> Entities { get; set; }
        private List<object> Actions { get; set; }
        private List<IContentType> AllowedContentTypes { get; set; }
        private IPublishedContent MainModel { get; set; }
        private int CurrentPageId { get; set; }
        private bool CanCreate { get; set; }
        private bool CanUpdate { get; set; }
        private bool CanDelete { get; set; }
        private bool SimpleJson { get; set; }
        private int CacheHours { get; set; }
        private int CacheMinutes { get; set; }
        private int CacheSeconds { get; set; }
        private bool IsDebug { get; set; }
        private int QueryMaxItems { get; set; }
        private string RequestAction { get; set; }
        private string RequestDocType { get; set; }
        private string RequestCurrentModel { get; set; }
        private string RequestEncodeHtml { get; set; }
        private string RequestResolveContent { get; set; }
        private string[] RequestResolveMedia { get; set; }
        private string RequestResolveToIds { get; set; }
        private string RequestAncestor { get; set; }
        private string RequestDescendants { get; set; }
        private string RequestChildren { get; set; }
        private string RequestSelect { get; set; }
        private string RequestWhere { get; set; }
        private string RequestHtml { get; set; }
        private string RequestSkip { get; set; }
        private string RequestTake { get; set; }
        private string RequestNoCache { get; set; }
        private string RequestOrderBy { get; set; }
        private string RequestOrderByDesc { get; set; }
        private string PublishedModelsNamespace { get; set; }
        //public ILogger Logger { get; set; }

        public UHateoas()
        {
            Initialise();
        }

        protected UHateoas(SerializationInfo info, StreamingContext context)
        {
            Initialise();
        }

        public UHateoas(IPublishedContent currentPage, bool simple = false)
        {
            Initialise();
            foreach (var item in Process(currentPage, simple))
            {
                Add(item.Key, item.Value);
            }
        }

        private void Initialise()
        {
            Context = HttpContext.Current;
            UmbracoHelper = Umbraco.Web.Composing.Current.UmbracoHelper;
            //Logger = Current.Logger;

            var template = string.Empty;
            var contentType = Context.Request.ContentType;
            var outputType = Context.Request.ContentType;

            if (String.IsNullOrEmpty(contentType))
            {
                var urlPath = Context.Request.Path;
                if (urlPath.IndexOf("ujson", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    contentType = "application/json";
                    outputType = "application/json";
                }
                else
                if (urlPath.IndexOf("uhateoas", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    contentType = "text/umbraco+json";
                    outputType = "application/json";
                }
                else if (urlPath.IndexOf("uxml", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    contentType = "text/xml";
                    outputType = "text/xml";
                }
            }

            PublishedModelsNamespace =
                ConfigurationManager.AppSettings.AllKeys.Contains(
                    $"{UExtensions.AppSettingsPrefix}.PublishedModelsNamespace")
                    ? ConfigurationManager.AppSettings[$"{UExtensions.AppSettingsPrefix}.PublishedModelsNamespace"]
                    : "PublishedModels";

            IsDebug = ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Debug") &&
                 ConfigurationManager.AppSettings[$"{UExtensions.AppSettingsPrefix}.Debug"] == "1";

            if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.{contentType}"))
                template = ConfigurationManager.AppSettings[$"{UExtensions.AppSettingsPrefix}.Templates.{contentType}"];

            Context.Response.ContentType = string.IsNullOrEmpty(template) ? outputType : "application/json";

            CacheHours = 24 * 7;
            CacheMinutes = 0;
            CacheSeconds = 0;

            if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Cache"))
            {
                var cache = $"{UExtensions.AppSettingsPrefix}.Cache";
                var cacheData = cache.Split(':');

                if (cacheData.Length == 3)
                {
                    CacheHours = System.Int32.TryParse(cacheData[0], out int cacheHours) ? cacheHours : 24 * 7;
                    CacheMinutes = System.Int32.TryParse(cacheData[1], out int cacheMinutes) ? cacheMinutes : 0;
                    CacheSeconds = System.Int32.TryParse(cacheData[2], out int cacheSeconds) ? cacheSeconds : 0;
                }
                if (IsDebug)
                {
                    //Logger.Info(GetType(), "uHateoas: Caching is set up  (duration is currently " + CacheHours + ":" + CacheMinutes + ":" + CacheSeconds + ")");
                }
            }
            else
            {
                CacheMinutes = 10;
            }

            CanCreate = false;
            CanUpdate = false;
            CanDelete = false;

            RequestAction = Context.Request["action"] ?? "";
            RequestDocType = Context.Request["doctype"] ?? "";
            RequestCurrentModel = Context.Request["currentmodel"] ?? "";
            RequestEncodeHtml = Context.Request["encodeHTML"] ?? "";
            RequestResolveContent = Context.Request["resolveContent"] ?? "";
            RequestResolveMedia = (Context.Request["resolveMedia"] ?? "").ToLower().Split(',');
            RequestResolveToIds = Context.Request["resolveToIds"] ?? "";
            RequestAncestor = Context.Request["ancestor"] ?? "";
            RequestDescendants = Context.Request["descendants"] ?? "";
            RequestChildren = Context.Request["children"] ?? "";
            RequestSelect = Context.Request["select"] ?? "";
            RequestWhere = Context.Request["where"] ?? "";
            RequestHtml = Context.Request["html"] ?? "";
            RequestSkip = Context.Request["skip"] ?? "";

            QueryMaxItems = 1000;
            RequestTake = Context.Request["take"] ?? "";
            Int32.TryParse(RequestTake, out _);
            if (string.IsNullOrEmpty(RequestTake))
            {
                RequestTake = QueryMaxItems.ToString();
                _ = QueryMaxItems;
            }

            RequestNoCache = Context.Request["nocache"] ?? "";
            RequestOrderBy = Context.Request["orderby"] ?? "";
            RequestOrderByDesc = Context.Request["orderbydesc"] ?? "";

            if (string.IsNullOrEmpty(RequestNoCache))
            {
                Context.Response.Cache.SetCacheability(HttpCacheability.Public);
                Context.Response.Cache.SetMaxAge(new TimeSpan(CacheHours, CacheMinutes, CacheSeconds));
            }
        }

        public HtmlString Process(string xmlRoot)
        {
            return new HtmlString(JsonConvert.DeserializeXmlNode(JsonConvert.SerializeObject(this, Formatting.Indented,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                }), xmlRoot).OuterXml);
        }

        public HtmlString Process()
        {
            try
            {
                var str = JsonConvert.SerializeObject(this, Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    });
                return new HtmlString(str);
            }
            catch (Exception ex)
            {
                return new HtmlString(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        public Dictionary<string, object> Process(IPublishedContent model, bool simple = false)
        {
            Dictionary<string, object> Data;
            MainModel = model;
            CurrentPageId = model.Id;
            SimpleJson = simple;

            var isLoggedIn = new HttpContextWrapper(HttpContext.Current).GetOwinContext().GetUmbracoAuthTicket();
            try
            {
                if (isLoggedIn != null && isLoggedIn.Properties.ExpiresUtc.GetValueOrDefault() > DateTime.Now)
                {
                    string userName = isLoggedIn.Identity.Name;
                    if (userName != null)
                    {
                        var services = Umbraco.Core.Composing.Current.Services;
                        CurrentUser = services.UserService.GetByUsername(userName);
                        if (CurrentUser != null && CurrentUser.Groups.Any(x => x.Alias == "admin"))
                        {
                            int[] aliases = { model.ContentType.Id };
                            var currentContentType = services.ContentTypeService.GetAll(aliases).FirstOrDefault();
                            if (currentContentType != null)
                            {
                                CanUpdate = true;
                                CanDelete = true;
                                if (currentContentType.AllowedContentTypes.Any())
                                    CanCreate = true;
                            }
                        }
                    }
                }

                if (string.Equals(Context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    Data = new Dictionary<string, object>();
                }
                else if (string.Equals(Context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(RequestAction) && !string.IsNullOrEmpty(RequestDocType))
                    {
                        Data = BuildForm(model, RequestDocType);
                    }
                    else if (UExtensions.SkipDomainCheck() || !string.IsNullOrEmpty(RequestNoCache))
                    {
                        if (IsDebug)
                        {
                            //Logger.Info(GetType(), "uHateoas: Skipping caching - SkipDomainCheck: " + UExtensions.SkipDomainCheck() + " RequestNoCache: " + RequestNoCache);
                        }
                        Data = ProcessRequest(model);
                    }
                    else
                    {
                        var cacheName = UExtensions.CachePrefix + model.ContentType.Alias + "-" +
                                        UExtensions.GetHashString(Context.Request.Url.PathAndQuery.ToLower());

                        if (IsDebug)
                        {
                            //Logger.Info(GetType(), "uHateoas: Looking for " + cacheName + " in cache (duration is currently " + CacheHours + ":" + CacheMinutes + ")");
                        }

                        var cache = Umbraco.Core.Composing.Current.AppCaches.RuntimeCache;
                        var timespan = new TimeSpan(CacheHours, CacheMinutes, CacheSeconds);
                        Data = cache.GetCacheItem<Dictionary<string, object>>(cacheName, () => ProcessRequest(model), timespan);
                    }
                }
                else
                {
                    Data = ProcessForm(model);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Process Error " + ex.Message);
            }
            return Data;
        }

        private Dictionary<string, object> ProcessRequest(IPublishedContent model)
        {
            if (IsDebug)
            {
                //Logger.Info(GetType(), "uHateoas: Item was not found in cache " + model.Id + " - " + model.Name);
            }

            Entities = new List<object>();
            Actions = new List<object>();
            try
            {
                if (model != null)
                {
                    bool EncodeHtml = false;
                    if (!string.IsNullOrEmpty(RequestCurrentModel))
                    {
                        model = UmbracoHelper.Content(RequestCurrentModel);
                    }
                    if (!string.IsNullOrEmpty(RequestEncodeHtml))
                    {
                        EncodeHtml = RequestEncodeHtml.AsBoolean();
                    }
                    if (!string.IsNullOrEmpty(RequestAncestor))
                    {
                        var ancestor = model.AncestorOrSelf(RequestAncestor);
                        return Simplify(ancestor);
                    }

                    Entities.AddRange(GetDescendantEntities(model));
                    Entities.AddRange(GetChildrenEntities(model));
                    if (!SimpleJson && (CanCreate || CanUpdate || CanDelete))
                        Actions.AddRange(GetChildrenActions(model));

                    return Simplify(model, true, Entities, Actions);
                }
                return new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                throw new Exception("ProcessRequest Error " + ex.Message);
            }
        }

        private Dictionary<string, object> Simplify(IPublishedContent node, bool showClass = true)
        {
            return Simplify(node, false, new List<object>(), new List<object>(), showClass);
        }

        private Dictionary<string, object> Simplify(IPublishedContent node, bool isRoot, List<object> entities, List<object> actions, bool showClass = true)
        {
            try
            {
                if (!HasAccess(node))
                    throw new Exception("Access Denied");

                Dictionary<string, object> returnProperties = new Dictionary<string, object>();
                SortedDictionary<string, object> properties = new SortedDictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);
                PropertyInfo[] props = typeof(IPublishedContent).GetProperties();
                List<object> links = new List<object>();

                foreach (PropertyInfo pi in props.OrderBy(p => p.Name))
                {
                    switch (pi.Name)
                    {
                        case "ContentSet":
                        case "ContentType":
                        case "PropertiesAsList":
                        case "ChildrenAsList":
                        case "ChildrenForAllCultures":
                        case "TemplateId":
                        case "Properties":
                        case "Item":
                        case "Version":
                        case "Cultures":
                            break;
                        case "Parent":
                            if (node != null && node.Parent != null)
                                if (HasAccess(node.Parent))
                                {
                                    links.Add(new
                                    {
                                        rel = new[] {
                                        "_Parent", node.Parent.ContentType.Alias
                                    },
                                        title = node.Parent.Name,
                                        href = GetHateoasHref(node.Parent, null)
                                    });
                                }
                            break;
                        case "Url":
                            if (node != null)
                            {
                                links.Add(new
                                {
                                    rel = new[] { "_Self", node.ContentType.Alias },
                                    title = node.Name,
                                    href = GetHateoasHref(node, null)
                                });
                                properties.Add(pi.Name, node.Url);
                            }
                            break;
                        case "Children":
                        case "GetChildrenAsList":
                            if (node != null)
                            {
                                foreach (var child in node.Children.ToList())
                                {
                                    if (HasAccess(child))
                                    {
                                        links.Add(new
                                        {
                                            rel = new[] { "_Child", child.ContentType.Alias },
                                            title = child.Name,
                                            href = GetHateoasHref(child, null)
                                        });
                                    }
                                }
                            }
                            break;
                        case "DocumentTypeAlias":
                        case "NodeTypeAlias":
                            if (node != null)
                            {
                                var classes = new SortedSet<string>
                                {
                                    node.ContentType.Alias
                                };
                                if (!string.IsNullOrEmpty(RequestDescendants) && isRoot)
                                {
                                    classes.Add("Descendants");
                                }
                                if (!string.IsNullOrEmpty(RequestChildren) && isRoot)
                                {
                                    classes.Add("Children");
                                }
                                if (showClass)
                                {
                                    if (SimpleJson)
                                        returnProperties.Add("class", string.Join(",", classes.ToArray()));
                                    else
                                        returnProperties.Add("class", classes.ToArray());
                                    returnProperties.Add("title", node.Name);
                                }
                                var prop = SimplyfyProperty(pi, node);
                                properties.Add(prop.Key, prop.Value);
                            }
                            break;
                        default:
                            if (node != null)
                            {
                                var prop1 = SimplyfyProperty(pi, node);
                                properties.Add(prop1.Key, prop1.Value);
                            }
                            break;
                    }
                }

                var useAllProperties = false;
                var propertyNames = new List<string>();
                if (!string.IsNullOrEmpty(RequestSelect))
                {
                    propertyNames = RequestSelect.ToLower().Split(',').ToList();
                }

                else
                    useAllProperties = true;

                if (node != null)
                {
                    foreach (IPublishedProperty pal in node.Properties)
                    {
                        if (pal != null)
                        {
                            if (useAllProperties || propertyNames.Contains(pal.Alias.ToLower()))
                            {
                                var prop = SimplifyProperty(pal, node);
                                properties.Add(prop.Key, prop.Value);
                            }
                        }
                    } 
                }

                if (propertyNames.Any())
                {
                    var properties1 = properties;
                    var selectedProperties = new SortedDictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

                    foreach (var a in properties.Where(p => propertyNames.Contains(p.Key.ToLower())))
                    {
                        selectedProperties.Add(a.Key, properties1[a.Key]);
                    }

                    properties = selectedProperties;
                }

                ResolveContent(properties);

                returnProperties.Add("properties", properties);

                if (entities.Any())
                {
                    returnProperties.Add("entities", entities);
                }

                if (!SimpleJson)
                {
                    if (actions.Any())
                    {
                        returnProperties.Add("actions", actions);
                    }

                    if (links.Any())
                    {
                        returnProperties.Add("links", links);
                    }
                }
                return returnProperties;
            }
            catch (Exception ex)
            {
                throw new Exception("Simplify Error " + ex.Message);
            }
        }

        private Dictionary<string, object> BuildForm(IPublishedContent model, string docTypeAlias)
        {
            Actions = new List<object>();
            try
            {
                if (CanCreate || CanUpdate || CanDelete)
                    Actions.AddRange(GetChildrenActions(model));
                else
                    return ProcessRequest(model);
                Dictionary<string, object> simpleNode = GenerateForm(model, docTypeAlias);
                if (Actions.Any())
                {
                    simpleNode.Add("actions", Actions);
                }
                return simpleNode;
            }
            catch (Exception ex)
            {
                throw new Exception("BuildForm Error " + ex.Message);
            }
        }

        private Dictionary<string, object> ProcessForm(IPublishedContent model)
        {
            Dictionary<string, object> node;
            try
            {
                string doctype = Context.Request.QueryString["doctype"] ?? "";
                string action = Context.Request.HttpMethod.ToUpper();
                bool delete = Context.Request.QueryString["delete"] != null && string.Equals(Context.Request.QueryString["delete"], "true", StringComparison.OrdinalIgnoreCase);
                bool publish = Context.Request.QueryString["publish"] != null && string.Equals(Context.Request.QueryString["publish"], "true", StringComparison.OrdinalIgnoreCase);
                ContentService = Umbraco.Core.Composing.Current.Services.ContentService;

                switch (action)
                {
                    case "POST":
                        if (!CanCreate)
                            throw new Exception(action + " Access Denied");
                        if (Context.Request.QueryString["doctype"] == null)
                            throw new Exception("No doctype supplied\r\n" + Context.Request.GetDetails());
                        node = ProcessRequest(CreateNode(model, doctype, publish));
                        break;

                    case "PUT":
                        if (!CanUpdate)
                            throw new Exception(action + " Access Denied");
                        node = ProcessRequest(UpdateNode(model, publish));
                        break;

                    case "PATCH":
                        if (!CanUpdate)
                            throw new Exception(action + " Access Denied");
                        node = ProcessRequest(UpdateNode(model, publish));
                        break;

                    case "DELETE":
                        if (!CanDelete)
                            throw new Exception(action + " Access Denied");
                        node = ProcessRequest(RemoveNode(model, delete));
                        break;

                    case "OPTIONS":
                        node = null;
                        break;

                    default:
                        throw new Exception(action + " is an invalid action");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Process Form Error " + ex.Message);
            }
            return node;
        }

        private IPublishedContent RemoveNode(IPublishedContent model, bool delete)
        {
            IPublishedContent node;
            try
            {
                IContent deleteNode = ContentService.GetById(model.Id);
                if (deleteNode == null)
                    throw new Exception("Node is null");
                if (delete)
                    ContentService.Delete(deleteNode, CurrentUser.Id);
                else
                    ContentService.Unpublish(deleteNode, "*", CurrentUser.Id);

                node = UmbracoHelper.Content(deleteNode.ParentId);
            }
            catch (Exception ex)
            {
                throw new Exception("RemoveNode Error " + ex.Message);
            }
            return node;
        }

        private IPublishedContent UpdateNode(IPublishedContent model, bool publish)
        {
            IPublishedContent node;
            try
            {
                IContent updateNode = ContentService.GetById(model.Id);
                if (updateNode == null)
                    throw new Exception("Node is null");

                string json = GetPostedJson();
                Dictionary<string, object> form = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (form.ContainsKey("Name"))
                    updateNode.Name = form["Name"].ToString();

                if (form.ContainsKey("ExpireDate"))
                    updateNode.ContentSchedule.Add(new ContentSchedule("*", (DateTime)form["ExpireDate"], ContentScheduleAction.Expire));

                if (form.ContainsKey("ReleaseDate"))
                    updateNode.ContentSchedule.Add(new ContentSchedule("*", (DateTime)form["ReleaseDate"], ContentScheduleAction.Release));

                foreach (Property prop in updateNode.Properties)
                {
                    try
                    {
                        KeyValuePair<string, object> kvp = GetValuePair(prop.Alias, form, updateNode);
                        if (kvp.Value != null)
                            updateNode.SetValue(kvp.Key, kvp.Value);
                    }
                    catch (Exception)
                    {
                        //Logger.Debug<UHateoas>("Node property error: \"{0}\"", ex.Message);
                    }
                }

                if (publish)
                {
                    var result = ContentService.SaveAndPublish(updateNode, "*", CurrentUser.Id);
                    node = UmbracoHelper.Content(result.Content.Id);
                }
                else
                {
                    ContentService.Save(updateNode, CurrentUser.Id);
                    node = UmbracoHelper.Content(updateNode.Id);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("UpdateNode Error " + ex.Message);
            }

            return node;
        }

        private IPublishedContent CreateNode(IPublishedContent model, string docType, bool publish)
        {
            IPublishedContent node;
            try
            {
                string json = GetPostedJson();
                Dictionary<string, object> form = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (form == null || !form.ContainsKey("Name"))
                    throw new Exception("Name form element is required");

                IContent parentNode = ContentService.GetById(model.Id);
                IContent newNode = ContentService.CreateContent(form["Name"].ToString(), parentNode.GetUdi(), docType, CurrentUser.Id);

                if (newNode == null)
                    throw new Exception("New Node is null");

                if (form.ContainsKey("ExpireDate"))
                    newNode.ContentSchedule.Add(new ContentSchedule("*", (DateTime)form["ExpireDate"], ContentScheduleAction.Expire));

                if (form.ContainsKey("ReleaseDate"))
                    newNode.ContentSchedule.Add(new ContentSchedule("*", (DateTime)form["ReleaseDate"], ContentScheduleAction.Release));

                foreach (Property prop in newNode.Properties)
                {
                    try
                    {
                        KeyValuePair<string, object> kvp = GetValuePair(prop.Alias, form, newNode);
                        if (kvp.Value != null)
                            newNode.SetValue(kvp.Key, kvp.Value);
                    }
                    catch (Exception)
                    {
                        //Logger.Debug<UHateoas>("New node property error: \"{0}\"", ex.Message);
                    }
                }

                if (publish)
                {
                    PublishResult result = ContentService.SaveAndPublish(newNode, "*", CurrentUser.Id);
                    node = UmbracoHelper.Content(result.Content.Id);
                }
                else
                {
                    ContentService.Save(newNode, CurrentUser.Id);
                    node = model;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("CreateNode Error " + ex.Message);
            }

            return node;
        }

        private Dictionary<string, object> GenerateForm(IPublishedContent node, string docTypeAlias)
        {
            try
            {
                Dictionary<string, object> properties = new Dictionary<string, object>();
                SortedSet<string> classes = new SortedSet<string>();
                string title = "";
                IContentType doc = ContentTypeService.GetAll().FirstOrDefault(x => x.Alias == docTypeAlias);

                if (RequestAction == "create")
                {
                    if (doc != null)
                    {
                        docTypeAlias = doc.Alias;
                        title = "New " + docTypeAlias;
                        AddContentTypeProperties(doc, properties, DataTypeService, null);
                        while (doc.ParentId != -1)
                        {
                            doc = ContentTypeService.GetAll().FirstOrDefault(x => x.Id == doc.ParentId);
                            AddContentTypeProperties(doc, properties, DataTypeService, null);
                        }

                        properties.Add("Name", new
                        {
                            description = "Name for the Node",
                            group = "Properties",
                            manditory = true,
                            propertyEditor = "Umbraco.Textbox",
                            title = "Name",
                            type = "text",
                            validation = "([^\\s]*)",
                            value = ""
                        });

                        properties.Add("ExpiryDate", new
                        {
                            description = "Date for the Node to be Unpublished",
                            group = "Properties",
                            manditory = false,
                            propertyEditor = "date",
                            title = "Expriry Date",
                            type = "text",
                            validation = "",
                            value = ""
                        });

                        properties.Add("ReleaseDate", new
                        {
                            description = "Date for the Node to be Published",
                            group = "Properties",
                            manditory = false,
                            propertyEditor = "date",
                            title = "Release Date",
                            type = "text",
                            validation = "",
                            value = ""
                        });
                    }
                }

                if (RequestAction == "update")
                {
                    if (doc != null)
                    {
                        docTypeAlias = doc.Alias;
                        title = "Update " + docTypeAlias;
                        AddContentTypeProperties(doc, properties, DataTypeService, node);
                        while (doc.ParentId != -1)
                        {
                            doc = ContentTypeService.GetAll().FirstOrDefault(x => x.Id == doc.ParentId);
                            AddContentTypeProperties(doc, properties, DataTypeService, node);
                        }
                        properties.Add("Name", new
                        {
                            description = "Name for the Node",
                            group = "Properties",
                            manditory = true,
                            propertyEditor = "Umbraco.Textbox",
                            title = "Name",
                            type = "text",
                            validation = "([^\\s]*)",
                            value = node.Name
                        });

                        properties.Add("ExpiryDate", new
                        {
                            description = "Date for the Node to be Unpublished",
                            group = "Properties",
                            manditory = false,
                            propertyEditor = "date",
                            title = "Expiry Date",
                            type = "text",
                            validation = "",
                            value = ""
                        });

                        properties.Add("ReleaseDate", new
                        {
                            description = "Date for the Node to be Published",
                            group = "Properties",
                            manditory = false,
                            propertyEditor = "date",
                            title = "Release Date",
                            type = "text",
                            validation = "",
                            value = ""
                        });
                    }
                }

                if (RequestAction == "remove")
                {
                    if (doc != null)
                    {
                        docTypeAlias = doc.Alias;
                        title = "Update " + docTypeAlias;
                        AddContentTypeProperties(doc, properties, DataTypeService, node);
                        while (doc.ParentId != -1)
                        {
                            doc = ContentTypeService.GetAll().FirstOrDefault(x => x.Id == doc.ParentId);
                            AddContentTypeProperties(doc, properties, DataTypeService, node);
                        }

                        properties.Add("Name", new
                        {
                            description = "Name for the Node",
                            group = "Properties",
                            manditory = true,
                            propertyEditor = "Umbraco.Textbox",
                            title = "Name",
                            type = "text",
                            validation = "([^\\s]*)",
                            value = node.Name
                        });
                    }
                }

                classes.Add("x-form");
                classes.Add(docTypeAlias);
                properties.Add("class", classes.ToArray());
                properties.Add("title", title);
                properties.Add("properties", properties);

                return properties;
            }
            catch (Exception ex)
            {
                throw new Exception("GenerateForm Error " + ex.Message);
            }
        }

        private KeyValuePair<string, object> SimplyfyProperty(PropertyInfo prop, IPublishedContent node)
        {
            object val;
            try
            {
                val = prop.GetValue(node, null);
                val = ResolveMedia(prop.Name, val).ToString();
                if (!string.IsNullOrEmpty(RequestHtml) && string.Equals(RequestHtml, "false", StringComparison.OrdinalIgnoreCase))
                {
                    val = val.ToString().StripHtml();
                }
            }
            catch (Exception ex)
            {
                val = ex.Message;
            }

            string propTitle = Regex.Replace(prop.Name, "(\\B[A-Z])", " $1");
            if (SimpleJson)
                return new KeyValuePair<string, object>(prop.Name, val);

            return new KeyValuePair<string, object>(prop.Name, new { title = propTitle, value = val });
        }

        private HtmlHelper CreateHtmlHelper(object model)
        {
            var rc = Umbraco.Web.Composing.Current.UmbracoContext.HttpContext.Request.RequestContext;
            var controllerContext = new ControllerContext()
            {
                RequestContext = rc
            };
            var viewContext = new ViewContext(controllerContext, new FakeView(), new ViewDataDictionary(model), new TempDataDictionary(), new StringWriter());
            return new HtmlHelper(viewContext, new ViewPage());
        }

        private KeyValuePair<string, object> SimplifyProperty(IPublishedProperty prop, IPublishedContent node)
        {
            object val = prop.GetValue();
            var pubPropType = node.ContentType.GetPropertyType(prop.Alias);
            string propertyEditorAlias = pubPropType.Alias;
            string propName = prop.Alias;
            string propTitle = Regex.Replace(propName, "(\\B[A-Z])", " $1");
            if (val != null)
            {
                val = ResolveMedia(propName, val);
                val = ResolveToIds(propName, val);
                if (!string.IsNullOrEmpty(RequestHtml) && string.Equals(RequestHtml, "false", StringComparison.OrdinalIgnoreCase))
                    val = val.ToString().StripHtml();

                //if (val is IEnumerable<IPublishedContent>)
                //{
                //    var a = new List<int>();
                //    foreach (var i in val as IEnumerable<IPublishedContent>)
                //    {
                //        a.Add(i.Id);
                //    }
                //    val = a;
                        
                //}

                if (val.GetType().FullName.ToLower().IndexOf(PublishedModelsNamespace.ToLower()) >= 0)
                {
                    if (!string.IsNullOrEmpty(RequestResolveContent))
                    {
                        if (!RequestResolveContent.Split(',').Contains(propName))
                        {
                            val = ((PublishedContentModel) val).Id;
                        }
                    }
                    else
                    {
                        if (val is IEnumerable PublishedContentModel)
                        {
                            val = ""; // (from i in (val as IEnumerable<PublishedElementModel>) select i.Key).ToList();
                        }
                        else
                        {
                            val = ((PublishedContentModel)val).Id;
                        }
                    }
                }

                if (propertyEditorAlias == "Umbraco.MultipleTextstring")
                {
                    val = JsonConvert.SerializeObject(prop.GetValue() as string[], Formatting.Indented,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });
                }

                if (propertyEditorAlias == "Umbraco.NestedContent")
                {
                    var useAllProperties = false;
                    var propertyNames = new List<string>();
                    if (!string.IsNullOrEmpty(RequestSelect))
                    {
                        propertyNames = RequestSelect.ToLower().Split(',').ToList();
                    }

                    else
                        useAllProperties = true;

                    val = val == null ? null : prop.GetValue();
                    if (val != null)
                    {
                        var v = ((JArray)(JToken)JsonConvert.DeserializeObject(val.ToString())).Children();
                        var items = new List<Dictionary<string, object>>();

                        foreach (var contentPiece in v)
                        {
                            Dictionary<string, object> newProps = new Dictionary<string, object>();
                            foreach (JProperty x in contentPiece.Children())
                            {
                                if ((useAllProperties || propertyNames.Contains(x.Name.ToLower())) && (!(x.First() is JArray)))
                                {
                                    var item = x.First().ToString();
                                    if (item.IndexOf("umb://document", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                    {
                                        var udi = Udi.Parse(item);
                                        var content = UmbracoHelper.Content(udi);
                                        newProps.Add(x.Name,
                                            content != null
                                                ? JToken.FromObject(Simplify(content))
                                                : x.Value.ToString());
                                    }
                                    else if (item.IndexOf("umb://media", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                    {
                                        var udi = Udi.Parse(item);
                                        var media = UmbracoHelper.Media(udi);
                                        newProps.Add(x.Name, media != null ? media.Url : x.Value.ToString());
                                    }
                                    else
                                    {
                                        newProps.Add(x.Name, x.Value.ToString());
                                    }
                                }
                            }
                            items.Add(newProps);
                        }
                        val = items;
                    }
                }

                if (propertyEditorAlias == "modules")
                {
                    var model = prop.GetValue();
                    var html = CreateHtmlHelper(model);

                    if (model is string asString && string.IsNullOrEmpty(asString))
                    {
                        val = string.Empty;
                    }
                    else
                    {
                        val = html.GetGridHtml(node, prop.Alias, "GenesisGrid-fluid");
                    }
                }
            }
            if (SimpleJson)
                return new KeyValuePair<string, object>(prop.Alias.ToFirstUpper(), SetPropType(val, GetPropType(val)));

            return new KeyValuePair<string, object>(prop.Alias, new { title = propTitle, value = SetPropType(val, GetPropType(val)), type = GetPropType(val), propertyEditor = propertyEditorAlias });
        }

        private KeyValuePair<string, object> GetValuePair(string alias, Dictionary<string, object> form, IContent newNode)
        {
            KeyValuePair<string, object> val = new KeyValuePair<string, object>(alias, null);
            var propType = newNode.Properties.FirstOrDefault(p => p.Alias == alias);
            if (propType == null)
                return val;

            if (form[alias] == null)
                return val;

            IDataType dtd = DataTypeService.GetDataType(propType.Id);

            switch (dtd.DatabaseType)
            {
                case ValueStorageType.Date:
                    val = new KeyValuePair<string, object>(alias, DateTime.Parse(form[alias].ToString()));
                    break;
                case ValueStorageType.Integer:
                    val = new KeyValuePair<string, object>(alias, Int32.Parse(form[alias].ToString()));
                    break;
                default:
                    val = new KeyValuePair<string, object>(alias, form[alias].ToString());
                    break;
            }
            return val;
        }

        private List<object> GetChildrenActions(IPublishedContent node)
        {
            Dictionary<string, object> action = new Dictionary<string, object>();
            List<object> actions = new List<object>();
            SortedSet<string> classes = new SortedSet<string>();
            if (CanCreate || CanUpdate || CanDelete)
            {
                BuildGetActions(node, ref action, actions, ref classes);
                BuildPostAction(node, ref action, actions, ref classes);
            }
            return actions;
        }

        private List<object> ProcessTakeSkip(List<object> entities)
        {
            if (!string.IsNullOrEmpty(RequestSkip) && RequestSkip.IsNumeric() && !string.IsNullOrEmpty(RequestTake) && RequestTake.IsNumeric())
            {
                return entities.Skip(RequestSkip.AsInteger()).Take(RequestTake.AsInteger()).ToList();
            }

            if (!string.IsNullOrEmpty(RequestTake) && RequestTake.IsNumeric())
            {
                return entities.Take(RequestTake.AsInteger()).ToList();
            }

            if (!string.IsNullOrEmpty(RequestSkip) && RequestSkip.IsNumeric())
            {
                return entities.Skip(RequestSkip.AsInteger()).ToList();
            }

            return entities;
        }

        private IEnumerable<IPublishedContent> SortedData(IEnumerable<IPublishedContent> data)
        {
            IEnumerable<IPublishedContent> sortedData = new List<IPublishedContent>();

            if (!string.IsNullOrEmpty(RequestOrderByDesc))
            {
                switch (RequestOrderByDesc.ToLower())
                {
                    case "updatedate":
                        sortedData = data.OrderByDescending(x => x.UpdateDate);
                        break;
                    case "name":
                        sortedData = data.OrderByDescending(x => x.Name);
                        break;
                    case "createdate":
                        sortedData = data.OrderByDescending(x => x.CreateDate);
                        break;
                    case "sortorder":
                        sortedData = data.OrderByDescending(x => x.SortOrder);
                        break;
                    default:
                        sortedData = data.OrderByDescending(x => x.GetProperty(RequestOrderByDesc).Alias.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 ? (x.HasValue(RequestOrderByDesc) ? x.GetProperty(RequestOrderByDesc).Value<DateTime>().ToString("yyyyMMddHHmm") : DateTime.MinValue.ToString("yyyyMMddHHmm")) : x.GetProperty(RequestOrderByDesc).Value<string>() ?? "");
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(RequestOrderBy))
            {
                switch (RequestOrderBy.ToLower())
                {
                    case "updatedate":
                        sortedData = data.OrderBy(x => x.UpdateDate);
                        break;

                    case "name":
                        sortedData = data.OrderBy(x => x.Name);
                        break;

                    case "createdate":
                        sortedData = data.OrderBy(x => x.CreateDate);
                        break;

                    case "sortorder":
                        sortedData = data.OrderBy(x => x.SortOrder);
                        break;
                    default:
                        sortedData = data.OrderBy(x => x.GetProperty(RequestOrderBy).Alias.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 ?
                            (x.HasValue(RequestOrderBy) ? x.GetProperty(RequestOrderBy).Value<DateTime>().ToString("yyyyMMddHHmm") : DateTime.MinValue.ToString("yyyyMMddHHmm")) : x.GetProperty(RequestOrderBy).Value<string>() ?? "");
                        break;
                }
            }
            else
            {
                sortedData = data.OrderBy(x => x.SortOrder);
            }

            return sortedData;
        }

        private List<object> GetDescendantEntities(IPublishedContent model)
        {
            List<object> entities = new List<object>();
            if (model != null && !string.IsNullOrEmpty(RequestDescendants))
            {
                IEnumerable<IPublishedContent> descendants;
                string descendantsAlias = RequestDescendants;

                if (descendantsAlias == "")
                    descendants = model.Descendants();

                else if (descendantsAlias.IsNumeric())
                    descendants = model.Descendants(System.Int32.Parse(descendantsAlias));

                else if (descendantsAlias.Contains(","))
                {
                    var listDescendants = new List<IPublishedContent>();
                    foreach (var descendantAlias in descendantsAlias.Split(','))
                    {
                        listDescendants.AddRange(model.Descendants(descendantAlias));
                    }
                    descendants = listDescendants;
                }
                else
                    descendants = model.Descendants(descendantsAlias);

                // TODO: Resolve sortedData
                //var descendantListSortedData = SortedData(!string.IsNullOrEmpty(RequestWhere) ? descendants.Where(RequestWhere.ChangeBinary()) : descendants).ToList();
                var descendantList = descendants;
                List<object> descendantObjectList = descendantList.Select(x => Simplify(x)).Cast<object>().ToList();
                entities.AddRange(descendantObjectList);
            }
            return ProcessTakeSkip(entities);
        }

        private List<object> GetChildrenEntities(IPublishedContent currentModel)
        {
            List<object> entities = new List<object>();
            if (currentModel != null && !string.IsNullOrEmpty(RequestChildren))
            {
                IEnumerable<IPublishedContent> children = currentModel.Children;
                List<IPublishedContent> childList = new List<IPublishedContent>();
                if (!string.IsNullOrEmpty(RequestWhere))
                {
                    //childList = children.Where(RequestWhere.ChangeBinary()).OrderBy(x => x.SortOrder).ToList();
                    childList.AddRange(children.OrderBy(x => x.SortOrder));
                }
                else
                {
                    childList.AddRange(children.OrderBy(x => x.SortOrder));
                }
                // TODO: Resolve sortedData
                //childList = SortedData(!string.IsNullOrEmpty(RequestWhere) ? children.Where(RequestWhere.ChangeBinary()) : children).ToList();
                List<object> childObjectList = childList.Select(x => Simplify(x)).Cast<object>().ToList();
                entities.AddRange(childObjectList);
            }
            return ProcessTakeSkip(entities);
        }

        private object ResolveMedia(string name, object property)
        {
            if (RequestResolveMedia.Contains(name.ToLower()))
            {
                try
                {
                    if (property != null && property.ToString().Contains(","))
                    {
                        property = string.Join(",",
                            property.ToString().Split(',').Select(x => UmbracoHelper.Media(x)?.Url)
                                .ToArray());
                    }
                    else if (property is IEnumerable<IPublishedContent> enumerable && enumerable.Any())
                    {
                        var items = new List<string>();
                        foreach (var mItem in enumerable)
                        {
                            if (mItem != null && !string.IsNullOrEmpty(mItem.Url))
                            {
                                items.Add(mItem.Url);
                            }
                        }
                        property = items.ToArray();
                    }
                    else if (property is IPublishedContent)
                    {
                        IPublishedContent mItem = property as IPublishedContent;
                        property = mItem.Url;
                    }
                }
                catch (Exception)
                {
                    //Logger.Error(MethodBase.GetCurrentMethod().DeclaringType, ex);
                    property = "#";
                }
            }
            else if (property is IPublishedContent && (property as IPublishedContent).ItemType == PublishedItemType.Media)
            {
                IPublishedContent mItem = property as IPublishedContent;
                property = mItem.Id;
            }

            return property;
        }

        private object ResolveToIds(string name, object property)
        {
            if (!string.IsNullOrEmpty(RequestResolveToIds))
            {
                foreach (var key in RequestResolveToIds.Split(','))
                {
                    if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            if (property is IEnumerable<IPublishedContent> contents)
                            {
                                if (contents.Any())
                                {
                                    var a = (IEnumerable<IPublishedContent>)property;
                                    property = a.Select(x => x.Id).ToArray();
                                }
                                else
                                {
                                    property = new object();
                                }
                            }
                            else if (property is IPublishedContent content)
                            {
                                property = content.Id.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            property = ex.Message;
                        }
                    }
                }
            }
            return property;
        }

        private object SetPropType(object val, string guessType)
        {
            switch (guessType)
            {
                case "number":
                    return System.Int32.Parse(val.ToString());

                case "checkbox":
                    return val.ToString() == "True";

                case "htmlstring":
                    return val.ToString();

                case "dynamic":
                case "array":
                    return val;

                default:
                    return val;
            }
        }

        private string GetPostedJson()
        {
            StreamReader reader = new StreamReader(Context.Request.InputStream);
            return reader.ReadToEnd();
        }

        private string GetPropType(object val)
        {
            if (val == null)
                return "text";

            if (val is string)
            {
                if (val.ToString() == "True" || val.ToString() == "False")
                    return "checkbox";

                if (val.ToString().Contains("-") && val.ToString().Contains("T") && val.ToString().Contains(":"))
                    return "date";

                if (val.ToString().IsNumeric() && System.Int32.TryParse(val.ToString(), out _))
                    return "number";
            }

            if (val is int)
            {
                return "number";
            }

            if (val is bool)
            {
                return "checkbox";
            }

            if (val is DateTime)
            {
                return "date";
            }

            if (val is Array)
            {
                return "array";
            }

            if (val is HtmlString)
            {
                return "htmlstring";
            }

            if (val is DynamicObject)
            {
                return "dynamic";
            }

            return "text";
        }

        private string GetHateoasHref(IPublishedContent node, object queryString)
        {
            string[] segments = Context.Request.Url.Segments;
            string lastSegment = segments.LastOrDefault();
            string template = "";

            if (lastSegment != null && lastSegment != "/" && lastSegment != MainModel.Name && lastSegment != MainModel.Name + "/")
                template = lastSegment;

            string href = $"{Context.Request.Url.Scheme + "://"}{Context.Request.Url.Host}{node.Url}{template}";

            if (queryString != null)
            {
                PropertyInfo[] nvps = queryString.GetType().GetProperties();
                href = nvps.Aggregate(href, (current, nvp) => current + ((current.Contains("?") ? "&" : "?") + nvp.Name + "=" + nvp.GetValue(queryString, null)));
            }

            return href;
        }

        private bool HasAccess(IPublishedContent node)
        {
            IPublicAccessService publicAccessService = Umbraco.Core.Composing.Current.Services.PublicAccessService;
            if (node != null && publicAccessService.IsProtected(node.Path))
            {
                return UmbracoHelper.MemberHasAccess(node.Path);
            }
            return true;
        }

        private static string GetSimpleType(IDataType dtd)
        {
            string val;
            switch (dtd.DatabaseType)
            {
                case ValueStorageType.Date:
                    val = "date";
                    break;
                case ValueStorageType.Integer:
                    val = "number";
                    break;
                default:
                    val = "text";
                    break;
            }
            return val;
        }

        private static void AddContentTypeProperties(IContentType newDoc, Dictionary<string, object> properties, IDataTypeService dataTypeService, IPublishedContent node)
        {
            if (newDoc?.PropertyGroups != null)
            {
                foreach (PropertyGroup propGroup in newDoc.PropertyGroups)
                {
                    foreach (PropertyType propType in propGroup.PropertyTypes)
                    {
                        if (!properties.ContainsKey(propType.Alias))
                        {
                            var dtd = dataTypeService.GetAll().FirstOrDefault(x => x.Id == propType.DataTypeId);
                            var property = new Dictionary<string, object>
                            {
                                {"title", propType.Name},
                                {"value", node == null ? "" : node.GetProperty(propType.Alias).GetValue()},
                                {"group", propGroup.Name},
                                {"type", GetSimpleType(dtd)},
                                {"manditory", propType.Mandatory},
                                {"validation", propType.ValidationRegExp},
                                {"description", propType.Description},
                                {"propertyEditor", propType.PropertyEditorAlias}
                            };
                            var prevalues = dataTypeService.GetDataType(propType.DataTypeId);
                            if (prevalues != null)
                            {
                                property.Add("prevalues", prevalues);
                            }
                            properties.Add(propType.Alias, property);
                        }
                    }
                }
            }
        }

        private void BuildGetActions(IPublishedContent node, ref Dictionary<string, object> action, List<object> actions, ref SortedSet<string> classes)
        {
            if (!Context.Request.Params.AllKeys.Contains("action"))
            {
                IContentType currentContentType = ContentTypeService.Get(node.ContentType.Id);
                if (currentContentType != null && currentContentType.AllowedContentTypes.Any() && CanCreate)
                {
                    AllowedContentTypes = ContentTypeService.GetAll(currentContentType.AllowedContentTypes.Select(ct => ct.Id.Value).ToArray()).ToList();
                    if (AllowedContentTypes != null && AllowedContentTypes.Any())
                    {
                        foreach (IContentType ct in AllowedContentTypes)
                        {
                            classes = new SortedSet<string> { ct.Alias, "x-form" };
                            action = new Dictionary<string, object>
                            {
                                {"class", classes.ToArray()},
                                {"title", "Create @content".SmartReplace(new {content = ct.Name})},
                                {"method", "GET"},
                                {
                                    "href",
                                    GetHateoasHref(node, new {action = "create", doctype = ct.Alias, publish = "false"})
                                },
                                {"type", Context.Request.ContentType}
                            };
                            actions.Add(action);
                        }
                    }
                }

                if (CanUpdate || CanDelete)
                {
                    if (currentContentType != null)
                        classes = new SortedSet<string> { currentContentType.Alias, "x-form" };
                }

                if (CanUpdate)
                {
                    //update
                    action = new Dictionary<string, object>
                    {
                        {"class", classes.ToArray()},
                        {"title", "Update @content".SmartReplace(new {content = currentContentType?.Name})},
                        {"method", "GET"},
                        {
                            "href",
                            GetHateoasHref(node,
                                new {action = "update", doctype = currentContentType?.Alias, publish = "false"})
                        },
                        {"type", Context.Request.ContentType}
                    };
                    actions.Add(action);
                }

                if (CanDelete)
                {
                    //delete
                    action = new Dictionary<string, object>
                    {
                        {"class", classes.ToArray()},
                        {"method", "GET"},
                        {"title", "Remove @content".SmartReplace(new {content = currentContentType?.Name})},
                        {
                            "href",
                            GetHateoasHref(node,
                                new {action = "remove", doctype = currentContentType?.Alias, delete = "false"})
                        },
                        {"type", Context.Request.ContentType}
                    };
                    actions.Add(action);
                }
            }
        }

        private void BuildPostAction(IPublishedContent node, ref Dictionary<string, object> action, List<object> actions, ref SortedSet<string> classes)
        {
            if (!string.IsNullOrEmpty(RequestAction) && RequestAction == "create" && !string.IsNullOrEmpty(RequestDocType))
            {
                IContentType currentContentType = ContentTypeService.Get(node.ContentType.Id);
                if (currentContentType != null && currentContentType.AllowedContentTypes.Any())
                {
                    AllowedContentTypes = ContentTypeService.GetAll(currentContentType.AllowedContentTypes.Select(ct => ct.Id.Value).ToArray()).ToList();
                    if (AllowedContentTypes != null && AllowedContentTypes.Any())
                    {
                        foreach (IContentType ct in AllowedContentTypes)
                        {
                            if (ct.Alias == RequestDocType)
                            {
                                classes = new SortedSet<string> { ct.Alias, "x-form" };
                                action = new Dictionary<string, object>
                                {
                                    {"class", classes.ToArray()},
                                    {"title", "Save @content".SmartReplace(new {content = ct.Name})},
                                    {"method", "POST"},
                                    {"action", GetHateoasHref(node, new {doctype = ct.Alias, publish = "true"})},
                                    {"type", Context.Request.ContentType}
                                };
                                actions.Add(action);
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(RequestAction) && RequestAction == "update")
            {
                IContentType ct = ContentTypeService.Get(node.ContentType.Id);
                if (ct.Alias == RequestDocType)
                {
                    classes = new SortedSet<string> { ct.Alias, "x-form" };
                    action = new Dictionary<string, object>
                    {
                        {"class", classes.ToArray()},
                        {"title", "Update @content".SmartReplace(new {content = ct.Name})},
                        {"method", "PUT"},
                        {"action", GetHateoasHref(node, new {doctype = ct.Alias, publish = "true"})},
                        {"type", Context.Request.ContentType}
                    };
                    actions.Add(action);
                }
            }

            if (!string.IsNullOrEmpty(RequestAction) && RequestAction == "remove")
            {
                IContentType ct = ContentTypeService.Get(node.ContentType.Id);
                if (ct.Alias == RequestDocType)
                {
                    classes = new SortedSet<string> { ct.Alias, "x-form" };
                    action = new Dictionary<string, object>
                    {
                        {"class", classes.ToArray()},
                        {"title", "Remove @content".SmartReplace(new {content = ct.Name})},
                        {"method", "DELETE"},
                        {"action", GetHateoasHref(node, new {doctype = ct.Alias, delete = "false"})},
                        {"type", Context.Request.ContentType}
                    };
                    actions.Add(action);
                }
            }

            if (!string.IsNullOrEmpty(RequestAction))
            {
                classes = new SortedSet<string> { node.ContentType.Alias };
                action = new Dictionary<string, object>
                {
                    {"class", classes.ToArray()},
                    {"title", "Cancel"},
                    {"method", "GET"},
                    {"action", GetHateoasHref(node, null)},
                    {"type", Context.Request.ContentType}
                };
                actions.Add(action);
            }
        }

        private void ResolveContent(SortedDictionary<string, object> properties)
        {
            if (!string.IsNullOrEmpty(RequestResolveContent))
            {
                foreach (string key in RequestResolveContent.Split(','))
                {
                    if (properties.ContainsKey(key))
                    {
                        try
                        {
                            if ((dynamic)properties[key] is int)
                            {
                                int nodeId = (Int32)properties[key];
                                if (nodeId != CurrentPageId && key != "Path")
                                    properties[key] = Simplify(UmbracoHelper.Content(nodeId));
                            }
                            else if ((dynamic)properties[key].GetType() == typeof(string))
                            {
                                List<object> content = new List<object>();
                                foreach (string node in ((string)properties[key]).Split(','))
                                {
                                    int nodeId = System.Int32.Parse(node);
                                    if (nodeId != CurrentPageId && key != "Path")
                                        content.Add(Simplify(UmbracoHelper.Content(nodeId)));
                                }
                                properties[key] = content;
                            }
                            else if ( (properties[key].GetType().BaseType.FullName.ToLower().IndexOf(PublishedModelsNamespace.ToLower()) >=0) ||
                                (properties[key].GetType().FullName.ToLower().IndexOf(PublishedModelsNamespace.ToLower()) >= 0) )
                            {
                                int nodeId = (properties[key] as PublishedContentModel).Id;
                                if (nodeId != CurrentPageId && key != "Path")
                                    properties[key] = Simplify(UmbracoHelper.Content(nodeId));
                            }
                        }
                        catch (Exception)
                        {
                            properties[key] = properties[key];
                        }
                    }
                }
            }
        }

        private static GuidUdi GetUdi(IContent content)
        {
            return new GuidUdi(content.ContentType.ToString(), content.Key);
        }

        private static GuidUdi GetUdi(IPublishedContent content)
        {
            return new GuidUdi(content.ContentType.ItemType.ToString(), content.Key);
        }
    }
}