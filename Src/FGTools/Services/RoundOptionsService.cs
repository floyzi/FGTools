using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FG.Common;
using FG.Common.CMS;
using FGTools.Services.Logic;

namespace FGTools.Services
{
    internal class RoundOptionsService : FGTService
    {
        public class RoundOptionsJson
        {
            public int PlayerCount { get; set; }
            public int RoundSeed { get; set; }
            public bool TimeLimit { get; set; }
            public float TimeLimitLength { get; set; }
            public bool AllowCheckpoints { get; set; }
        }

        RoundOptionsJson latestOptions;

        public override void RegisterService()
        {
            LoadData();
        }

        public override void UpdateService()
        {

        }

        public void LoadData()
        {
            try
            {
                if (File.Exists(Plugin.RoundOptionsData))
                {
                    latestOptions = JsonSerializer.Deserialize<RoundOptionsJson>(File.ReadAllText(Plugin.RoundOptionsData));
                }
                else
                {
                    latestOptions = new()
                    {
                        AllowCheckpoints = true,
                        PlayerCount = 20,
                        RoundSeed = 0,
                        TimeLimit = false,
                        TimeLimitLength = 0,
                    };
                }
            }
            catch
            {
                File.Delete(Plugin.RoundOptionsData);
                LoadData();
            }
        }

        public RoundOptionsJson ReturnLatestOptions()
        {
            return latestOptions;
        }

        internal int GetSeedForGame() => latestOptions.RoundSeed == 0 ? UnityEngine.Random.Range(0, int.MaxValue) : latestOptions.RoundSeed;
        internal float GetRoundLength(GameRulesSchema round) => (latestOptions.TimeLimit && latestOptions.TimeLimitLength > 1 ? latestOptions.TimeLimitLength : round.Duration) + 5;

        public void WriteData() => File.WriteAllText(Plugin.RoundOptionsData, JsonSerializer.Serialize<RoundOptionsJson>(latestOptions));

        public override void DrawGUI()
        {
        }
    }
}
