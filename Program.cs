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
        INISerializer nameSerializer = new INISerializer("Blocknames");

        string COCKPITNAME { get { return (string)nameSerializer.GetValue("COCKPITNAME"); } }

        INISerializer parameterSerializer = new INISerializer("Parameters");
        double projectileVelocity { get { return (double)parameterSerializer.GetValue("projectileVelocity"); } }
        #endregion

        private readonly string[] runningIndicator = new string[] { "- - - - -", "- - 0 - -", "- 0 - 0 -", "0 - 0 - 0", "- 0 - 0 -", "- - 0 - -" };
        const string versionString = "v1.0.1";

        int update100Counter = 0;
        double averageRuntime = 0;

        Random random;

        TurretDetection turretDetection;
        Trajectory trajectoryCalculation;
        IMyShipController cockpit;
        IMyProgrammableBlock missileManager;

        GyroRotation autoAim;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;

            #region serializer
            nameSerializer.AddValue("COCKPITNAME", x => x, "Cockpit");
            parameterSerializer.AddValue("projectileVelocity", x => double.Parse(x), 400);

            string customDat = Me.CustomData;
            nameSerializer.FirstSerialization(ref customDat);
            parameterSerializer.FirstSerialization(ref customDat);
            Me.CustomData = customDat;
            nameSerializer.DeSerialize(Me.CustomData);
            parameterSerializer.DeSerialize(Me.CustomData);
            #endregion

            cockpit = GridTerminalSystem.GetBlockWithName(COCKPITNAME) as IMyShipController;
            if (cockpit == null)
                throw new Exception($"No cockpit named \"{COCKPITNAME}\"");

            var gunList = new List<IMyUserControllableGun>();
            GridTerminalSystem.GetBlocksOfType(gunList, x => x is IMySmallGatlingGun);

            var turretList = new List<IMyTurretControlBlock>();
            GridTerminalSystem.GetBlocksOfType(turretList);
            turretDetection = new TurretDetection(turretList);

            autoAim = null;
            DisposeAuto();

            random = new Random();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            averageRuntime = averageRuntime * (1 - 0.05) + Runtime.LastRunTimeMs * 0.95;

            if ((updateSource & UpdateType.Update100) == UpdateType.Update100)
            {
                Echo($"HaE Fighter Aimbot {versionString}");
                Echo(runningIndicator[update100Counter++ % runningIndicator.Length]);
                Echo($"runtime average: {averageRuntime:N4}");
            }

            if (argument.ToUpper() == "AUTOMATIC")
            {
                if (autoAim == null)
                    autoAim = new GyroRotation(this, cockpit);
                else
                    DisposeAuto();

                Echo("Automatic!");
            }

            if (autoAim != null || (updateSource | UpdateType.Update10) == UpdateType.Update10)
            {
                turretDetection.GetTarget(Runtime.LifetimeTicks);

                if (turretDetection.currentlyTracking)
                {
                    GetTargetInterception(turretDetection.detected, turretDetection.oldDetected);
                }
            }
        }

        public void GetTargetInterception(MyDetectedEntityInfo target, MyDetectedEntityInfo oldTarget)
        {
            Vector3D cockpitPosition = cockpit.GetPosition();
            trajectoryCalculation = new Trajectory(cockpit.GetShipVelocities().LinearVelocity, cockpitPosition, cockpit.WorldMatrix.Forward, projectileVelocity);

            Vector3D acceleration = -cockpit.GetNaturalGravity();

            if (!oldTarget.IsEmpty())
            {
                acceleration += (target.Velocity - oldTarget.Velocity) * 60;
            }

            var intersection = trajectoryCalculation.SimulateTrajectory(target, acceleration);
            if (intersection.HasValue)
            {
                if (autoAim != null)
                {
                    var dist = intersection.Value - cockpitPosition;
                    var direction = SpreadVectors(Vector3D.Normalize(dist), 0.0174532925);

                    autoAim.SetTarget(direction);
                    autoAim.mainRotate();
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
    }
}