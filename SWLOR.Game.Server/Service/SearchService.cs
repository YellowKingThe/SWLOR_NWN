﻿using System;
using System.Linq;
using SWLOR.Game.Server.Data.Contracts;
using SWLOR.Game.Server.Data;
using SWLOR.Game.Server.GameObject;

using NWN;
using SWLOR.Game.Server.Data.Entity;
using SWLOR.Game.Server.Enumeration;
using SWLOR.Game.Server.Service.Contracts;
using SWLOR.Game.Server.ValueObject;

namespace SWLOR.Game.Server.Service
{
    public class SearchService : ISearchService
    {
        private const string SearchSiteCopyResref = "srch_plc_copy";
        private const string SearchSiteIDVariableName = "SearchSiteID";
        private const string SearchSiteDCVariableName = "SearchSiteDC";
        private const string SearchSiteLootTableVariableName = "SearchLootTable";
        private const int ExtraSearchPerNumberLevels = 5;

        private readonly INWScript _;
        private readonly IRandomService _random;
        private readonly IDataService _data;
        private readonly IQuestService _quest;
        private readonly ISerializationService _serialization;
        private readonly ILocalVariableService _localVariable;
        private readonly IColorTokenService _color;
        private readonly IDurabilityService _durability;

        public SearchService(
            INWScript script,
            IRandomService random,
            IDataService data,
            IQuestService quest,
            ISerializationService serialization,
            ILocalVariableService localVariable,
            IColorTokenService color,
            IDurabilityService durability)
        {
            _ = script;
            _random = random;
            _data = data;
            _quest = quest;
            _serialization = serialization;
            _localVariable = localVariable;
            _color = color;
            _durability = durability;
        }

        public void OnChestClose(NWPlaceable oChest)
        {
            foreach (NWItem item in oChest.InventoryItems)
            {
                item.Destroy();
            }
        }

        public void OnChestDisturbed(NWPlaceable oChest)
        {
            NWPlayer oPC = (_.GetLastDisturbed());
            if (!oPC.IsPlayer && !oPC.IsDM) return;

            string pcName = oPC.Name;
            NWItem oItem = (_.GetInventoryDisturbItem());
            int disturbType = _.GetInventoryDisturbType();

            if (disturbType == NWScript.INVENTORY_DISTURB_TYPE_ADDED)
            {
                _.CopyItem(oItem.Object, oPC.Object, NWScript.TRUE);
                oItem.Destroy();
            }
            else if (disturbType == NWScript.INVENTORY_DISTURB_TYPE_REMOVED)
            {
                SaveChestInventory(oPC, oChest, false);
                string itemName = oItem.Name;
                if (string.IsNullOrWhiteSpace(itemName)) itemName = "money";

                float minSearchSeconds = 1.5f;
                float maxSearchSeconds = 4.5f;
                float searchDelay = _random.RandomFloat() * (maxSearchSeconds - minSearchSeconds) + minSearchSeconds;

                oPC.AssignCommand(() =>
                {
                    _.ActionPlayAnimation(NWScript.ANIMATION_LOOPING_GET_LOW, 1.0f, searchDelay);
                });

                _.ApplyEffectToObject(NWScript.DURATION_TYPE_TEMPORARY, _.EffectCutsceneImmobilize(), oPC.Object, searchDelay);

                // Notify party members in the vicinity

                NWPlayer player = (_.GetFirstPC());
                while (player.IsValid)
                {
                    if (_.GetDistanceBetween(oPC.Object, player.Object) <= 20.0f &&
                        player.Area.Equals(oPC.Area) &&        
                        _.GetFactionEqual(player.Object, oPC.Object) == NWScript.TRUE)
                    {
                        player.SendMessage(pcName + " found " + itemName + ".");
                    }

                    player = (_.GetNextPC());
                }
            }
        }

        public void OnChestOpen(NWPlaceable oChest)
        {
            NWPlayer oPC = (_.GetLastOpenedBy());
            if (!oPC.IsPlayer) return;

            if (_.GetActionMode(oPC.Object, NWScript.ACTION_MODE_STEALTH) == NWScript.TRUE)
                _.SetActionMode(oPC.Object, NWScript.ACTION_MODE_STEALTH, NWScript.FALSE);
            
            string resref = oChest.Resref;
            int chestID = oChest.GetLocalInt(SearchSiteIDVariableName);
            int skillRank = _.GetSkillRank(NWScript.SKILL_SEARCH, oPC.Object);
            int numberOfSearches = (skillRank / ExtraSearchPerNumberLevels) + 1;
            PCSearchSite searchEntity = _data.SingleOrDefault<PCSearchSite>(x => x.PlayerID == oPC.GlobalID && x.SearchSiteID == chestID);
            DateTime timeLock = DateTime.UtcNow;

            if (numberOfSearches <= 0) numberOfSearches = 1;

            if (searchEntity != null)
            {
                timeLock = searchEntity.UnlockDateTime;
            }

            if (resref == SearchSiteCopyResref)
            {
                oChest.IsUseable = false;
            }

            _quest.SpawnQuestItems(oChest, oPC);

            if (timeLock < DateTime.UtcNow || searchEntity == null)
            {
                int dc = oChest.GetLocalInt(SearchSiteDCVariableName);

                for (int search = 1; search <= numberOfSearches; search++)
                {
                    RunSearchCycle(oPC, oChest, dc);
                    dc += _random.Random(3) + 1;
                }

                SaveChestInventory(oPC, oChest, false);
            }
            else
            {
                var searchItems = _data.Where<PCSearchSiteItem>(x => x.PlayerID == oPC.GlobalID && x.SearchSiteID == searchEntity.SearchSiteID).ToList();
                foreach (PCSearchSiteItem item in searchItems)
                {
                    NWItem oItem = _serialization.DeserializeItem(item.SearchItem, oChest);

                    // Prevent item duplication in containers
                    if (oItem.HasInventory)
                    {
                        foreach (NWItem containerItem in oItem.InventoryItems)
                        {
                            containerItem.Destroy();
                        }
                    }
                }
            }
        }

        public void OnChestUsed(NWPlaceable oChest)
        {
            NWPlayer oPC = (_.GetLastUsedBy());
            if (!oPC.IsPlayer) return;

            
            NWPlaceable oCopy = (_.CreateObject(NWScript.OBJECT_TYPE_PLACEABLE, SearchSiteCopyResref, oChest.Location));
            oCopy.Name = oChest.Name;
            _.SetFacingPoint(oPC.Position);

            _localVariable.CopyVariables(oChest, oCopy);

            oPC.AssignCommand(() =>
            {
                _.ActionInteractObject(oCopy.Object);
            });
        }

        private void SaveChestInventory(NWPlayer oPC, NWPlaceable oChest, bool resetTimeLock)
        {
            int chestID = oChest.GetLocalInt(SearchSiteIDVariableName);
            PCSearchSite entity = _data.SingleOrDefault<PCSearchSite>(x => x.PlayerID == oPC.GlobalID && x.SearchSiteID == chestID);

            int lockHours = _random.Random(2, 5);
            DateTime lockTime = DateTime.UtcNow.AddHours(lockHours);
            if (entity != null)
            {
                if (resetTimeLock)
                {
                    lockTime = entity.UnlockDateTime;
                }
                _data.SubmitDataChange(entity, DatabaseActionType.Delete);
            }

            entity = new PCSearchSite
            {
                PlayerID = oPC.GlobalID,
                SearchSiteID = chestID,
                UnlockDateTime = lockTime
            };

            foreach (NWItem item in oChest.InventoryItems)
            {
                if (item.GetLocalInt("QUEST_ID") <= 0)
                {
                    PCSearchSiteItem itemEntity = new PCSearchSiteItem
                    {
                        SearchItem = _serialization.Serialize(item),
                        SearchSiteID = entity.SearchSiteID
                    };

                    _data.SubmitDataChange(itemEntity, DatabaseActionType.Insert);
                }
            }
        }


        private void RunSearchCycle(NWPlayer oPC, NWPlaceable oChest, int iDC)
        {
            int lootTable = oChest.GetLocalInt(SearchSiteLootTableVariableName);
            int skill = _.GetSkillRank(NWScript.SKILL_SEARCH, oPC.Object);
            if (skill > 10) skill = 10;
            else if (skill < 0) skill = 0;

            int roll = _random.Random(20) + 1;
            int combinedRoll = roll + skill;
            if (roll + skill >= iDC)
            {
                oPC.FloatingText(_color.SkillCheck("Search: *success*: (" + roll + " + " + skill + " = " + combinedRoll + " vs. DC: " + iDC + ")"));
                ItemVO spawnItem = PickResultItem(lootTable);

                if (!string.IsNullOrWhiteSpace(spawnItem.Resref) && spawnItem.Quantity > 0)
                {
                    NWItem foundItem = (_.CreateItemOnObject(spawnItem.Resref, oChest.Object, spawnItem.Quantity, ""));
                    float maxDurability = _durability.GetMaxDurability(foundItem);
                    if (maxDurability > -1)
                        _durability.SetDurability(foundItem, _random.RandomFloat() * maxDurability + 1);
                }
            }
            else
            {
                oPC.FloatingText(_color.SkillCheck("Search: *failure*: (" + roll + " + " + skill + " = " + combinedRoll + " vs. DC: " + iDC + ")"));
            }
        }


        private ItemVO PickResultItem(int lootTableID)
        {
            var lootTableItems = _data.Where<LootTableItem>(x => x.LootTableID == lootTableID).ToList();

            int[] weights = new int[lootTableItems.Count];

            for (int x = 0; x < lootTableItems.Count; x++)
            {
                weights[x] = lootTableItems.ElementAt(x).Weight;
            }
            int randomIndex = _random.GetRandomWeightedIndex(weights);

            LootTableItem itemEntity = lootTableItems.ElementAt(randomIndex);
            int quantity = _random.Random(itemEntity.MaxQuantity) + 1;

            ItemVO result = new ItemVO
            {
                Quantity = quantity,
                Resref = itemEntity.Resref
            };

            return result;
        }
    }
}
