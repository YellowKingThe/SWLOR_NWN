﻿using SWLOR.Game.Server.Event;
using SWLOR.Game.Server.GameObject;

using NWN;
using Object = NWN.Object;

namespace SWLOR.Game.Server.Placeable.Resource
{
    public class OnHeartbeat: IRegisteredEvent
    {
        private readonly INWScript _;

        public OnHeartbeat(INWScript script)
        {
            _ = script;
        }

        public bool Run(params object[] args)
        {
            NWPlaceable objSelf = (Object.OBJECT_SELF);
            bool hasSpawnedProp = objSelf.GetLocalInt("RESOURCE_PROP_SPAWNED") == 1;
            if (hasSpawnedProp) return false;

            string propResref = objSelf.GetLocalString("RESOURCE_PROP");
            if (string.IsNullOrWhiteSpace(propResref)) return false;

            Location location = objSelf.Location;
            NWPlaceable prop = (_.CreateObject(NWScript.OBJECT_TYPE_PLACEABLE, propResref, location));
            objSelf.SetLocalObject("RESOURCE_PROP_OBJ", prop.Object);
            objSelf.SetLocalInt("RESOURCE_PROP_SPAWNED", 1);

            _.SetEventScript(objSelf, NWScript.EVENT_SCRIPT_PLACEABLE_ON_HEARTBEAT, string.Empty);
            return true;
        }
    }
}
