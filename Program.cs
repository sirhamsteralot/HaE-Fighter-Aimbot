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

        string PROJECTORNAME { get { return (string)nameSerializer.GetValue("PROJECTORNAME"); } }
        string LEADPROJECTORNAME { get { return (string)nameSerializer.GetValue("LEADPROJECTORNAME"); } }
        string COCKPITNAME { get { return (string)nameSerializer.GetValue("COCKPITNAME"); } }
        string MISSILECONTROLLER { get { return (string)nameSerializer.GetValue("MISSILECONTROLLER"); } }


        INISerializer parameterSerializer = new INISerializer("Parameters");
        double projectionDistanceFromCockpit { get { return (double)parameterSerializer.GetValue("projectionDistanceFromCockpit"); } }
        Vector3I projectionOffset { get { return new Vector3I(
            (int)parameterSerializer.GetValue("projectionOffsetX"), 
            (int)parameterSerializer.GetValue("projectionOffsetY"), 
            (int)parameterSerializer.GetValue("projectionOffsetZ")
            ); } }

        #endregion


        Vector3D cockpitpos;
        Random random;

        TurretDetection turretDetection;
        Trajectory trajectoryCalculation;
        ProjectorVisualization projector;
        ProjectorVisualization leadProjector;
        IMyShipController cockpit;
        IMyProgrammableBlock missileManager;

        GyroRotation autoAim;
        GunSequencer guns;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            #region serializer
            nameSerializer.AddValue("PROJECTORNAME", x => x, "Proj");
            nameSerializer.AddValue("LEADPROJECTORNAME", x => x, "LeadProj");
            nameSerializer.AddValue("COCKPITNAME", x => x, "Cockpit");
            nameSerializer.AddValue("MISSILECONTROLLER", x => x, "MissileController");

            parameterSerializer.AddValue("projectionDistanceFromCockpit", x => double.Parse(x), 30);

            parameterSerializer.AddValue("projectionOffsetX", x => int.Parse(x), 0);
            parameterSerializer.AddValue("projectionOffsetY", x => int.Parse(x), 0);
            parameterSerializer.AddValue("projectionOffsetZ", x => int.Parse(x), 0);

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

            var proj = GridTerminalSystem.GetBlockWithName(PROJECTORNAME) as IMyProjector;
            var leadproj = GridTerminalSystem.GetBlockWithName(LEADPROJECTORNAME) as IMyProjector;

            var gunList = new List<IMyUserControllableGun>();
            GridTerminalSystem.GetBlocksOfType(gunList, x => x is IMySmallGatlingGun);
            guns = new GunSequencer(gunList);

            var turretList = new List<IMyTurretControlBlock>();
            GridTerminalSystem.GetBlocksOfType(turretList);
            turretDetection = new TurretDetection(turretList);

            if (proj != null)
                projector = new ProjectorVisualization(proj, projectionOffset);

            if (leadproj != null)
                leadProjector = new ProjectorVisualization(leadproj, projectionOffset);

            missileManager = GridTerminalSystem.GetBlockWithName(MISSILECONTROLLER) as IMyProgrammableBlock;

            autoAim = null;
            DisposeAuto();

            random = new Random();
        }


        public void Main(string argument, UpdateType uType)
        {
            //MUST BE EXECUTED AT THE START OF EACH TICK!
            cockpitpos = cockpit.GetPosition() + cockpit.WorldMatrix.Backward * 0.8 - cockpit.WorldMatrix.Up * 0.5;

            if (argument.ToUpper() == "AUTOMATIC")
            {
                if (autoAim == null)
                    autoAim = new GyroRotation(this, cockpit);
                else
                    DisposeAuto();

                Echo("Automatic!");
            }
            else if (argument.ToUpper() == "HOLDFIRE")
            {
                if (!guns.firing)
                    guns.Shoot();
                else
                    guns.StopShooting();
            }
            else if (argument.ToUpper() == "FULLAUTOMATIC")
            {
                if (autoAim == null)
                {
                    autoAim = new GyroRotation(this, cockpit);
                    if (!guns.firing)
                        guns.Shoot();
                }
                else
                {
                    DisposeAuto();
                    guns.StopShooting();
                }
            }

            turretDetection.GetTarget();

            if (turretDetection.currentlyTracking)
            {
                GetTargetInterception(turretDetection.detected, turretDetection.oldDetected);
            }
            else
            {
                if (leadProjector != null)
                    leadProjector.Disable();

                if (projector != null)
                    projector.Disable();
            }

            guns.Main();
        }

        public void GetTargetInterception(MyDetectedEntityInfo target, MyDetectedEntityInfo oldTarget)
        {
            trajectoryCalculation = new Trajectory(cockpit.GetShipVelocities().LinearVelocity, cockpitpos, cockpit.WorldMatrix.Forward, 400);

            if (projector != null)
            {
                projector.UpdatePosition(cockpitpos + Vector3D.Normalize(target.Position - cockpitpos) * projectionDistanceFromCockpit);
                projector.Enable();
            }


            Vector3D acceleration = -cockpit.GetNaturalGravity();

            if (!oldTarget.IsEmpty())
            {
                acceleration += target.Velocity - oldTarget.Velocity;
            }

            var intersection = trajectoryCalculation.SimulateTrajectory(target, acceleration);
            if (intersection.HasValue)
            {
                if (leadProjector != null)
                {
                    leadProjector.UpdatePosition(cockpitpos + Vector3D.Normalize(intersection.Value - cockpitpos) * (projectionDistanceFromCockpit - 1));
                    leadProjector.Enable();
                }


                if (autoAim != null)
                {
                    var dist = intersection.Value - cockpitpos;
                    var direction = SpreadVectors(Vector3D.Normalize(dist), 0.0174532925);

                    autoAim.SetTarget(direction);
                    autoAim.mainRotate();
                }
            }
            else
            {
                if (leadProjector != null)
                    leadProjector.Disable();

                if (projector != null)
                    projector.Disable();

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