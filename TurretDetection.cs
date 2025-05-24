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
            public const float maxRange = 45f;
            public const float Timeout = 2500;

            public List<IMyTurretControlBlock> turrets;
            public MyDetectedEntityInfo detected;
            public MyDetectedEntityInfo oldDetected;

            public bool currentlyTracking = false;
            public long mostRecentTime = 0;

            public TurretDetection(List<IMyTurretControlBlock> turrets)
            {
                this.turrets = turrets;
            }

            public void GetTarget()
            {
                MyDetectedEntityInfo newDetected;

                foreach (var turret in turrets)
                {
                    newDetected = turret.GetTargetedEntity();
                    if (!newDetected.IsEmpty())
                    {
                        mostRecentTime = newDetected.TimeStamp;
                        if (currentlyTracking == false || (newDetected.Position - oldDetected.Position).LengthSquared() < maxRange * maxRange)
                        {
                            oldDetected = detected;
                            detected = newDetected;
                            currentlyTracking = true;
                            break;
                        }
                    }
                }

                if (mostRecentTime - detected.TimeStamp > Timeout)
                {
                    currentlyTracking = false;
                }
            }
        }
    }
}