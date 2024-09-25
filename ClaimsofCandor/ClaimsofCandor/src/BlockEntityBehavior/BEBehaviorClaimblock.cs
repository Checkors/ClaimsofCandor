using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;


namespace ClaimsofCandor {
    public class BlockEntityBehaviorClaimblock : BlockEntityBehavior {

        //=======================
        // D E F I N I T I O N S
        //=======================

            public Stronghold Stronghold { get; protected set; } = new Stronghold();
           
            public float CapturedPercent { get; protected set; }
            
            private int captureRadius; // Used to define capture area.
            private float trueCaptureRadius; // Used to determine nearby players by radius.

            protected float TargetPercent => this.captureDirection switch {
                EnumCaptureDirection.Claim   => 1f,
                EnumCaptureDirection.Unclaim => 0f,
                _ => this.Stronghold.IsClaimed
                    ? this.Api.World.BlockAccessor.GetLightLevel(this.Pos, EnumLightLevelType.OnlySunLight) >= 16
                        ? this.NowClaimedUntilDay - this.Api.World.Calendar.TotalDays > 0.1
                            ? 1f
                            : 0f
                        : 0f
                    : 0f,
            }; // ..
            
            private EnumCaptureDirection previousCaptureDirection;
            private float lastCapturePercent;

            protected float   cellarExpectancy;
            protected double? NowClaimedUntilDay => this.Api.World.Calendar.TotalDays
                + this.cellarExpectancy
                - (double)MathF.Pow(this.Stronghold.SiegeIntensity * 0.25f, 2);


            protected IPlayer               capturedBy;
            protected int?                   capturedByGroup; // Used for automatic Group Capture

            protected EnumCaptureDirection captureDirection;
            protected float                captureDuration;

            // Capture message



            public BlockBehaviorClaimblock BlockBehavior { get; protected set; }

            // Tick References
            private long? updateRef;
            private long? captureRef;
            private long? computeRef;
            private long? cellarRef;
            
            

        //===============================
        // I N I T I A L I Z A T I O N S
        //===============================

    public BlockEntityBehaviorClaimblock(BlockEntity blockEntity) : base(blockEntity) {}

            public override void Initialize(ICoreAPI api, JsonObject properties) {

                base.Initialize(api, properties);
                this.BlockBehavior = this.Block.GetBehavior<BlockBehaviorClaimblock>();


                // Stronghold properties
                this.Stronghold.Api = api;
                int protectionRadius   = properties["protectionRadius"].AsInt(16);
                captureRadius = properties["captureRadius"].AsInt(4);
                this.captureDuration   = properties["captureDuration"].AsFloat(4f);
                this.Stronghold.Center = this.Pos;
                int worldheight = api.World.BlockAccessor.MapSizeY;
                Vec3i positiveCorner = this.Pos.AsVec3i + new Vec3i(protectionRadius, worldheight, protectionRadius);
                Vec3i negativeCorner = this.Pos.AsVec3i - new Vec3i(protectionRadius, this.Pos.Y, protectionRadius);
                
                this.Stronghold.Area   = new Cuboidi( // Defines Stronghold area, protection radius defined in block json.
                negativeCorner, //Defines negative corner coordinate from center
                positiveCorner // Defines positive corner coordinate from center
                );

                this.Stronghold.CaptureArea = new Cuboidi( // Declare capture area. "half cube" area. Area that must be reached as a form of control point.
                this.Pos.AsVec3i - new Vec3i(captureRadius, 1, captureRadius),
                this.Pos.AsVec3i + new Vec3i(captureRadius, captureRadius, captureRadius)
                );

                trueCaptureRadius = (float)((captureRadius*Math.Sqrt(2))/2);

                this.updateRef  = api.Event.RegisterGameTickListener(this.Update, 50);
                this.computeRef = api.Event.RegisterGameTickListener(this.ComputeCellar, 1000);
                this.cellarRef  = api.Event.RegisterGameTickListener(this.UpdateCellar, 6000);


                this.Api.ModLoader.GetModSystem<FortificationModSystem>().TryRegisterStronghold(this.Stronghold);
               
                


            } // void ..

        

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (this.Stronghold.gameDayPlaced == null) this.Stronghold.gameDayPlaced = Api.World.Calendar.ElapsedHours;

            
        }


        public override void OnBlockBroken(IPlayer byPlayer = null) {
                base.OnBlockBroken(byPlayer);
                //this.renderer?.Dispose();
                if (this.updateRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.updateRef.Value);
                if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
                if (this.captureRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.captureRef.Value);
                if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
                this.Stronghold.Unclaim(EnumUnclaimCause.FlagBroken);
                this.Api.ModLoader.GetModSystem<FortificationModSystem>().RemoveStronghold(this.Stronghold);
        } // void ..


        public override void OnBlockRemoved() {
            base.OnBlockRemoved();
            //this.renderer?.Dispose();
            if (this.updateRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.updateRef.Value);
            if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
            if (this.captureRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.captureRef.Value);
            if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
            this.Api.ModLoader.GetModSystem<FortificationModSystem>().RemoveStronghold(this.Stronghold);
        } // void ..


        public override void OnBlockUnloaded() {
            base.OnBlockUnloaded();
            //this.renderer?.Dispose();
            if (this.updateRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.updateRef.Value);
            if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
            if (this.captureRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.captureRef.Value);
            if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
            this.Api.ModLoader.GetModSystem<FortificationModSystem>().RemoveStronghold(this.Stronghold);
        } // void ..

        //===============================
        // Initialization Helper Functions
        //===============================

        



        //===============================
        // I M P L E M E N T A T I O N S
        //===============================

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
                base.GetBlockInfo(forPlayer, dsc);

                if (this.Stronghold.Name is string name)
                    dsc.AppendLine("<font color=\"#ccc\"><i>" + Lang.Get("Banner of {0}", name) + "</i></font>");

                if (this.NowClaimedUntilDay is double claimedUntilDay) {
                    double remaining = claimedUntilDay - this.Api.World.Calendar.TotalDays;
                    
                    if (double.IsPositive(remaining)) {
                        if (this.Stronghold.PlayerName is not null)
                            if (this.Stronghold.GroupName is not null) {
                                dsc.AppendLine(Lang.Get(
                                    "Under {0}'s command in the name of {1} for {2:0.#} days",
                                    this.Stronghold.PlayerName,
                                    this.Stronghold.GroupName,
                                    remaining
                                )); // ..
                            } else dsc.AppendLine(Lang.Get(
                                    "Under {0}'s command for {1:0.#} days",
                                    this.Stronghold.PlayerName,
                                    remaining
                                )); // ..
                    } // if ..
                } // if ..
            } // void ..


        private void ComputeCellar(float _) {
            if (this.Stronghold.IsClaimed) {
                //Api.Logger.Debug("COMPUTE CELLAR TICK");

                List<BlockEntityContainer> cellars = new(4);
                if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 1, 0,  0, this.Pos.dimension)) is BlockEntityContainer cellarA) cellars.Add(cellarA);
                if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos(-1, 0,  0, this.Pos.dimension)) is BlockEntityContainer cellarB) cellars.Add(cellarB);
                if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0,  1, this.Pos.dimension)) is BlockEntityContainer cellarC) cellars.Add(cellarC);
                if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0, -1, this.Pos.dimension)) is BlockEntityContainer cellarD) cellars.Add(cellarD);

                this.cellarExpectancy = 0f;

                foreach(BlockEntityContainer cellar in cellars)
                    if (cellar != null)
                        foreach (ItemSlot itemSlot in cellar.Inventory)
                            if (itemSlot.Itemstack is ItemStack itemStack)
                                if (itemStack.Collectible?.NutritionProps is FoodNutritionProperties foodNutrition)
                                    this.cellarExpectancy += foodNutrition.Satiety
                                        * itemStack.StackSize
                                        * (ClaimsofCandorModSystem.ClaimDurationPerSatiety * (1f + this.BlockBehavior.ExpectancyBonus));

            } // if ..
        } // void ..


        private void UpdateCellar(float deltaTime) {

            if (this.Stronghold.IsClaimed) {
                //Api.Logger.Debug("UPDATE CELLAR TICK");

                float nowDurationPerSatiety = ClaimsofCandorModSystem.ClaimDurationPerSatiety * (1f + this.BlockBehavior.ExpectancyBonus);

                float satiety       = 0f;
                float targetSatiety = deltaTime / 86400f / this.Api.World.Calendar.SpeedOfTime / nowDurationPerSatiety;

                BlockEntityContainer[] cellars = new BlockEntityContainer [4];
                cellars[0] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 1, 0,  0, this.Pos.dimension));
                cellars[1] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos(-1, 0,  0, this.Pos.dimension));
                cellars[2] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0,  1, this.Pos.dimension));
                cellars[3] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0, -1, this.Pos.dimension));

                foreach(BlockEntityContainer cellar in cellars)
                    if (cellar != null)
                        foreach (ItemSlot itemSlot in cellar.Inventory)
                            if (satiety >= targetSatiety) return;
                            else if (itemSlot.Itemstack is ItemStack itemStack)
                                if (itemStack.Collectible?.NutritionProps is FoodNutritionProperties foodNutrition) {

                                    int targetSize = GameMath.Min(
                                        itemStack.StackSize,
                                        GameMath.RoundRandom(this.Api.World.Rand, targetSatiety / (foodNutrition.Satiety * nowDurationPerSatiety)
                                        - satiety / (foodNutrition.Satiety * nowDurationPerSatiety))
                                    ); // ..

                                    satiety += foodNutrition.Satiety * targetSize * nowDurationPerSatiety;
                                    itemSlot.TakeOut(targetSize);
                                    itemSlot.MarkDirty();

                                } // if ..
            } // if ..
        } // void ..


        private void Update(float deltaTime) {
        if (Api.Side == EnumAppSide.Server)
        {
                //Api.Logger.Debug("Update Tick, Captureby: {0}, group: {1}, current owner: {2}, PercCap: {3} ", capturedBy != null ? capturedBy.PlayerName : "none", this.capturedByGroup, this.Stronghold.PlayerName ?? "none", CapturedPercent);
                //Api.Logger.Debug(" \"void Update\" function call");

                this.CapturedPercent += GameMath.Clamp(this.TargetPercent - this.CapturedPercent, -deltaTime / this.captureDuration, deltaTime / this.captureDuration);
            if (this.CapturedPercent == 0f && this.Stronghold.IsClaimed)
            {
                //Api.Logger.Debug(" \"void Update\" function condition pass, unclaim");

                EnumUnclaimCause unclaimCause = this.cellarExpectancy == 0f
                        ? EnumUnclaimCause.EmptyCellar
                        : EnumUnclaimCause.Player;

                /*if (this.captureDirection == EnumCaptureDirection.Unclaim) {
                    //this.Api.World.SpawnItemEntity(this.Banner, this.Pos.ToVec3d());
                    //this.Banner = null;
                    //this.renderer?.Dispose();
                    //this.renderer = null;
                } // if .. */

                this.Blockentity.MarkDirty();
                this.Stronghold.Unclaim(unclaimCause);

            } // if ..
        }
        } // void ..

        //===============================
        // Block Interactions
        //===============================
        public void OnInteractFilter(IPlayer byPlayer, float secondsUsed)
        {
            if (byPlayer == null) return;
            Api.Logger.Debug("INPUT FILTER");
            if (Api.Side == EnumAppSide.Server)
            {
                if (byPlayer.Entity.ServerControls.Sneak && (secondsUsed>3f)){

                    if (this.Api.ModLoader.GetModSystem<FortificationModSystem>().IsOwner(byPlayer, this.Stronghold))
                    {
                        this.Stronghold.Unclaim(EnumUnclaimCause.Abandon);
                        this.Blockentity.MarkDirty();
                    }
                    else if (this.Api.ModLoader.GetModSystem<FortificationModSystem>().IsMember(byPlayer, this.Stronghold))
                    {
                        TryStartCapture(byPlayer, true); 
                    }
                    return;
                }
                else
                {
                    TryStartCapture(byPlayer);
                }
                return;
            }

        }
        
        public void TryStartCapture(IPlayer byPlayer, bool usurp=false) {

            if (byPlayer == null) return; //If there's no player, do nothing

            if (!(Api.World.BlockAccessor.GetTerrainMapheightAt(this.Pos) - this.Pos.Y <= ClaimsofCandorModSystem.UndergroundClaimLimit)) // If the flag is underground, do nothing.
            {
                IServerPlayer serverPlayer = byPlayer as IServerPlayer;
                if (serverPlayer != null) serverPlayer.SendIngameError("stronghold-undergroundflag");
                return;
            }
            if (!this.Api.ModLoader.GetModSystem<FortificationModSystem>().TryGetStronghold(this.Pos, out var _))
            {
                return;
            }

            if (this.Stronghold.PlayerUID == byPlayer.PlayerUID) return; //If the player who owns it is trying to capture, do nothing.

            if (this.Stronghold.GroupUID.HasValue) //If the stronghold has a group 0->
            {
                if (byPlayer.GetGroup(this.Stronghold.GroupUID.Value) != null && !usurp)
                {
                    return;
                }// 0-> And if the player has the same group as the stronghold group, do nothing.
                // *IMPLEMENT* Stronghold transfering via command.
            };


            Api.Logger.Debug(" TryStartCapture Attempt {0} at {1}, current owner: {2}, ref {3}", byPlayer.PlayerName, byPlayer.Entity.Pos.XYZInt, this.Stronghold.PlayerName, captureRef != null); //Debug print
            if (this.captureRef == null && this.capturedBy == null) {

                // If not currently being captured, start capturing
                // Api.Logger.Debug("FIRST CONDITION: {0} at {1}", byPlayer.PlayerName, byPlayer.Entity.Pos.XYZInt); //Debug print
                if (Api.Side == EnumAppSide.Server)
                {
                    this.Stronghold.contested = true;
                    this.capturedBy = byPlayer;

                    PlayerGroupMembership capGroup = this.Api.ModLoader.GetModSystem<FortificationModSystem>().GetPlayerCaptureGroup(byPlayer);
                    if (capGroup != null)
                    {
                        this.capturedByGroup = capGroup.GroupUid;
                        Api.Logger.Debug("Capturegroup", this.captureRef != null ? this.captureRef : "null");
                    }
                    this.captureRef = Api.Event.RegisterGameTickListener(this.CaptureUpdate, 200);
                    Api.Logger.Debug("CaptureRef: {0}", this.captureRef != null ? this.captureRef : "null");
                    this.Blockentity.MarkDirty();

                    this.Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage("ClaimsofCandor:stronghold-groupstartcapture", byPlayer.PlayerUID, capGroup.GroupUid, this.Stronghold.Name != null ? this.Stronghold.Name : this.Stronghold.Center.ToLocalPosition(this.Api));
                    if (this.Stronghold.IsClaimed)
                    {
                        this.Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage("ClaimsofCandor:stronghold-grouplosingclaim", this.Stronghold.PlayerUID, this.Stronghold.GroupUID, this.Stronghold.Name != null ? this.Stronghold.Name : this.Stronghold.Center.ToLocalPosition(this.Api));
                    }
                }

                Api.Logger.Debug(" TryStartCapture Success {0} at {1}, current owner: {2}, ref {3}", byPlayer.PlayerName, byPlayer.Entity.Pos.XYZInt, this.Stronghold.PlayerName, captureRef.HasValue ? captureRef.Value : false); //Debug print
            }  else
            {
                Api.Logger.Debug(" TryStartCapture Fail: {0} at {1}, current owner: {2}, ref {3}", byPlayer.PlayerName, byPlayer.Entity.Pos.XYZInt, this.Stronghold.PlayerName, captureRef != null);
            }// if ..
    } // void ..


        private void CaptureUpdate(float deltaTime) {
            Api.Logger.Debug("CaptureUpdate: ", this.Stronghold.Name != null ? this.Stronghold.Name : this.Stronghold.Center);
            if (Api.Side != EnumAppSide.Server) return; // Serverside updates only.
            if (this.capturedBy == null) return;

            // Reset lastThresholdPassed if capture percentage drops

            this.previousCaptureDirection = this.captureDirection;
            this.captureDirection = CheckCaptureArea(out _, out _);

            if (this.captureDirection != this.previousCaptureDirection && CapturedPercent != 1f)
            {
                CaptureUpdateMessage(EnumCaptureProgress.CaptureReverse);
            }

            if (this.CapturedPercent == 1f)
            {
                if (!this.Stronghold.IsClaimed)
                {
                    this.previousCaptureDirection = EnumCaptureDirection.Still;
                    this.cellarExpectancy = GameMath.Max(this.cellarExpectancy, 0.2f);
                    this.Stronghold.Claim(capturedBy);
                    if (this.capturedByGroup.HasValue) this.Stronghold.ClaimGroup(capturedByGroup.Value);
                    this.Stronghold.contested = false;
                    this.EndCapture();
                    this.Blockentity.MarkDirty();
                }
                    
            }
                

            
        }

        private EnumCaptureDirection CheckCaptureArea(out List<IPlayer> attackers, out List<IPlayer> defenders)
        {
            IPlayer[] playersAround = Api.World.GetPlayersAround(this.Pos.ToVec3d(), trueCaptureRadius, trueCaptureRadius);
            attackers = new List<IPlayer>();
            defenders = new List<IPlayer>();

            for (int i = 0; i < playersAround.Length; i++)
            {
                if (this.Stronghold.CaptureArea.Contains(playersAround[i].Entity.Pos.XYZ))
                {

                    if (this.Stronghold.IsClaimed)
                    {
                        if (playersAround[i].PlayerUID == this.Stronghold.PlayerUID || playersAround[i].GetGroup(this.Stronghold.GroupUID ?? -1) != null)
                        {
                            //Api.Logger.Debug("C, DEF: {0}", playersAround[i].PlayerName);
                            defenders.Add(playersAround[i]);
                        }
                        else if (playersAround[i] == this.capturedBy || (playersAround[i].GetGroup(this.capturedByGroup.HasValue ? this.capturedByGroup.Value : -1) != null))
                        {
                            //Api.Logger.Debug("C, ATK: {0}", playersAround[i].PlayerName);
                            attackers.Add(playersAround[i]);
                        }
                    } // Claimed Capture
                    else
                    {
                        if (playersAround[i] == this.capturedBy || (playersAround[i].GetGroup(this.capturedByGroup.HasValue ? this.capturedByGroup.Value : -1) != null))
                        {
                            //Api.Logger.Debug("U DEF: {0}", playersAround[i].PlayerName);
                            defenders.Add(playersAround[i]);
                        }
                        else
                        {
                            //Api.Logger.Debug("U ATK: {0}", playersAround[i].PlayerName);
                            attackers.Add(playersAround[i]);
                        }
                    } // Unclaimed Capture

                } // If player in capture zone
            } // For loop over players in radius

            // Capture direction logic. If attackers are greater than defenders, unclaim point,
            //Api.Logger.Debug("Def: {0}, Atk: {1}, Dir: {2}, Perc: {3}, capGrp-uid: {4}", defenders.Count, attackers.Count, this.captureDirection, this.CapturedPercent, this.capturedByGroup);
            return attackers.Count > defenders.Count ? EnumCaptureDirection.Unclaim : (defenders.Count == attackers.Count ? EnumCaptureDirection.Still : EnumCaptureDirection.Claim);

        }

        private void CaptureUpdateMessage(EnumCaptureProgress capEvent)
        {
            string defenderMessage = null;
            string capperMessage = null;
            string strongholdName = string.Format("{0}", Stronghold.Name != null ? Stronghold.Name : Stronghold.Center.ToLocalPosition(this.Api));
            string defender = null;
            string capper = capturedBy.PlayerName;
            if (capturedByGroup.HasValue) capper = string.Format("{0}", capturedBy.GetGroup(capturedByGroup.Value) != null ? capturedBy.GetGroup(capturedByGroup.Value).GroupName : capturedBy.PlayerName);
            switch (capEvent)
            {
                case EnumCaptureProgress.CaptureReverse:
                    {
                        if (Stronghold.IsClaimed)
                        {
                            defender = string.Format("{0}", Stronghold.GroupName ?? Stronghold.PlayerName);
                            defenderMessage = "ClaimsofCandor:stronghold-takeover-switch";
                            capperMessage = "ClaimsofCandor:stronghold-takeover-switch";


                            if (captureDirection == EnumCaptureDirection.Still)
                            {
                                defenderMessage = "ClaimsofCandor:stronghold-takeover-switch-stalemate";
                                capperMessage = "ClaimsofCandor:stronghold-takeover-switch-stalemate";

                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(capperMessage, capturedBy.PlayerUID, capturedByGroup, strongholdName);
                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(defenderMessage, Stronghold.PlayerUID, Stronghold.GroupUID, strongholdName);
                                return;
                            }
                            else if (captureDirection == EnumCaptureDirection.Claim)
                            {
                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(capperMessage, capturedBy.PlayerUID, capturedByGroup, defender, strongholdName);
                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(defenderMessage, Stronghold.PlayerUID, Stronghold.GroupUID, defender, strongholdName);
                                return;
                            }
                            else if (captureDirection == EnumCaptureDirection.Unclaim)
                            {
                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(capperMessage, capturedBy.PlayerUID, capturedByGroup, capper, strongholdName);
                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(defenderMessage, Stronghold.PlayerUID, Stronghold.GroupUID, capper, strongholdName);
                                return;
                            }
                        }
                        else
                        {
                            if (captureDirection == EnumCaptureDirection.Still)
                            {
                                capperMessage = "ClaimsofCandor:stronghold-capture-switch-stalemate";

                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(capperMessage, capturedBy.PlayerUID, capturedByGroup, strongholdName);
                                return;
                            }
                            else if (captureDirection == EnumCaptureDirection.Claim)
                            {
                                capperMessage = "ClaimsofCandor:stronghold-capture-switch-taking";
                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(capperMessage, capturedBy.PlayerUID, capturedByGroup, strongholdName);
                                return;
                            }
                            else if (captureDirection == EnumCaptureDirection.Unclaim)
                            {
                                capperMessage = "ClaimsofCandor:stronghold-capture-switch-losing";
                                Api.ModLoader.GetModSystem<FortificationModSystem>().SendStrongholdMessage(capperMessage, capturedBy.PlayerUID, capturedByGroup, strongholdName);
                                return;
                            }



                        }



                        break;
                    }
                case EnumCaptureProgress.CaptureQ2:{
                        break;
                    }
            }
        }

        public void EndCapture() {
            if (Api.Side == EnumAppSide.Server)
            {

                Api.Logger.Debug("EndCapture: captureRef {0}", this.captureRef.HasValue ? this.captureRef : "Null");
                if (this.captureRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.captureRef.Value);
                this.captureDirection = EnumCaptureDirection.Still;
                this.captureRef = null;
                this.capturedBy = null;
                this.capturedByGroup = -1;
                this.Blockentity.MarkDirty();
            }
        } // void ..


            //-------------------------------
            // T R E E   A T T R I B U T E S
            //-------------------------------

        public override void FromTreeAttributes(
            ITreeAttribute tree,
            IWorldAccessor worldForResolving
        ) {

            this.cellarExpectancy = tree.GetFloat("cellarExpectancy");
            this.CapturedPercent  = tree.GetFloat("capturedPercent");
            this.captureDirection = (EnumCaptureDirection)tree.GetInt("captureDirection", (int)EnumCaptureDirection.Still);
            this.Stronghold.gameDayPlaced = tree.GetDouble("gameDatePlaced");

            if (tree.GetString("claimedPlayerUID") is string playerUID
                && tree.GetString("claimedPlayerName") is string playerName
            ) {
                this.Stronghold.PlayerUID  = playerUID;
                this.Stronghold.PlayerName = playerName;
            } else {
                this.Stronghold.PlayerUID  = null;
                this.Stronghold.PlayerName = null;
            } // if ..

            if (tree.GetInt("claimedGroupUID") is int groupUID && groupUID != 0
                && tree.GetString("claimedGroupName") is string groupName
            ) {
                this.Stronghold.GroupUID  = groupUID;
                this.Stronghold.GroupName = groupName;
            } else {
                this.Stronghold.GroupUID  = null;
                this.Stronghold.GroupName = null;
            } // if ..

            if (tree.GetString("areaName") is string name) this.Stronghold.Name = name;

            this.Stronghold.SiegeIntensity = tree.GetFloat("siegeIntensity");

            base.FromTreeAttributes(tree, worldForResolving);

        } // void ..


        public override void ToTreeAttributes(ITreeAttribute tree) {

            if (this.Stronghold.gameDayPlaced.HasValue)          tree.SetDouble("gameDatePlaced", this.Stronghold.gameDayPlaced.Value);
            if (this.Block != null)                              tree.SetString("forBlockCode", this.Block.Code.ToShortString());
            if (this.Stronghold.Name is string name)             tree.SetString("areaName", name);
            if (this.Stronghold.PlayerUID is string playerUID)   tree.SetString("claimedPlayerUID", playerUID);   else tree.RemoveAttribute("claimedPlayerUID");
            if (this.Stronghold.PlayerName is string playerName) tree.SetString("claimedPlayerName", playerName); else tree.RemoveAttribute("claimedPlayerName");
            if (this.Stronghold.GroupUID is int groupUID)        tree.SetInt("claimedGroupUID", groupUID);        else tree.RemoveAttribute("claimedGroupUID");
            if (this.Stronghold.GroupName is string groupName)   tree.SetString("claimedGroupName", groupName);   else tree.RemoveAttribute("claimedGroupName");

            tree.SetFloat("siegeIntensity",   this.Stronghold.SiegeIntensity);
            tree.SetFloat("cellarExpectancy", this.cellarExpectancy);
            tree.SetFloat("capturedPercent",  this.CapturedPercent);
            tree.SetInt("captureDirection", (int)this.captureDirection);
            

            base.ToTreeAttributes(tree);

        } // void ..
    } // class ..
} // namespace ..
