extern alias wle;

using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FGTools.LocalServer;
using FGTools.Services;
using FGTools.States.Logic;
using Levels.Progression;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.Internal.Behaviours.ServerSide
{
    internal class ServerBehaviour : ToolsBehaviour
    {
        internal struct RespawnLocation(Vector3 pos, Quaternion rot)
        {
            public Vector3 Position = pos + new Vector3(0, 2, 0);
            public Quaternion Rotation = rot;
        }

        static float _respawnPos = -999;

        void Update()
        {
            if (FGTBase.CGM != null && FGTBase.CGM.IsTimerEnded())
            {
                LocalServerService.ServerManager.EndRound(true);
                enabled = false;
            }
        }

        internal static float GetRespawnPos()
        {
            if (_respawnPos != -999)
                return _respawnPos;

            _respawnPos = -75f;

            if (CGM == null)
                return _respawnPos;

            if (!CGM.IsUGCRound)
            {
                foreach (StaticGeometryHashID testCol in Resources.FindObjectsOfTypeAll<StaticGeometryHashID>())
                {
                    if (testCol.transform.position.y < _respawnPos)
                    {
                        _respawnPos = testCol.transform.position.y;
                    }
                }

                return _respawnPos;
            }
            else
            {
                _respawnPos = float.MinValue;
                var potentialPos = Resources.FindObjectsOfTypeAll<CheckpointManager>().FirstOrDefault();

                if (potentialPos != null)
                    _respawnPos = potentialPos.transform.position.y;
                else if (!CGM.GameRules.IsSurvivalRound)
                {
                    foreach (wle.LevelEditorPlaceableObject obj in wle.LevelIO.PlaceableObjects._dictionary.Values)
                    {
                        try
                        {
                            float y;

                            if (obj.KillPlaneBounds == wle.LevelEditorPlaceableObject.EKillPlaneBounds.UseRenderBounds)
                                y = wle.FG.Common.LevelEditorManager.GetLowestPointForPlaceableObjectUsingRenderBounds(obj);
                            else
                                y = obj.GetWorldSpaceAxisAlignedBounds().min.y;

                            _respawnPos = Mathf.Min(_respawnPos, y);
                        }
                        catch { }

                    }
                }
                else
                    _respawnPos = -200;

                return _respawnPos;
            }
        }

        internal static RespawnLocation GetRespawnLocation()
        {
            var pos = CGM.GameRules.PickRespawnPosition(0, 0, -1, -1, false);
            return new(pos.transform.position, pos.transform.rotation);
        }

        void OnDestroy()
        {
            _respawnPos = -999;
        }
    }
}
