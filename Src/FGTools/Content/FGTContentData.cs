using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using FGTools.Content.Attributes;
using FGTools.Content.ContentImpl;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FGTools.Content
{
    public class FGTContentData
    {
        public FGTConfig Config;
        public FGTMeta Meta;
        public FGTNewsfeedData Newsfeeds;
        public FGTRandomImages RandomImages;
        public FGTThemesData ThemeData;
        public FGTLocalesData LocaleConfig;
        public FGTUserShowsData UserShows;
        public FGTChangelogData Changelog;
        public FGTCreditsData Credits;
        public FGTLangCodes LangCodes;
        public FGTTargetSettingsData TargetSettings;
        public string ContentVersion => contentVersion;
        string contentVersion = string.Empty;

        public FGTContentData(string json)
        {
            var doc = JsonDocument.Parse(json);

            contentVersion = doc.RootElement.GetProperty("meta").GetProperty("content_version").GetString();

            foreach (var field in this.GetType().GetFields())
            {
                var groupAttr = field.FieldType.GetCustomAttribute<FGTGroup>();
                var type = field.FieldType;
                var baseType = type.BaseType;

                if (groupAttr != null && doc.RootElement.TryGetProperty(groupAttr.Name, out var groupJson))
                {
                    //FGTLog(BepInEx.Logging.LogLevel.Info, GetType(), $"got in json = {type} ({baseType.IsGenericType})");

                    var typeInst = Activator.CreateInstance(type);

                    if (baseType.IsGenericType)
                    {
                        var genericDef = baseType.GetGenericTypeDefinition();

                        if (genericDef == typeof(Dictionary<,>))
                        {
                            var valueType = baseType.GetGenericArguments()[1];
                            var dict = (IDictionary)Activator.CreateInstance(type);

                            if (groupJson.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in groupJson.EnumerateArray())
                                {
                                    var keyProperty = item.GetProperty("id").GetString();
                                    var value = Activator.CreateInstance(valueType);

                                    foreach (var prop in valueType.GetProperties())
                                    {
                                        var fieldAttr = prop.GetCustomAttribute<FGTField>();
                                        if (fieldAttr != null && item.TryGetProperty(fieldAttr.JsonKey, out var fieldJson))
                                        {
                                            if (prop.PropertyType == typeof(string))
                                                prop.SetValue(value, fieldJson.GetString());

                                            else if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                                            {
                                                var listItemType = prop.PropertyType.GetGenericArguments()[0];
                                                var listInstance = (IList)Activator.CreateInstance(prop.PropertyType);

                                                foreach (var listJson in fieldJson.EnumerateArray())
                                                {
                                                    object listEl;

                                                    if (listItemType == typeof(string))
                                                        listEl = listJson.GetString();
                                                    else
                                                    {
                                                        listEl = Activator.CreateInstance(listItemType);

                                                        foreach (var listProp in listItemType.GetProperties())
                                                        {
                                                            var listFieldAttr = listProp.GetCustomAttribute<FGTField>();
                                                            if (listFieldAttr != null && listJson.TryGetProperty(listFieldAttr.JsonKey, out var listFieldJson))
                                                                listProp.SetValue(listEl, JsonSerializer.Deserialize(listFieldJson.GetRawText(), listProp.PropertyType));
                                                        }
                                                    }

                                                    listInstance.Add(listEl);
                                                }

                                                prop.SetValue(value, listInstance);
                                            }
                                            else
                                            {
                                                prop.SetValue(value, JsonSerializer.Deserialize(fieldJson.GetRawText(), prop.PropertyType));
                                            }
                                        }
                                    }

                                    dict.Add(keyProperty, value);
                                }
                            }

                            field.SetValue(this, dict);
                        }


                        if (genericDef == typeof(List<>))
                        {
                            var itemType = baseType.GetGenericArguments()[0];
                            var list = (IList)typeInst;

                            if (groupJson.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in groupJson.EnumerateArray())
                                {
                                    var listItem = Activator.CreateInstance(itemType);

                                    foreach (var prop in itemType.GetProperties())
                                    {
                                        var fieldAttr = prop.GetCustomAttribute<FGTField>();
                                        if (fieldAttr != null && item.TryGetProperty(fieldAttr.JsonKey, out var fieldJson))
                                        {
                                            if (prop.PropertyType.IsGenericType && (prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>) || prop.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                                            {
                                                var propType = prop.PropertyType.GetGenericArguments()[0];
                                                var propInst = (IList)Activator.CreateInstance(prop.PropertyType);

                                                foreach (var itm2 in fieldJson.EnumerateArray())
                                                {
                                                    var objInst = Activator.CreateInstance(propType);

                                                    foreach (var nestedProp in propType.GetProperties())
                                                    {
                                                        var propFieldAttr = nestedProp.GetCustomAttribute<FGTField>();
                                                        if (propFieldAttr != null && itm2.TryGetProperty(propFieldAttr.JsonKey, out var nestedFieldJson))
                                                            nestedProp.SetValue(objInst, JsonSerializer.Deserialize(nestedFieldJson.GetRawText(), nestedProp.PropertyType));
                                                    }
                                                    propInst.Add(objInst);
                                                }
                                                prop.SetValue(listItem, propInst);
                                            }
                                            else
                                                prop.SetValue(listItem, JsonSerializer.Deserialize(fieldJson.GetRawText(), prop.PropertyType));
                                        }
                                    }
                                    list.Add(listItem);
                                }
                            }
                            field.SetValue(this, list);
                        }
                    }
                    else
                    {
                        foreach (var prop in type.GetProperties())
                        {
                            var fieldAttr = prop.GetCustomAttribute<FGTField>();
                            if (fieldAttr != null && groupJson.TryGetProperty(fieldAttr.JsonKey, out var fieldJson))
                            {
                                prop.SetValue(typeInst, JsonSerializer.Deserialize(fieldJson.GetRawText(), prop.PropertyType));
                            }
                        }
                        field.SetValue(this, typeInst);
                    }
                }
            }

            FGTTargetSettings.Load(TargetSettings);
        }
    }
    }
