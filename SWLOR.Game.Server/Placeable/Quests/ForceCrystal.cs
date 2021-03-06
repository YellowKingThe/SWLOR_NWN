﻿using System;
using NWN;
using SWLOR.Game.Server.Data.Entity;
using SWLOR.Game.Server.Event;
using SWLOR.Game.Server.GameObject;
using SWLOR.Game.Server.Service.Contracts;
using Object = NWN.Object;

namespace SWLOR.Game.Server.Placeable.Quests
{
    public class ForceCrystal: IRegisteredEvent
    {
        private readonly INWScript _;
        private readonly IQuestService _quest;
        private readonly IDataService _data;
        private readonly IObjectVisibilityService _ovs;

        public ForceCrystal(
            INWScript script,
            IQuestService quest,
            IDataService data,
            IObjectVisibilityService ovs)
        {
            _ = script;
            _quest = quest;
            _data = data;
            _ovs = ovs;
        }

        public bool Run(params object[] args)
        {
            const int QuestID = 30;
            NWPlaceable crystal = Object.OBJECT_SELF;
            NWPlayer player = _.GetLastUsedBy();

            // Check player's current quest state. If they aren't on stage 2 of the quest only show a message.
            var status = _data.Single<PCQuestStatus>(x => x.PlayerID == player.GlobalID && x.QuestID == QuestID);
            var currentState = _data.Single<QuestState>(x => x.ID == status.CurrentQuestStateID);

            if (currentState.Sequence != 2)
            {
                player.SendMessage("The crystal glows quietly...");
                return false;
            }

            // Player is on stage 2, so they're able to click the crystal, get a cluster, complete the quest, and teleport back to the cavern.
            int type = crystal.GetLocalInt("CRYSTAL_COLOR_TYPE");
            string cluster;

            switch (type)
            {
                case 1: cluster = "c_cluster_blue"; break; // Blue
                case 2: cluster = "c_cluster_red"; break; // Red
                case 3: cluster = "c_cluster_green"; break; // Green 
                case 4: cluster = "c_cluster_yellow"; break; // Yellow
                default: throw new Exception("Invalid crystal color type.");
            }

            _.CreateItemOnObject(cluster, player);
            _quest.AdvanceQuestState(player, crystal, QuestID);

            // Hide the "Source of Power?" placeable so the player can't use it again.
            _ovs.AdjustVisibility(player, "81533EBB-2084-4C97-B004-8E1D8C395F56", false);

            NWObject tpWP = _.GetObjectByTag("FORCE_QUEST_LANDING");
            player.AssignCommand(() => _.ActionJumpToLocation(tpWP.Location));
            
            // Notify the player that new lightsaber perks have unlocked.
            player.FloatingText("You have unlocked the Lightsaber Blueprints perk. Find this under the Engineering category in your perks menu.");

            return true;
        }
    }
}
