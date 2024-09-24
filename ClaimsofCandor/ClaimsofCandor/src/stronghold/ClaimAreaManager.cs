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

namespace ClaimsofCandor
{
    public class ClaimAreaManager
    {

        private const int ClaimAreaSize = 256;
        private Dictionary<Vec2i, HashSet<Stronghold>> claimAreas = new Dictionary<Vec2i, HashSet<Stronghold>>();


        /// <summary>
        /// Registers the stronghold to the claims manager for efficient calling
        /// </summary>
        /// <param name="stronghold">stronghold to add</param>
        public bool RegisterStronghold(Stronghold stronghold)
        {
            bool retVal = false;
            Vec2i minArea = GetClaimArea(new Vec2i(stronghold.Area.MinX, stronghold.Area.MinZ));
            Vec2i maxArea = GetClaimArea(new Vec2i(stronghold.Area.MaxX, stronghold.Area.MaxZ));

            for (int x = minArea.X; x <= maxArea.X; x++)
            {
                for (int z = minArea.Y; z <= maxArea.Y; z++)
                {
                    Vec2i areaKey = new Vec2i(x, z);
                    if (!claimAreas.ContainsKey(areaKey))
                    {
                        claimAreas[areaKey] = new HashSet<Stronghold>();
                    }
                    if (claimAreas[areaKey].Add(stronghold))
                    {
                        retVal = true;
                    }
                }
            }
            return retVal;
        }

        /// <summary>
        /// Removes the stronghold from the claims manager.
        /// </summary>
        /// <param name="removeTarget"> stronghold to remove </param>
        public void RemoveStronghold(Stronghold removeTarget)
        {
            Vec2i minArea = GetClaimArea(new Vec2i(removeTarget.Area.MinX, removeTarget.Area.MinZ));
            Vec2i maxArea = GetClaimArea(new Vec2i(removeTarget.Area.MaxX, removeTarget.Area.MaxZ));

            for (int x = minArea.X; x <= maxArea.X; x++)
            {
                for (int z = minArea.Y; z <= maxArea.Y; z++)
                {
                    Vec2i areaKey = new Vec2i(x, z);
                    if (claimAreas.ContainsKey(areaKey))
                    {
                        claimAreas[areaKey].Remove(removeTarget);
                    }
                    
                }
            }



        }

        /// <summary>
        /// Returns a list of strongholds that overlap the given position
        /// </summary>
        /// <param name="pos"> Blockposition to check for overlapping strongholds</param>
        /// <returns></returns>
        public List<Stronghold> GetStrongholdAtPosition(BlockPos pos)
        {
            Vec2i areaKey = GetClaimArea(new Vec2i(pos.X, pos.Z));
            
            if (claimAreas.TryGetValue(areaKey, out var strongholds))
            {
                List<Stronghold> claimaints = new List<Stronghold>();
                foreach (var stronghold in strongholds)
                {
                    if (stronghold.Area.Contains(pos))
                    {
                        claimaints.Add(stronghold);
                    }
                }
                return claimaints.Count != 0 ? claimaints : null;
            }
            return null;
            
        }



        /// <summary>
        /// Get's Claim area associated with the position.
        /// </summary>
        /// <param name="pos">XZ position of the block</param>
        /// <returns> Relevant Vec2i Associated with the claim area</returns>
        private Vec2i GetClaimArea(Vec2i pos)
        {
           return new Vec2i((int)pos.X / ClaimAreaSize, (int)pos.Y / ClaimAreaSize);
        }


        /// <summary>
        /// Get's Claim area associated with the position.
        /// </summary>
        /// <param name="pos">Block position of the block</param>
        /// <returns> Relevant Vec2i Associated with the claim area</returns>
        private Vec2i GetClaimArea(BlockPos pos)
        {
            return new Vec2i((int)pos.X / ClaimAreaSize, (int)pos.Y / ClaimAreaSize);
        }


    }
}
