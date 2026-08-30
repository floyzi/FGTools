extern alias wle;
using FG.Common;
using FGTools.Services;
using FGTools.States;
using FGTools.States.Logic;
using Levels.Progression;
using System.Linq;
using UnityEngine;

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
            Vector3 pos;
            Quaternion rot;

            if (Config.Config.RespawnAtCheckpoint.Value)
            {
                var point = FGTStateManager.StateManager.GetState<GameplayState>().Spawnpoint;
                pos = point.transform.position;
                rot = point.transform.rotation;
            }
            else
            {
                var point = CGM.GameRules.PickRespawnPosition(0, 0, -1, -1, false);
                pos = point.transform.position;
                rot = point.transform.rotation;
            }

            return new(pos, rot);
        }

        void OnDestroy()
        {
            _respawnPos = -999;
        }
    }
}
