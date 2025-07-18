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
        #region Serializers
        INISerializer parameterSerializer = new INISerializer("Parameters");
        double projectileVelocity { get { return (double)parameterSerializer.GetValue("projectileVelocity"); } }
        double _projectileVelocity;
        #endregion

        private readonly string[] runningIndicator = new string[] { "- - - - -", "- - 0 - -", "- 0 - 0 -", "0 - 0 - 0", "- 0 - 0 -", "- - 0 - -" };
        const string versionString = "v1.1.3";

        int update100Counter = 0;
        double averageRuntime = 0;

        Random random;

        TurretDetection turretDetection;
        IMyShipController cockpit;

        GyroRotation autoAim;

        Vector3D oldTargetAcceleration;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;

            #region serializer
            parameterSerializer.AddValue("projectileVelocity", x => double.Parse(x), 400);

            string customDat = Me.CustomData;
            parameterSerializer.FirstSerialization(ref customDat);
            Me.CustomData = customDat;
            parameterSerializer.DeSerialize(Me.CustomData);
            #endregion

            GridTerminalSystem.GetBlocksOfType<IMyCockpit>(null, x =>
            {
                if (x.IsMainCockpit)
                {
                    cockpit = x;
                }
                else if (cockpit == null)
                {
                    cockpit = x;
                }

                return false;
            });

            if (cockpit == null)
                throw new Exception($"No cockpit found!");

            var gunList = new List<IMyUserControllableGun>();
            GridTerminalSystem.GetBlocksOfType(gunList, x => x is IMySmallGatlingGun);

            var turretList = new List<IMyTurretControlBlock>();
            GridTerminalSystem.GetBlocksOfType(turretList);
            turretDetection = new TurretDetection(turretList);

            autoAim = null;
            DisposeAuto();

            random = new Random();
            _projectileVelocity = projectileVelocity;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            averageRuntime = averageRuntime * 0.05 + Runtime.LastRunTimeMs * 0.95;

            if ((updateSource & UpdateType.Update100) == UpdateType.Update100)
            {
                Echo($"HaE Fighter Aimbot {versionString}");
                Echo(runningIndicator[update100Counter++ % runningIndicator.Length]);
                Echo($"tracking: {turretDetection.currentlyTracking}");
                Echo($"runtime average: {averageRuntime:N4}");
            }


            if (argument.ToUpper() == "AUTOMATIC")
            {
                if (autoAim == null)
                {
                    Echo("Automatic!");
                    autoAim = new GyroRotation(this, cockpit);
                }
                else
                {
                    Echo("Automatic Disabled!");

                    DisposeAuto();
                }
            }

            if (autoAim != null || ((updateSource & UpdateType.Update10) == UpdateType.Update10))
            {
                turretDetection.GetTarget(Runtime.LifetimeTicks);

                if (turretDetection.currentlyTracking)
                {
                    GetTargetInterception(turretDetection.detected, turretDetection.oldDetected);
                }
            }

            if ((updateSource & UpdateType.Update1) == UpdateType.Update1 && autoAim != null)
            {
                autoAim.MainRotate(Runtime.LifetimeTicks);
            }
        }

        public void GetTargetInterception(MyDetectedEntityInfo target, MyDetectedEntityInfo oldTarget)
        {
            Vector3D cockpitPosition = cockpit.WorldMatrix.Translation;
            Vector3D velocity = cockpit.GetShipVelocities().LinearVelocity;
            Vector3D acceleration = -cockpit.GetNaturalGravity();

            if (!oldTarget.IsEmpty())
            {
                Vector3D targetAcceleration = Vector3D.Lerp((target.Velocity - oldTarget.Velocity) * 60, oldTargetAcceleration, 0.8);
                oldTargetAcceleration = targetAcceleration;

                acceleration += targetAcceleration;
            }

            var intersection = SimulateTrajectory(target, acceleration, cockpitPosition, velocity, projectileVelocity);
            if (intersection.HasValue)
            {
                if (autoAim != null)
                {
                    var dist = intersection.Value - cockpitPosition;
                    var direction = SpreadVectors(Vector3D.Normalize(dist), 0.0174532925);

                    autoAim.SetTargetDirection(Vector3D.Normalize(direction));
                }
            }
            else
            {
                DisposeAuto();
            }
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

        public void DisposeAuto()
        {
            if (autoAim != null)
                autoAim.Dispose();
            autoAim = null;
        }

        public Vector3D? SimulateTrajectory(MyDetectedEntityInfo target, Vector3D acceleration, Vector3D myPosition, Vector3D myVelocity, double projectileVelocity)
        {
            Vector3D relVelocity = target.Velocity - myVelocity;
            Vector3D P0 = target.Position;
            Vector3D V0 = relVelocity;

            if (V0.LengthSquared() < 0.0001) // More robust zero-speed check
                return P0;

            Vector3D P1 = myPosition;
            Vector3D D = P0 - P1;
            double projectileSpeed = projectileVelocity;

            Vector3D halfA = 0.5 * acceleration;

            // Quartic coefficients: a*t^4 + b*t^3 + c*t^2 + d*t + e = 0
            double a = halfA.LengthSquared();
            double b = 2 * Vector3D.Dot(halfA, V0);
            double c = V0.LengthSquared() + 2 * Vector3D.Dot(halfA, D) - projectileSpeed * projectileSpeed;
            double d = 2 * Vector3D.Dot(V0, D);
            double e = D.LengthSquared();

            // Solve quartic for time-to-intercept
            double? t = FastRootSolver.SolveQuarticFast(a, b, c, d, e);

            if (!t.HasValue || t.Value < 0)
                return null;

            double time = t.Value + 1.5/60.0; // add one tick because we can only ever aim for the next tick at best
            double maxAccelTime = Math.Min(2, time);

            // Predict future target position using uniformly accelerated motion
            Vector3D futureTargetPosition = P0 + V0 * time + 0.5 * (acceleration * maxAccelTime * maxAccelTime);

            return futureTargetPosition;
        }
        

    }
}