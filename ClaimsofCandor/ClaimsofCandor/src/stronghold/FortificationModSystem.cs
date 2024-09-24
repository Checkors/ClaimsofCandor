using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using ProtoBuf;
using Vintagestory.API.Util;
using System.Text;
using System.Xml.Linq;
using System.Security.Claims;
using ClaimsofCandor;
using System.Text.Json.Serialization;


namespace ClaimsofCandor
{
    public class FortificationModSystem : ModSystem
    {

        //=======================
        // D E F I N I T I O N S
        //=======================

        /*//protected HashSet<Stronghold> strongholds = new();
        protected Dictionary<int, Stronghold> strongholds = new Dictionary<int, Stronghold>();
        protected int strongholdIndexID = 1;
        //protected HashSet<IWorldChunk> strongHoldChunks = new();*/
        // Replaced with claims manager;
        protected ClaimAreaManager claimsManager = new ClaimAreaManager();

        protected ICoreAPI api;
        protected ICoreServerAPI serverAPI;
        

        public delegate void NewStrongholdDelegate(Stronghold stronghold);
        public event NewStrongholdDelegate StrongholdAdded;


        //===============================
        // I N I T I A L I Z A T I O N S
        //===============================

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;
        } // void ..


        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);
            this.serverAPI = sapi;

            sapi.Event.CanPlaceOrBreakBlock += BlockChangeAttempt;
            //api.Event.DidPlaceBlock += this.PlaceBlockEvent; // Deprecated and removed, caused block duplications.
            //api.Event.DidBreakBlock += this.BreakBlockEvent; // Deprecated, use case requires stopping block breaking and placement.
            sapi.Event.PlayerDeath += PlayerDeathEvent;



            // Baseline Bulwark command registrations
            sapi.ChatCommands
                .Create("stronghold")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("name")
                    .WithDescription("Name the claimed area you are in")
                    .WithArgs(sapi.ChatCommands.Parsers.Word("name"))
                    .HandleWith(new OnCommandDelegate(Cmd_StrongholdName))
                .EndSubCommand()
                .BeginSubCommand("league")
                    .WithDescription("Affiliate the claimed area you are in with a group")
                    .WithArgs(sapi.ChatCommands.Parsers.Word("group name"))
                    .HandleWith(new OnCommandDelegate(Cmd_StrongholdLeague)) // ..
                .EndSubCommand()
                .BeginSubCommand("stopleague")
                    .WithDescription("Stops the affiliation with a group")
                    .WithArgs(sapi.ChatCommands.Parsers.Word("group name"))
                    .HandleWith(new OnCommandDelegate(Cmd_StrongholdUnleague))
                .EndSubCommand()
            // RoC Bulwark command registrations
                .BeginSubCommand("capturegroup")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.chat)
                    .BeginSubCommand("set")
                        .WithArgs(sapi.ChatCommands.Parsers.Word("groupname"))
                        .HandleWith(new OnCommandDelegate(Cmd_SetCaptureGroup))
                    .EndSubCommand()
                    .BeginSubCommand("show")
                        .IgnoreAdditionalArgs()
                        .HandleWith(new OnCommandDelegate(Cmd_GetCaptureGroup));

        }




        //===============================
        // Bulwark Original Command Handlers
        //===============================

        private TextCommandResult Cmd_StrongholdName(TextCommandCallingArgs args)
        {

            string callerUID = args.Caller.Player.PlayerUID;

            Stronghold area; 
            if (HasPrivilege(args.Caller.Player, args.Caller.Player.Entity.Pos.AsBlockPos, out area))
            {
                if (area == null) return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "You're not in a stronghold you claimed"));
                if (IsOwner(args.Caller.Player, area))
                {
                    area.Name = args[0].ToString();
                    api.World.BlockAccessor.GetBlockEntity(area.Center).MarkDirty();
                    return TextCommandResult.Success(Lang.GetL(args.LanguageCode, "ClaimsofCandor:stronghold-name-success", args[0].ToString()));
                }
                else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "ClaimsofCandor:stronghold-name-failure"));
            }
            else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "You're not in a stronghold you claimed"));
            

        }

        private TextCommandResult Cmd_StrongholdLeague(TextCommandCallingArgs args)
        {

            string callerUID = args.Caller.Player.PlayerUID;
            Stronghold leagueArea;
            if (HasPrivilege(args.Caller.Player, args.Caller.Player.Entity.Pos.AsBlockPos, out leagueArea))
            {
                if (leagueArea == null) return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "You're not in a stronghold you claimed"));

                if ((api as ICoreServerAPI).Groups.GetPlayerGroupByName(args[0].ToString()) is PlayerGroup playerGroup)
                {
                    leagueArea.ClaimGroup(playerGroup);
                    api.World.BlockAccessor.GetBlockEntity(leagueArea.Center).MarkDirty();

                }
                else TextCommandResult.Error(Lang.GetL(args.LanguageCode, "No such group found"));
            }
            else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "You're not in a stronghold you claimed"));
            return TextCommandResult.Success();

        }

        private TextCommandResult Cmd_StrongholdUnleague(TextCommandCallingArgs args)
        {

            string callerUID = args.Caller.Player.PlayerUID;
            Stronghold leagueArea;
            if (HasPrivilege(args.Caller.Player, args.Caller.Player.Entity.Pos.AsBlockPos, out leagueArea))
            {
                if (leagueArea == null) return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "You're not in a stronghold you claimed"));

                if ((api as ICoreServerAPI).Groups.GetPlayerGroupByName(args[0].ToString()) is PlayerGroup playerGroup)
                {
                    leagueArea.UnclaimGroup();
                    api.World.BlockAccessor.GetBlockEntity(leagueArea.Center).MarkDirty();

                }
                else TextCommandResult.Error(Lang.GetL(args.LanguageCode, "No such group found"));
            }
            else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "You're not in a stronghold you claimed"));
            return TextCommandResult.Success();

        }

        //===============================
        // RoC Command Handlers
        //===============================

        /// <summary>
        /// Show claim areas (WIP, Not functional)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private TextCommandResult Cmd_ShowClaimAreas(TextCommandCallingArgs args)
        {
            if (args.Caller == null)
            {
                return TextCommandResult.Error("No Player");
            }


            return TextCommandResult.Success();
            //return TextCommandResult.Error("Fucked");
        }

        /// <summary>
        /// Set Capture Group. Used to set attacker group for capturing control point.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private TextCommandResult Cmd_SetCaptureGroup(TextCommandCallingArgs args)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
            string groupName = (string)args[0];

            sapi.Logger.Debug("ARGS: {0}, Type:{1}, gname: {2}", args[0], args[0].GetType(), groupName);

            if (groupName != null)
            {
                PlayerGroup mainGroup = sapi.Groups.GetPlayerGroupByName(groupName);

                if (mainGroup == null) return TextCommandResult.Error("ClaimsofCandor:capturegroup-nosuchgroup"); // No such group retrieved

                if (args.Caller.Player.GetGroup(mainGroup.Uid) != null)
                {

                    if (!sapi.PlayerData.PlayerDataByUid[args.Caller.Player.PlayerUID].CustomPlayerData.TryAdd("claimsofcandor:CaptureGroup", mainGroup.Uid.ToString()))
                    {
                        sapi.PlayerData.PlayerDataByUid[args.Caller.Player.PlayerUID].CustomPlayerData["CaptureGroup"] = mainGroup.Uid.ToString();
                    }
                    return TextCommandResult.Success(Lang.GetL(args.LanguageCode, "ClaimsofCandor:capturegroup-setsuccess", mainGroup.Name)); // Success state
                }
                else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "ClaimsofCandor:capturegroup-notingroup", groupName)); // No in requested group

            }
            else return TextCommandResult.Error("Null group name, debug error, contact dev"); //Args nulled out, somehow


        }

        // Set Capture Group. Used to set attacker group when capturing control point.
        private TextCommandResult Cmd_GetCaptureGroup(TextCommandCallingArgs args)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
            PlayerGroupMembership capGroup = GetPlayerCaptureGroup(args.Caller.Player);
            if (capGroup != null)
            {
                return TextCommandResult.Success(Lang.GetL(args.LanguageCode, "ClaimsofCandor:capturegroup-current", capGroup.GroupName));
            }
            else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "ClaimsofCandor:capturegroup-nosetgroup")); //Not set or in group
        }

        //



        //===============================
        // Event Handlers
        //===============================
        private bool BlockChangeAttempt(IServerPlayer byPlayer, BlockSelection blockSel, out string claimant)
        {
            claimant = null;

            if (blockSel != null)
            {
                string blockname = api.World.BlockAccessor.GetBlock(blockSel.Position).GetPlacedBlockName(api.World, blockSel.Position);

                //api.Logger.Debug("[ClaimsofCandor_BCA] {0} attempted place/remove {1}, at position {2}", byPlayer.PlayerName.ToString(), blockname, blockSel.Position.ToString());

                if (!(HasPrivilege(byPlayer, blockSel, out _) || byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative))
                {
                    //api.Logger.Debug("No Access");
                    claimant = Lang.GetL(byPlayer.LanguageCode, "ClaimsofCandor:stronghold-noaccess");
                    return false;
                }
                else
                {
                    //api.Logger.Debug("Access");
                    claimant = null;
                    return true;
                }

            }
            else
            {
                claimant = "Null selection";
                return false;
            }
        }

        private void PlayerDeathEvent(
            IServerPlayer forPlayer,
            DamageSource damageSource
        )
        {
            List<Stronghold> checkStrongholds = null;
            

            if (TryGetStronghold(forPlayer.Entity.ServerPos.AsBlockPos, out checkStrongholds))
            {

                foreach (Stronghold stronghold in checkStrongholds)
                {
                    Entity byEntity = damageSource.CauseEntity ?? damageSource.SourceEntity;

                    if (byEntity is EntityPlayer playerCause
                        && stronghold.Area.Contains(byEntity.ServerPos.AsBlockPos)
                        && !(playerCause.Player.Groups?.Any(group => group.GroupUid == stronghold.GroupUID) ?? false
                            || playerCause.PlayerUID == stronghold.PlayerUID)
                    ) stronghold.IncreaseSiegeIntensity(1f, byEntity);

                    else if (byEntity.WatchedAttributes.GetString("guardedPlayerUid") is string playerUid
                        && api.World.PlayerByUid(playerUid) is IPlayer byPlayer
                        && stronghold.Area.Contains(byEntity.ServerPos.AsBlockPos)
                        && !(byPlayer.Groups?.Any(group => group.GroupUid == stronghold.GroupUID) ?? false
                            || byPlayer.PlayerUID == stronghold.PlayerUID)
                        ) stronghold.IncreaseSiegeIntensity(1f, damageSource.CauseEntity);
                }
            } // if ..
        } // void ..



        //===============================
        // CaptureGroup Functions
        //===============================
        /// <summary>
        /// Gets a player's current capture group
        /// </summary>
        /// <param name="player"> Player of whom whose capture group will be returned</param>
        /// <returns>Group membership data of the player, will be null if no group</returns>
        public PlayerGroupMembership GetPlayerCaptureGroup(IPlayer player)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
            string getGroupUID;
            if (sapi.PlayerData.PlayerDataByUid[player.PlayerUID].CustomPlayerData.TryGetValue("claimsofcandor:CaptureGroup", out getGroupUID))
            {
                int groupUID = int.Parse(getGroupUID);
                if (groupUID > 0)
                {
                    return player.GetGroup(groupUID);
                }
                else return null; //unset group.
            }
            else return null; //Never set group


        }



        //===============================
        // Stronghold Messages
        //===============================

        public void SendStrongholdMessage(string langcode,IPlayer recipient = null, PlayerGroup recipientGroup = null,  params object[] args)
        {
           if (api is not ICoreServerAPI Sapi) return;
           if ((recipient == null && recipientGroup == null) || langcode == null) return;

           if (recipient != null)
           {
             Sapi.SendMessage(recipient, GlobalConstants.InfoLogChatGroup, Lang.Get(langcode, args), EnumChatType.Notification);
           }

           if (recipientGroup != null)
           {
                Sapi.SendMessageToGroup(recipientGroup.Uid, Lang.Get(langcode, args), EnumChatType.Notification);
           }
            return;
        }

        public void SendStrongholdMessage(string langcode, string recipientUID = null, int? recipientGroupUID = null, params object[] args)
        {
            if (api is not ICoreServerAPI Sapi) return;
            if ((recipientUID == null && recipientGroupUID == null) || langcode == null) return;

            if (recipientUID != null)
            {
                Sapi.SendMessage( Sapi.World.PlayerByUid(recipientUID), GlobalConstants.InfoLogChatGroup, Lang.Get(langcode, args), EnumChatType.Notification);
            }

            if (recipientGroupUID != null)
            {
                Sapi.SendMessageToGroup(recipientGroupUID.Value, Lang.Get(langcode, args), EnumChatType.Notification);
            }
            return;
        }


        //===============================
        // Stronghold Handlers
        //===============================

        public bool TryRegisterStronghold(Stronghold stronghold)
        {
            if (stronghold == null) return false;
            if (claimsManager.RegisterStronghold(stronghold) && !stronghold.UpdateRef.HasValue)
            {
                stronghold.UpdateRef = stronghold.Api
                    .Event
                    .RegisterGameTickListener(stronghold.Update, 2000, 1000);
            }

            return true;
        } // void ..


        public void RemoveStronghold(Stronghold stronghold)
        {
            if (stronghold == null) return;
            else
            {
                if (stronghold.UpdateRef.HasValue) stronghold.Api.Event.UnregisterGameTickListener(stronghold.UpdateRef.Value);
                claimsManager.RemoveStronghold(stronghold);
            } // if ..
        } // void ..

        /// <summary>
        /// Attempts to get the stronghold at the provided block position. Outputs a list of strongholds that encompass that point.
        /// </summary>
        /// <param name="pos"> Block position to see if it's in a curent stronghold</param>
        /// <param name="value"> List of strongholds that encompass the given position, null if none</param>
        /// <returns> Returns true if the position is enompassed by a stronghold, false otherwise</returns>
        public bool TryGetStronghold(BlockPos pos, out List<Stronghold> value) // I don't even know what uses this function. Gonna use it in priv function.
        {
            value = claimsManager.GetStrongholdAtPosition(pos);
            if (value == null) return false;
            return value.Count > 0;
        }


        public bool IsMember(IPlayer player, Stronghold stronghold)
        {
            if (player != null && stronghold != null)
            {
                if (stronghold.IsClaimed && stronghold.GroupUID.HasValue)
                {
                    return (player.GetGroup(stronghold.GroupUID.Value) != null);
                }
            }

            return false;
        }


        public bool IsOwner(IPlayer player, Stronghold stronghold)
        {
            if(player != null && stronghold != null)
            {
                if (stronghold.IsClaimed)
                {
                    return player.PlayerUID == stronghold.PlayerUID;
                }
            }

            return false;
        }




        /// <summary>
        /// ClaimsofCandor: Checks if the player has privilege over the selected block.
        /// </summary>
        /// <param name="byPlayer"> Player data</param>
        /// <param name="blockSel"> Block selection</param>
        /// <param name="area"> Output, Stronghold that claims the block selection, null if none</param>
        /// <returns></returns>
        public bool HasPrivilege(
            IPlayer byPlayer,
            BlockSelection blockSel,
            out Stronghold area
        )
        {
            area = null;

            if (byPlayer != null && blockSel != null)
            {
                bool privilege = true;
                List<Stronghold> claims;
                if (TryGetStronghold(blockSel.Position, out claims))
                {

                    StringBuilder sb = new StringBuilder();
                    foreach (Stronghold claim in claims)
                    {
                        sb.Append(string.Format("stronghold: {0} ", claim.Name != null ? claim.Name : claim.Center.AsVec3i));
                    }
                    //api.Logger.Debug("CHECKING YOUR PRIVILEGE: {0}", sb.ToString());


                    if (claims == null) return true; // No claim found, access
                    area = claims.Aggregate((currentMin, claim) =>
                        currentMin == null || claim.gameDayPlaced < currentMin.gameDayPlaced ? claim : currentMin); //Aggregate should prioritize first claim in list if values are equal.

                    if (area.contested)
                    {

                        privilege = false;
                        //api.Logger.Debug("PRIV: CONTESTED: {0}", privilege);
                        goto Privilige;
                    } // Contested, no access
                    if (area.PlayerUID == null)
                    {
                        privilege = true;
                        //api.Logger.Debug("PRIV: NO OWNER: {0}", privilege);
                        goto Privilige;
                    } // Fortress not claimed, access
                    if (IsOwner(byPlayer, area))
                    {
                        privilege = true;
                        //api.Logger.Debug("PRIV: IS OWNER: {0}", privilege);
                        goto Privilige;
                    } // Player is current owner, access
                    if (area.GroupUID.HasValue)
                    {
                        privilege = byPlayer.GetGroup(area.GroupUID.Value) != null;
                        //api.Logger.Debug("PRIV: IN GROUP: {0}", privilege);
                        goto Privilige;
                    }// Returns true if player is in the group, false otherwise, skips if there is no group.

                    privilege = false; // End case, owned stronghold with no group, byplayer does not own the stronghold.
                    //api.Logger.Debug("PRIV: NO ACCESS: {0}", privilege);


                }
                // No stronghold, so privilege is true

                Privilige:
                return privilege;
            }
            else
            {
                //api.Logger.Debug("Bulwark:ForificationModsystem.HasPrivilege Null IPlayer or BlockSel");
                return false; //Null player, null blocksel, return should be irrelevant.
            }
        }

        /// <summary>
        /// ClaimsofCandor: Checks if the player has privilege over the selected block.
        /// </summary>
        /// <param name="byPlayer"> Player data</param>
        /// <param name="pos"> Block position to check</param>
        /// <param name="area"> Output, Stronghold claiming block, null if no claim</param>
        /// <returns></returns>
        public bool HasPrivilege(
           IPlayer byPlayer,
           BlockPos pos,
           out Stronghold area
       )
        {
            area = null;

            if (byPlayer != null && pos != null)
            {
                bool privilege = true;
                List<Stronghold> claims;
                if (TryGetStronghold(pos, out claims))
                {

                    StringBuilder sb = new StringBuilder();
                    foreach (Stronghold claim in claims)
                    {
                        sb.Append(string.Format("stronghold: {0} ", claim.Name != null ? claim.Name : claim.Center.AsVec3i));
                    }
                    //api.Logger.Debug("CHECKING YOUR PRIVILEGE: {0}", sb.ToString());


                    if (claims == null) return true; // No claim found, access
                    area = claims.Aggregate((currentMin, claim) =>
                        currentMin == null || claim.gameDayPlaced < currentMin.gameDayPlaced ? claim : currentMin); //Aggregate should prioritize first claim in list if values are equal.

                    if (area.contested)
                    {

                        privilege = false;
                        //api.Logger.Debug("PRIV: CONTESTED: {0}", privilege);
                        goto Privilige;
                    } // Contested, no access
                    if (area.PlayerUID == null)
                    {
                        privilege = true;
                        //api.Logger.Debug("PRIV: NO OWNER: {0}", privilege);
                        goto Privilige;
                    } // Fortress not claimed, access
                    if (IsOwner(byPlayer, area))
                    {
                        privilege = true;
                        //api.Logger.Debug("PRIV: IS OWNER: {0}", privilege);
                        goto Privilige;
                    } // Player is current owner, access
                    if (area.GroupUID.HasValue)
                    {
                        privilege = byPlayer.GetGroup(area.GroupUID.Value) != null;
                        //api.Logger.Debug("PRIV: IN GROUP: {0}", privilege);
                        goto Privilige;
                    }// Returns true if player is in the group, false otherwise, skips if there is no group.

                    privilege = false; // End case, owned stronghold with no group, byplayer does not own the stronghold.
                    //api.Logger.Debug("PRIV: NO ACCESS: {0}", privilege);
                }
            // No stronghold, so privilege is true

            Privilige:
                
                return privilege;
            }
            else
            {
                api.Logger.Debug("Bulwark:ForificationModsystem.HasPrivilege Null IPlayer or BlockSel");
                return false; //Null player, null blocksel, return should be irrelevant.
            }
        }

    } // class ..

} // namespace ..
