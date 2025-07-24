using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public class TurretDetection
        {

            public const long Timeout = 120;

            public List<IMyTurretControlBlock> turrets;
            public MyDetectedEntityInfo detected;
            long detectedTicks = 0;
            public MyDetectedEntityInfo oldDetected;

            public bool currentlyTracking = false;

            public TurretDetection(List<IMyTurretControlBlock> turrets)
            {
                this.turrets = turrets;
            }

            public void GetTarget(long currentTime)
            {
                MyDetectedEntityInfo newDetected;

                foreach (var turret in turrets)
                {
                    newDetected = turret.GetTargetedEntity();
                    if (!newDetected.IsEmpty())
                    {
                        oldDetected = detected;
                        detected = newDetected;
                        detectedTicks = currentTime;
                        currentlyTracking = true;
                        break;
                    }
                }

                if (currentTime - detectedTicks > Timeout)
                {
                    currentlyTracking = false;
                }
            }
        }
    }
}