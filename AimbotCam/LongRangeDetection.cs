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
        public class LongRangeDetection
        {

            public bool foundInitialTarget = false;
            public bool hasTarget = false;
            public MyDetectedEntityInfo targetI;

            private double maximumDistance = 10000;
            private MyRelationsBetweenPlayerAndBlock[] bad = { MyRelationsBetweenPlayerAndBlock.Enemies, MyRelationsBetweenPlayerAndBlock.Neutral, MyRelationsBetweenPlayerAndBlock.NoOwnership};

            private int ticksFromLastFind = 0;
            private int ticksTimeout = 480;

            private Vector3D startLoc;
            private Program P;
            private List<IMyCameraBlock> cameraArray;
            private Random random;


            public LongRangeDetection(Vector3D InitialLocation, List<IMyCameraBlock> cameras, Program P)
            {
                cameraArray = cameras;
                this.P = P;
                random = new Random();

                if (RayCastTarget(InitialLocation + Vector3D.Multiply(Vector3D.Normalize(InitialLocation - P.cockpitpos), 5)))
                {
                    foundInitialTarget = true;
                    hasTarget = true;
                    P.Echo("FoundTarget");
                }
                else
                {
                    startLoc = InitialLocation + Vector3D.Multiply(Vector3D.Normalize(InitialLocation - P.cockpitpos), 5);
                }
            }

            public bool DoDetect()
            {
                bool foundTarget = false;

                if (ticksFromLastFind % ((int)Vector3D.Distance(targetI.Position, P.Me.GetPosition()) / 100 + 1) == 0)
                {
                    if (!targetI.IsEmpty())
                    {
                        if (Vector3D.Distance(P.cockpitpos, targetI.Position) <= maximumDistance)
                        {

                            if (RayCastTarget(GetTargetAfterTicks(targetI, ticksFromLastFind)))
                            {

                                foundTarget = true;
                                hasTarget = true;
                            }
                        }
                    }
                }

                if (!foundInitialTarget)
                {
                    if (RayCastTarget(startLoc))
                    {
                        foundInitialTarget = true;
                        foundTarget = true;
                        hasTarget = true;

                    }
                }

                if(ticksFromLastFind > ticksTimeout)
                {
                    hasTarget = false;
                }

                //Every tick
                ticksFromLastFind++;

                return foundTarget;
            }

            private bool RayCastTarget(Vector3D target)
            {
                MyDetectedEntityInfo tempTarget = default(MyDetectedEntityInfo);

                foreach (IMyCameraBlock camera in cameraArray)
                {
                    if (camera.CanScan(target))
                    {
                        tempTarget = camera.Raycast(target);

                        break;
                    }
                }

                if (!tempTarget.IsEmpty())
                {
                    if (tempTarget.Relationship == bad[0] || tempTarget.Relationship == bad[1])
                    {
                        targetI = tempTarget;

                        P.Echo("initial target found!");
                        ticksFromLastFind = 0;

                        return true;
                    }
                }

                return false;
            }

            private Vector3D GetTargetAfterTicks(MyDetectedEntityInfo detectedT, int ticks)
            {
                return detectedT.Position + Vector3D.Multiply(detectedT.Velocity, (ticks / 60)) + Vector3D.Multiply(Vector3D.Normalize(detectedT.Position - P.Me.GetPosition()), 5);
            }

            private Vector3D SpreadVectors(Vector3D vector, double ConeAngle = 0.1243549945)
            {          /*angle at 800 m with 100m spread*/
                Vector3D crossVec = Vector3D.Normalize(Vector3D.Cross(vector, Vector3D.Right));
                if (crossVec.Length() == 0)
                {
                    crossVec = Vector3D.Normalize(Vector3D.Cross(vector, Vector3D.Up));
                }

                double s = random.NextDouble();
                double r = random.NextDouble();

                double h = Math.Cos(ConeAngle);

                double phi = 2 * Math.PI * s;

                double z = h + (1 - h) * r;
                double sinT = Math.Sqrt(1 - z * z);
                double x = Math.Cos(phi) * sinT;
                double y = Math.Sin(phi) * sinT;

                return Vector3D.Normalize(Vector3D.Multiply(Vector3D.Right, x) + Vector3D.Multiply(crossVec, y) + Vector3D.Multiply(vector, z));
            }

            public Vector3D GetPredictedTargetLocation()
            {
                return (targetI.Position + Vector3D.Multiply(targetI.Velocity, (ticksFromLastFind / 60)) + Vector3D.Multiply(Vector3D.Normalize(targetI.Position - P.cockpitpos), 5));
            }

            public Vector3D GetVelocity()
            {
                return (targetI.Velocity);
            }
        }
    }
}
