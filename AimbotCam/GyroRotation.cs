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
        public class GyroRotation
        {
            private List<IMyGyro> gyros = new List<IMyGyro>();
            private IMyShipController rc;

            public Vector3D target;

            Program P;

            public GyroRotation(Program P, IMyShipController rc)
            {
                this.P = P;

                P.GridTerminalSystem.GetBlocksOfType(gyros);
                this.rc = rc;
            }

            public void mainRotate()
            {
                if (target != null)
                {
                    Rotate();
                }
            }

            private void Rotate()
            {
                GyroUtils.PointInDirection(gyros, rc, target, 5);
            }

            public void SetTarget(Vector3D NewTarget)
            {
                if (!target.Equals(NewTarget))
                {
                    target = NewTarget;

                    P.Echo("Set The target to: \n" + this.target.ToString());
                }
            }

            public void SetTargetFromString(string target)
            {
                if (Vector3D.TryParse(target, out this.target))
                {
                    P.Echo("Set Target to: " + this.target.ToString());
                }
                else
                {
                    P.Echo("Parsing Failed");
                }

            }

            public void SetTargetDirection(Vector3D direction, double howFarOut)
            {
                //Normalize dis
                Vector3D NewTarget = direction;
                NewTarget.Normalize();

                //Set the lenght of Newtarget to howFarOut in M
                NewTarget = Vector3D.Multiply(NewTarget, howFarOut);

                //create waypoint out in the targetDirection
                NewTarget = rc.GetPosition() + NewTarget;

                if (!target.Equals(NewTarget))
                {
                    target = NewTarget;
                }
            }

            public void Dispose()
            {
                foreach (IMyGyro gyro in gyros)
                    gyro.GyroOverride = false;
            }
        }
    }
}
