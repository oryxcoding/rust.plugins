using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Cargo Scientists Randomize", "Oryx", "1.0.0")]
    [Description("Allow/prevent scientists from spawning on cargo ship with probability")]

    public class CargoScientistRandomize : RustPlugin
    {
        #region Fields
        private bool preventNPCSpawning = false;
        private bool hasCargoSpawned = false;
        private bool _debug = true;

        private bool canBroadcast;
        private int probability;

        private string prefab = "assets/prefabs/npc/scientist";
        #endregion

        #region Config
        ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Broadcast to chat")]
            public bool CanBroadCast { get; set; }
            [JsonProperty(PropertyName = "probability")]
            public int Probability { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                CanBroadCast = true,
                Probability = 70
            };

            Config.WriteObject(config, true);
            configData = Config.ReadObject<ConfigData>();
            LoadVariables();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadVariables();
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if(entity == null) { return; }

            if(entity is CargoShip)
            {
                if(_debug) { Puts("Cargo has spawned"); }

                hasCargoSpawned = true;

                if(RandomNumber() > probability)
                {
                    preventNPCSpawning = true;
                    if (canBroadcast) { BroadcastMessage("NoScientistOnCargo"); }
                }
                else
                {
                    preventNPCSpawning = false;
                    if (canBroadcast) { BroadcastMessage("ScientistOnCargo"); }
                }

                if (_debug) { Puts("Prevent SpawningNPC: " + preventNPCSpawning); }

                timer.Once(5f, () =>
                {
                    hasCargoSpawned = false;
                });
            }

            if (!hasCargoSpawned) { return; }

            if (preventNPCSpawning)
            {
                if (entity.PrefabName.Contains(prefab)) { entity.Kill(); if (_debug) { Puts("NPC Killed"); } }
            }
        }
        #endregion

        #region Methods
        public int RandomNumber()
        {
            return (UnityEngine.Random.Range(1, 100));
        }

        public void LoadVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            probability = configData.Probability;
            canBroadcast = configData.CanBroadCast;
        }

        public void BroadcastMessage(string key)
        {
            var players = BasePlayer.activePlayerList;
            foreach (BasePlayer player in players)
            {
                SendReply(player, lang.GetMessage(key, this));
            }
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoScientistOnCargo"] = "No Scientists onboard. They must have died from radiation !",
                ["ScientistOnCargo"] = "Scientists on onboard !"
            }, this);
        }
        #endregion
    }
}
