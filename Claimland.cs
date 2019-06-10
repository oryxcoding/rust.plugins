using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("Claim land", "Oryx", "1.0.0")]
    [Description("Allow players to claim/buy lands.")]

    //add /land show 3d for users.
    //create border. Creates a delopable code lock where you can access the land command.

   
    public class Claimland : RustPlugin
    {
        //You stopped at list.
        //todo: store data...etc
        #region Fields
        [PluginReference] Plugin ZoneManager, Economics, ServerRewards, SignArtist;

        const string perm = "claimland.allow";
        const string perm2 = "claimland.landonly";
        const string perm3 = "claimland.admin";


        public List<LandData> landCache;
        public Dictionary<ulong, int> LandEdit = new Dictionary<ulong, int>();
        public Dictionary<ulong, int> PlayerCache = new Dictionary<ulong, int>();

        //config file
        public bool useEconomics = true;
        public bool useServerRewards = false;
        public float timeShow = 120f;

        public class LandData
        {
            public ulong OwnerID { get; set; }
            public int id { get; set; }
            public int zoneID { get; set; }
            public double price { get; set; }
            public string status { get; set; }
            public BaseEntity sign { get; set; }
            //public string type { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            permission.RegisterPermission(perm, this);
            permission.RegisterPermission(perm2, this);
            permission.RegisterPermission(perm3, this);

            ReadData();
        }

        void OnServerSave()
        {
            SaveData();
        }

        void Unload()
        {
            SaveData();
            landCache.Clear();
        }

        private void OnEntityBuilt(Planner planner, GameObject gObject)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            BaseEntity entity = gObject.ToBaseEntity();

            if (ZoneManager?.CallHook("GetPlayerZoneIDs", player) == null)
            {
                if (permission.UserHasPermission(player.UserIDString, perm2))
                {
                    Send(player, lang.GetMessage("OnlyLand", this));
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
            else
            {
                String[] lands = (string[])ZoneManager?.CallHook("GetPlayerZoneIDs", player);
                if(lands == null) { Send(player, GetMessage("ErrorLand")); return; }

                foreach (LandData data in landCache)
                {
                    if(data.zoneID == Convert.ToInt32(lands[0]))
                    {
                        if(player.userID != data.OwnerID)
                        {
                            Send(player, GetMessage("NoLand"));
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                    }
                }
            }

            Send(player, "Positon: " +  entity.transform.position);
        }
        #endregion

        #region ZoneManager Hooks
        void OnEnterZone(string ZoneID, BasePlayer player)
        {
            if (PlayerCache.ContainsKey(player.userID))
            {
                PlayerCache[player.userID] = Convert.ToInt32(ZoneID);
            }
            else
            {
                PlayerCache.Add(player.userID, Convert.ToInt32(ZoneID));
            }
        }

        void OnExitZone(string ZoneID, BasePlayer player)
        {
            if (PlayerCache.ContainsKey(player.userID))
            {
                PlayerCache[player.userID] = 0;
            }
            else
            {
                PlayerCache.Add(player.userID, 0);
            }
        }
        #endregion

        #region Chat Command
        [ChatCommand("land")]
        private void cmdLand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm)) { Send(player, lang.GetMessage("NoPermission", this)); return; }
            switch (args[0])
            {
                case "buy":
                    //check if args[1] is a number
                    if (args.Length != 2) { Send(player, GetMessage("SyntaxBuy")); return; }
                    BuyLand(player, Convert.ToInt32(args[1]));
                    break;
                case "list":
                    if (args.Length != 1) { Send(player, GetMessage("SyntaxList")); return; }
                    PlayerLands(player);
                    break;
            }
        }

        [ChatCommand("cl")]
        private void cmdClaimLand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm3)) { Send(player, lang.GetMessage("NoPermission", this));  return; }
            if(args.Length == 0) { return; }
            switch (args[0])
            {
                case "create":
                    CreateLand(player);
                    break;
                case "list":
                    ShowLandList(player);
                    break;
                case "edit":
                    if (args.Length != 2) { Send(player, GetMessage("SyntaxEdit")); return; }

                    if (LandEdit.ContainsKey(player.userID))
                    {
                        LandEdit[player.userID] = Convert.ToInt32(args[1]);
                    }
                    else
                    {
                        LandEdit.Add(player.userID, Convert.ToInt32(args[1]));
                    }
                    ShowLand(player, Convert.ToInt32(args[1]), timeShow);
                    Send(player, GetMessage("CurrentEditing"), args[1]);
                    break;
                case "detail":
                    if (args.Length != 2) { Send(player, GetMessage("SyntaxDetail")); return; }
                    DetailLand(player, Convert.ToInt32(args[1]));
                    break;
                case "show":
                    if (args.Length != 2) { Send(player, GetMessage("SyntaxShow")); return; }
                    ShowLand(player, Convert.ToInt32(args[1]), timeShow);
                    break;
                case "tp":
                    if (args.Length != 2) { Send(player, GetMessage("SyntaxTP")); return; }
                    TeleportLand(player, Convert.ToInt32(args[1])); 
                    break;
                default:
                    if (args.Length < 2) { Send(player, GetMessage("SyntaxEdit")); return; }
                    for (var i = 0; i < args.Length; i++)
                    {
                        string data = args[i].ToLower();
                        EditLand(player, data, args[++i]);
                    }
                    break;
            }
        }
        #endregion

        #region Methods
        public void EditLand(BasePlayer player, string data, string value)
        {
            if (!LandEdit.ContainsKey(player.userID)) { Send(player, GetMessage("MustEdit")); return; }

            int id = LandEdit[player.userID];
            //check if its a number.
            switch (data)
            {
                case "zone":
                    Send(player, value);
                    if (ZoneManager?.CallHook("CheckZoneID", value) == null) { Send(player, GetMessage("NoZone")); return; }
                    GetLand(id).zoneID = Convert.ToInt32(value); //check if zone exits
                    Send(player, GetMessage("ZoneSet"), GetLand(id).zoneID);
                    break;
                case "price":
                    GetLand(id).price = Convert.ToInt32(value);
                    Send(player, GetMessage("PriceSet"), GetLand(id).price);
                    break;
                case "border":
                    //check if zone exists...
                    if(value == "true")
                    {
                        Vector3 size = (Vector3)ZoneManager?.CallHook("GetZoneSize", GetLand(id).zoneID.ToString());
                        Vector3 position = (Vector3)ZoneManager?.CallHook("GetZoneLocation", GetLand(id).zoneID.ToString());

                        Vector3 borderPosition;
                        borderPosition.y = position.y;
                        borderPosition.z = position.z - (size.z / 3) - 4;

                        for (float x = -(size.x / 3); (x + 1) < size.x/3; x++)
                        {
                            borderPosition.x = position.x + x*3;
                            var entity = GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab", borderPosition, new Quaternion(), true);
                          
                            entity.Spawn();
                        };

                        borderPosition.z = position.z + (size.z / 3) + 4;

                        for (int x = 0; (x + 1) < size.x / 3; x++)
                        {
                            borderPosition.x = position.x + x * 3;
                            var entity = GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab", borderPosition, new Quaternion(), true);
                            entity.Spawn();
                        };

                        Send(player, "Border created");
                    }
                    break;
            }
        }

        public void DetailLand(BasePlayer player, int id)
        {
            if (GetLand(id) == null) { Send(player, GetMessage("WrongID")); return; }
            Send(player, GetMessage("DetailLand1"), id);
            Send(player, GetMessage("DetailLand2"), GetLand(id).OwnerID);
            Send(player, GetMessage("DetailLand3"), GetLand(id).price);
            Send(player, GetMessage("DetailLand4"), GetLand(id).zoneID);
        }

        public void BuyLand(BasePlayer player, int id)
        {
            LandData data = GetLand(id);
            if(data == null) { Send(player, GetMessage("WrongID"));  return;}
            if (data.OwnerID == player.userID) { Send(player, GetMessage("CantBuyYourLand")); return; }
            if (data.OwnerID != 0) { Send(player, GetMessage("HasOwner")); return;}

            if(TakeMoney(player, data.price))
            {
                GiveLand(player, id);
                Send(player, GetMessage("LandBought"), data.id);
            }
        }

        public bool TakeMoney(BasePlayer player, double amount)
        {
            if (useEconomics)
            {
                if(Economics == null) { Send(player, GetMessage("NoEconomics")); return false; }

                bool canBuy = (bool)Economics.CallHook("Withdraw", player.userID, amount);

                if (canBuy)
                {
                    return true;
                }
                else
                {
                    Send(player, GetMessage("NoMoney"));
                    return false;
                }
            }
            else if (useServerRewards)
            {
                if (Economics == null) { Send(player, GetMessage("NoServerRewards")); return false; }

                bool canBuy = (bool)ServerRewards?.Call("TakePoints", player.userID, Convert.ToInt32(amount));

                if (canBuy)
                {
                    return true;
                }
                else
                {
                    Send(player, GetMessage("NoMoney"));
                    return false;
                }
            }

            return false;
        }

        public void PlayerLands(BasePlayer player)
        {
            List<int> landList = new List<int>();
            foreach (LandData data in landCache)
            {
                if(data.OwnerID == player.userID)
                {
                    landList.Add(data.id);
                }
            }

            if(landList.Count == 0) { Send(player, GetMessage("NoLand")); return; }

            SendReply(player, "<color=#486391>==========Lands==========</color>");

            foreach(int land in landList)
            {
                SendReply(player, "<color=#932f2f>" + land + "</color>");   
            }
        }

        public void CreateLand(BasePlayer player)
        {
            LandData data = new LandData();
            data.id = UnityEngine.Random.Range(1, 9999);
            data.price = 50;
            data.OwnerID = 0;
            landCache.Add(data);
            Send(player, GetMessage("LandCreated"), data.id);
        }

        public void TeleportLand(BasePlayer player, int id)
        {
            if (GetLand(id) == null) { Send(player, GetMessage("WrongID")); return; }
            Vector3 landPosition = (Vector3)ZoneManager.CallHook("GetZoneLocation", GetLand(id).zoneID.ToString());

            if(landPosition == null) { Send(player, GetMessage("ErrorTP")); return; }

            player.transform.position = landPosition;
            Send(player, GetMessage("TP"), id);
        }

        public LandData CheckPlayerInLand(BasePlayer player)
        {
            foreach(LandData data in landCache)
            {

            }
            return null;
        }

        public void GiveLand(BasePlayer player, int id)
        {
            GetLand(id).OwnerID = player.userID;
        }
        
        public void ShowLand(BasePlayer player, int id, float time = 60)
        {

            ZoneManager?.CallHook("ShowZone", player, GetLand(id).zoneID.ToString(), time);
            Send(player, GetMessage("ShowLand"), id);
        }

        public void ShowLandList(BasePlayer player)
        {
            SendReply(player, "<color=#486391>==========LIST==========</color>");
            foreach (LandData data in landCache)
            {
                SendReply(player, "<color=#932f2f>" + data.id.ToString() + "</color>");
            }
        }

        public void Send(BasePlayer player, string message, params object[] args)
        {

            SendReply(player, "<color=#141414>[</color><color=#4c2f93>Claimland</color><color=#141414>]:</color> <color=#486391>" + message + "</color>", args);  
        }

        public void RemoveLand(int id, BasePlayer player)
        {
            landCache.Remove(GetLand(id));
            SaveData();
            Send(player, GetMessage("LandRemoved"));
        }

        public void RemoveAllLands()
        {
            landCache.Clear();
            SaveData();
        }

        public void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Claimland", landCache);
        }

        public void ReadData()
        {
            try
            {
                landCache = Interface.Oxide.DataFileSystem.ReadObject<List<LandData>>("Claimland");
            }
            catch
            {
                landCache = new List<LandData>();
            }
            
        }

        public LandData GetLand(int id)
        {
            foreach (LandData data in landCache)
            {
                if (data.id == id)
                {
                    return data;
                }
            }
            return null;
        }
        #endregion

        #region Localization
        public string GetMessage(string key)
        {
            return lang.GetMessage(key, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to access this command.",
                ["OnlyLand"] = "You'r only allowed to build on lands.",
                ["WrongID"] = "This id does not exists.",
                ["NoLand"] = "You dont own this land to build on it.",
                ["NoEconomics"] = "Economics plugin is not installed.",
                ["NoSignArtist"] = "SignArtist plugin is not installed.",
                ["NoServerRewards"] = "ServerRewards plugin is not installed.",
                ["HasOwner"] = "This land is already claimed.",
                ["NoMoney"] = "You do not have enough money.",
                ["SyntaxBuy"] = "Syntax Error, /land buy <id>",
                ["SyntaxList"] = "Syntax Error, /cl list",
                ["SyntaxPos"] = "Syntax Error, /pos <id> <1 or 2>",
                ["SyntaxEdit"] = "Syntax Error, /cl [option1] <value1> [option2] <value2>",
                ["SyntaxTP"] = "Syntax Error, /cl tp <id>",
                ["ErrorTP"] = "Error occurred while trying teleporting to land.",
                ["ErrorLand"] = "Error While trying to check if player is on land.",
                ["TP"] = "You have been teleported to land <color=#932f2f>{0}</color>",
                ["SyntaxPosEdit"] = "Syntax Error, /cl pos <id>",
                ["SyntaxNoPos"] = "You must do /cl pos <id> to set its position",
                ["SyntaxList"] = "Syntax Error, /cl edit <id>",
                ["SyntaxDetail"] = "Syntax Error, /cl detail <id>",
                ["SyntaxShow"] = "Syntax Error, /cl show <id>",
                ["NoNumberID"] = "ID must contain only numbers.",
                ["EditingPos"] = "Your editing position for <color=#932f2f> {0} </color> use /pos to set its position",
                ["NoNumberPos"] = "Argument must contain only numbers.",
                ["NoLand"] = "You dont own any lands.",
                ["CantBuyYourLand"] = "You cant buy your land.",
                ["LandCreated"] = "Land has been created with id <color=#932f2f> {0} </color>",
                ["LandBought"] = "You have claimed a land with id <color=#932f2f> {0} </color>",
                ["DetailLand1"] = "Details for land: <color=#932f2f> {0} </color>",
                ["DetailLand2"] = "OwnerID: <color=#932f2f> {0} </color>",
                ["DetailLand3"] = "Price: <color=#932f2f> {0} </color>",
                ["DetailLand4"] = "ZoneID: <color=#932f2f> {0} </color>",
                ["MustEdit"] = "You must do /cl edit <id>",
                ["NoZone"] = "This zone does not exists",
                ["PriceSet"] = "Land price has been set to <color=#932f2f> {0} </color>",
                ["ZoneSet"] = "Zone id has been set to <color=#932f2f> {0} </color>",
                ["CurrentEditing"] = "You'r now editing <color=#932f2f> {0} </color>",
                ["ShowLand"] = "Showing 3D visualization for land<color=#932f2f> {0} </color>"
            }, this);
        }
        #endregion
    }
}
