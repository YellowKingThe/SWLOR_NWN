﻿using SWLOR.Game.Server;
using SWLOR.Game.Server.Event.Conversation;
using static NWN.NWScript;

// ReSharper disable once CheckNamespace
namespace NWN.Scripts
{
#pragma warning disable IDE1006 // Naming Styles
    internal class has_skill_or_8
#pragma warning restore IDE1006 // Naming Styles
    {
        public static int Main()
        {
            return App.RunEvent<HasSkillRank>(8, "OR") ? TRUE : FALSE;
        }
    }
}
