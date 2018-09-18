﻿using SWLOR.Game.Server.ChatCommand.Contracts;
using SWLOR.Game.Server.Enumeration;
using SWLOR.Game.Server.GameObject;

namespace SWLOR.Game.Server.ChatCommand
{
    [CommandDetails("Displays your current position in the area.", CommandPermissionType.Player | CommandPermissionType.DM)]
    public class Pos: IChatCommand
    {
        /// <summary>
        /// Returns the current position of user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="args"></param>
        public void DoAction(NWPlayer user, params string[] args)
        {
            user.SendMessage($"Current Position: ({user.Position.m_X}, {user.Position.m_Y}, {user.Position.m_Z})");
        }
    }
}
