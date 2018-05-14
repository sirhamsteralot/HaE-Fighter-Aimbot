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
    partial class Program
    {
        public class GunSequencer
        {
            int counter = 0;
            List<IMyUserControllableGun> guns;
            public bool firing = false;

            public GunSequencer(List<IMyUserControllableGun> guns)
            {
                this.guns = guns;
            }

            public void Main()
            {
                if (firing)
                {
                    if (counter < guns.Count())
                    {
                        guns[counter].ApplyAction("Shoot_On");

                        counter++;
                    }
                } else
                {
                    foreach(var gun in guns)
                    {
                        gun.ApplyAction("Shoot_Off");
                        counter = 0;
                    }
                }
            }

            public void Shoot()
            {
                firing = true;
            }

            public void StopShooting()
            {
                firing = false;
            }
        }
    }
}
