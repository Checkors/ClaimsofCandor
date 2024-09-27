using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using ProtoBuf;



namespace ClaimsofCandor
{

    [ProtoContract]
    public class StrongholdData
    {

    }
    public class Stronghold
    {

        //=======================
        // D E F I N I T I O N S
        //=======================

        public string Name;
        public Cuboidi Area;
        public Cuboidi CaptureArea;
        public BlockPos Center;
        public double? gameDayPlaced;

        public string PlayerUID;
        public string PlayerName;

        public int? GroupUID;
        public string GroupName;

        private bool isClientInside;

        public HashSet<Entity> BesiegingEntities = new();
        public float SiegeIntensity;
        public bool contested;

        internal long? UpdateRef;

        public ICoreAPI Api;

        public bool IsClaimed => PlayerUID != null;

        //===============================
        // RoC Helper Functions
        //===============================

        public string GetDisplayName()
        {
            return string.Format("{0}", Name != null ? Name : Center.ToLocalPosition(this.Api));
        }


        //===============================
        // I M P L E M E N T A T I O N S
        //===============================

        public void Claim(IPlayer byPlayer)
        {

            PlayerUID = byPlayer.PlayerUID;
            PlayerName = byPlayer.PlayerName;

            if (Api is ICoreServerAPI Sapi)
            {

                Sapi.SendMessage(
                    byPlayer,
                    GlobalConstants.InfoLogChatGroup,
                    Lang.Get("You can use /stronghold name to name your claim"),
                    EnumChatType.Notification
                ); // ..

                if (Name is string claimName)
                    Sapi.SendMessageToGroup(
                        GlobalConstants.GeneralChatGroup,
                        Lang.Get("{0} captured {1}", PlayerName, claimName),
                        EnumChatType.Notification
                    ); // ..
            } // if ..
        } // void ..


        public void Unclaim(EnumUnclaimCause unclaimCause = EnumUnclaimCause.Server)
        {
            if (unclaimCause != EnumUnclaimCause.Server && Api is ICoreServerAPI Sapi)
            {
                string message = Lang.Get((Name is not null ? "{0}" : "One of your claim") + unclaimCause switch
                {
                    EnumUnclaimCause.EmptyCellar => " has run out of food!",
                    EnumUnclaimCause.Player => " has been captured!",
                    EnumUnclaimCause.FlagBroken => " has been destroyed!",
                    EnumUnclaimCause.Abandon => " has been abandoned.",
                    _ => "",
                }, Name); // ..

                if (GroupUID is int groupUID) Sapi.SendMessageToGroup(groupUID, message, EnumChatType.Notification);
                else Sapi.SendMessage(Sapi.World.PlayerByUid(PlayerUID), GlobalConstants.InfoLogChatGroup, message, EnumChatType.Notification);
            } // if ..

            PlayerUID = null;
            PlayerName = null;
            GroupUID = null;
            GroupName = null;

        } // void ..

        public void ClaimGroup(int groupUID)
        {
            if (Api is ICoreServerAPI Sapi)
            {

                GroupName = Sapi.Groups.PlayerGroupsById[groupUID].Name;
                string claimName = GetDisplayName();
                Sapi.SendMessageToGroup(
                    groupUID,
                    Lang.Get("{0} now leagues with {1}", claimName, GroupName),
                    EnumChatType.Notification
                ); // ..              
                Sapi.World.BlockAccessor.GetBlockEntity(Center).MarkDirty();
            }
        } // void ..

        public void ClaimGroup(PlayerGroup group)
        {

            GroupUID = group.Uid;
            GroupName = group.Name;

            if (Api is ICoreServerAPI Sapi && Name is string claimName)
                Sapi.SendMessageToGroup(
                    group.Uid,
                    Lang.Get("{0} now leagues with {1}", claimName, group.Name),
                    EnumChatType.Notification
                ); // ..
        } // void ..


        public void UnclaimGroup()
        {
            if (Api is ICoreServerAPI Sapi && GroupUID is int groupUID && Name is string claimName)
                Sapi.SendMessageToGroup(
                    groupUID,
                    Lang.Get("{0} no longer leagues with {1}", claimName, GroupName),
                    EnumChatType.Notification
                ); // ..

            GroupUID = null;
            GroupName = null;

        } // void ..


        public void IncreaseSiegeIntensity(
            float intensity,
            Entity byEntity = null
        )
        {

            if (Api is ICoreServerAPI Sapi)
            {

                int newBesiegingCount = BesiegingEntities.Count + (byEntity is not null ? 1 : 0);
                float newIntensity = SiegeIntensity + intensity;

                if (newIntensity >= 1f && SiegeIntensity < 1f)
                {

                    string message = Lang.Get((Name is not null ? "{0}" : "One of your claim") + " is under attack!", Name);

                    if (GroupUID is int groupUID) Sapi.SendMessageToGroup(groupUID, message, EnumChatType.Notification);
                    else Sapi.SendMessage(Sapi.World.PlayerByUid(PlayerUID), GlobalConstants.InfoLogChatGroup, message, EnumChatType.Notification);

                }
                else if (newIntensity >= 2f && Name is string name)
                {

                    if (newBesiegingCount >= 2 && BesiegingEntities.Count < 2) Sapi.SendMessageToGroup(GlobalConstants.InfoLogChatGroup, Lang.Get("{0} is currently being besieged by a small band", name), EnumChatType.Notification);
                    else if (newBesiegingCount >= 4 && BesiegingEntities.Count < 4) Sapi.SendMessageToGroup(GlobalConstants.InfoLogChatGroup, Lang.Get("{0} is currently being besieged by a medium sized army", name), EnumChatType.Notification);

                } // if ..


                if (byEntity != null) BesiegingEntities.Add(byEntity);
                SiegeIntensity += intensity;

            } // if ..
        } // void ..
          //---------
          // M A I N
          //---------

        



        //---------
        // M A I N
        //---------

        internal void Update(float _)
        {
            if (Api is ICoreClientAPI Capi)
            {
                if (Name != null)
                    if (isClientInside)
                        isClientInside = Area.Contains(Capi.World.Player.Entity.Pos.AsBlockPos);

                    else if (Area.Contains(Capi.World.Player.Entity.Pos.AsBlockPos))
                    {

                        isClientInside = true;
                        Capi.TriggerIngameDiscovery(
                            this,
                            "stronghold-enter",
                            PlayerUID is not null ? Name : Lang.Get("Ruins of {0}",
                            Name)
                        ); // ..
                    } // if ..
            }
            else
            {
                SiegeIntensity = GameMath.Max(SiegeIntensity - 0.01f, 0f);
                if (SiegeIntensity < 1f)
                    BesiegingEntities.Clear();

            } // if ..
        } // void ..
    } // class ..
} // namespace ..
