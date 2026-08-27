#if LAN_MULTIPLAYER
using BepInEx.Logging;
using FG.Common.CMS;
using FGTools.Content;
using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI.Tabs.Logic;
using System;
using UnityEngine;
using UniverseLib.UI;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.FGToolsUI;

namespace FGTools.UI.Tabs
{
    internal class LANMultiplayTab : UITab<LocalServerService>
    {
        public LANMultiplayTab() : base(Tab.LocalMultiplayer, FGTServiceManager.GetService<LocalServerService>())
        {

        }

        internal override void Draw(GameObject root)
        {
            string round2play = "round_gauntlet_01";

            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 5, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleHeight: 0);

            #region LOCAL MULTIPLAYER DEV
            FGToolsUI.Instance.TryDrawUI(() => FGTTargetSettings.LocalMultiplayer, ControlledObject, new(() =>
            {
                var fields = UIFactory.CreateHorizontalGroup(ControlledObject, "HostFields", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                UIFactory.SetLayoutElement(fields, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                var ipField = UIFactory.CreateInputField(fields, "ipField", LocalizedStr("lan_ip_field_holder"));
                ipField.Text = "127.0.0.1";
                ipField.Component.characterLimit = 15;
                UIFactory.SetLayoutElement(ipField.GameObject, minHeight: 25, flexibleHeight: 25, flexibleWidth: 20, minWidth: 20, preferredWidth: 20);

                var portField = UIFactory.CreateInputField(fields, "portField", LocalizedStr("lan_port_field_holder"));
                portField.Text = "1002";
                portField.Component.characterLimit = 4;
                UIFactory.SetLayoutElement(portField.GameObject, minHeight: 25, flexibleHeight: 25, flexibleWidth: 20, minWidth: 20, preferredWidth: 20);

                var lobbySize = UIFactory.CreateInputField(fields, "lobbySize", LocalizedStr("lan_players_field_holder"));
                lobbySize.Text = "1";
                lobbySize.Component.characterLimit = 2;
                UIFactory.SetLayoutElement(lobbySize.GameObject, minHeight: 25, flexibleHeight: 25, flexibleWidth: 20, minWidth: 20, preferredWidth: 20);

                var roundField = UIFactory.CreateInputField(ControlledObject, "joinServ", $"id of round to play on");
                roundField.Text = round2play;
                roundField.OnValueChanged += (string s) =>
                {
                    round2play = s;
                };
                UIFactory.SetLayoutElement(roundField.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                var actionButtons = UIFactory.CreateHorizontalGroup(ControlledObject, "HostFields", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                UIFactory.SetLayoutElement(actionButtons, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                var hostServ = UIFactory.CreateButton(actionButtons, "hostServ", LocalizedStr("gui_server_host"));
                hostServ.OnClick += () =>
                {
                    if (!string.IsNullOrEmpty(ipField.Text) && !string.IsNullOrEmpty(portField.Text) && !string.IsNullOrEmpty(lobbySize.Text))
                        FGTBase.FGTServiceManager.GetService<LocalServerService>().Host(ipField.Text, Convert.ToInt32(portField.Text), Convert.ToInt32(lobbySize.Text), CMSLoader.Instance.CMSData.Rounds[round2play]);
                };
                UIFactory.SetLayoutElement(hostServ.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                var joinServ = UIFactory.CreateButton(actionButtons, "joinServ", LocalizedStr("gui_server_join"));
                joinServ.OnClick += () =>
                {
                    if (!string.IsNullOrEmpty(ipField.Text) && !string.IsNullOrEmpty(portField.Text))
                        LocalServerService.Join(ipField.Text, Convert.ToInt32(portField.Text));
                };
                UIFactory.SetLayoutElement(joinServ.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                var terminateServ = UIFactory.CreateButton(ControlledObject, "joinServ", LocalizedStr("gui_server_shutdown"));
                terminateServ.OnClick += () =>
                {
                    Service.ShutdownSerer(new(() =>
                    {

                    }));
                };
                UIFactory.SetLayoutElement(terminateServ.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                var allRounds = UIFactory.CreateButton(ControlledObject, "allRounds", $"Print all rounds in console");
                allRounds.OnClick += () =>
                {
                    foreach (var r in CMSLoader.Instance.CMSData.Rounds)
                        FGTLog(LogLevel.Info, GetType(), $"{r.key} - {r.value.Archetype._name} - {r.value.GetSceneName()}");
                };
                UIFactory.SetLayoutElement(allRounds.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);
            }));
            #endregion
        }


        internal override void Refresh()
        {

        }
    }
}
#endif