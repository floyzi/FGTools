using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using Events;
using FG.Common;
using FGClient;
using FGTools.Internal;
using FGTools.Services.Logic;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.Services
{
    internal class ControllersDataService : FGTService
    {
        public Dictionary<string, Dictionary<string, object>> ControllerDatas = [];
        public CharacterControllerData ActiveControllerData;
        static List<ControllerElement> ControllerData;
        struct ControllerElement(string propName, object defaultValue, Action<CharacterControllerData, object> task, Type elementType)
        {
            public string PropName = propName;
            public object DefaultValue = defaultValue;
            public Action<CharacterControllerData, object> Set = task;
            public Type ElementType = elementType;
#if DEV
            public override readonly string ToString()
            {
                return $"{PropName} | {DefaultValue}";
            }
#endif
        }

        string LastUsedPreset;

        public override void RegisterService()
        {
            Broadcaster.Instance.Register<OnMainMenuDisplayed>(new Action<OnMainMenuDisplayed>(OnEnterMenu));
            GameActions.OnIntroStarts += OnIntroStarts;

            if (File.Exists(Launcher.ControllerDatasList))
                ControllerDatas = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(File.ReadAllText(Launcher.ControllerDatasList));
        }

        void OnEnterMenu(OnMainMenuDisplayed evt)
        {
            if (ActiveControllerData != null)
                return;

            ActiveControllerData = Resources.FindObjectsOfTypeAll<CharacterControllerData>().FirstOrDefault();
            var props = ActiveControllerData.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            ControllerData = new(props.Length);
            
            foreach (var prop in props)
            {
                if (!prop.CanWrite)
                    continue;

                if (prop.PropertyType == typeof(float))
                    AddControllerDataOfType<float>(ActiveControllerData, prop);

                if (prop.PropertyType == typeof(int))
                    AddControllerDataOfType<int>(ActiveControllerData, prop);

                if (prop.PropertyType == typeof(Vector3))
                    AddControllerDataOfType<Vector3>(ActiveControllerData, prop);
            }

        }

        void OnIntroStarts()
        {
            GlobalGameStateClient.Instance.GameStateView.GetCharacterDataMonitor()._timeToRunNextCharacterControllerDataCheck = float.MaxValue;
            foreach (var afk in Resources.FindObjectsOfTypeAll<AFKManager>())
                UnityEngine.Object.Destroy(afk);
        }

        public void SetDataPreset(string name)
        {
            if (name == LastUsedPreset)
                return;

            if (name == "Default")
            {
                ResetToDefault();
                return;
            }

            if (!ControllerDatas.TryGetValue(name, out var data))
            {
                FGTLog(BepInEx.Logging.LogLevel.Warning, GetType().Name, $"No preset were found for name \"{name}\"");
                return;
            }

            LastUsedPreset = name;

            foreach (var element in data)
            {
                var target = GetControllerElement(element.Key);

                if (string.IsNullOrEmpty(target.PropName))
                    continue;

                FGTLog(BepInEx.Logging.LogLevel.Debug, GetType(), $"Setting {target.PropName} from {target.DefaultValue} to {element.Value}");

                var actualElement = (JsonElement)element.Value;

                if (target.ElementType == typeof(float))
                    target.Set.Invoke(ActiveControllerData, actualElement.GetSingle());

                if (target.ElementType == typeof(int))
                    target.Set.Invoke(ActiveControllerData, actualElement.GetInt32());

                if (target.ElementType == typeof(Vector3))
                {
                    var arr = actualElement.EnumerateArray().ToArray();
                    target.Set.Invoke(ActiveControllerData, new Vector3(arr[0].GetSingle(), arr[1].GetSingle(), arr[2].GetSingle()));
                }
            }
        }

        internal void ResetToDefault()
        {
            foreach (var element in ControllerData)
            {
                element.Set.Invoke(ActiveControllerData, element.DefaultValue);
            }
        }

        static void AddControllerDataOfType<T>(CharacterControllerData charData, PropertyInfo prop)
        {
            var instance = Expression.Parameter(typeof(CharacterControllerData), "instance");
            var val = Expression.Parameter(typeof(object), "value");

            ControllerData.Add(new ControllerElement(prop.Name, prop.GetValue(charData), Expression.Lambda<Action<CharacterControllerData, object>>(Expression.Assign(Expression.Property(instance, prop), Expression.Convert(val, typeof(T))), instance, val).Compile(), prop.PropertyType));
        }

        static ControllerElement GetControllerElement(string name) => ControllerData.Find(x => x.PropName == name);

#if DEV
        public void DumpControllerData()
        {
            var outp = Paths.PluginPath + "\\data.json";
            var dict = new Dictionary<string, object>();
            var props = ActiveControllerData.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var prop in props)
            {
                if (!prop.CanWrite)
                    continue;

                object value = prop.GetValue(ActiveControllerData);
                FGTLog(BepInEx.Logging.LogLevel.Info, base.GetType(), $"{prop.Name} - {value}");
                if (prop.PropertyType == typeof(Vector3))
                {
                    Vector3 v = (Vector3)value;
                    dict.Add(prop.Name, new float[] { v.x, v.y, v.z });
                }

                else if (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(int))
                {
                    dict.Add(prop.Name, value);
                }
            }

            File.WriteAllText(outp, JsonSerializer.Serialize(dict));
        }
#endif
        public override void DrawGUI()
        {
        }

        public override void UpdateService()
        {
        }

        public override void OnAppFocus(bool focus)
        {

        }

        public override void OnAppQuit()
        {

        }
    }
}
