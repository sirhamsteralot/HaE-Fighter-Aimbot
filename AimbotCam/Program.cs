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
        #region Names
        INISerializer nameSerializer = new INISerializer("Blocknames");

        string MAINCAMERANAME { get { return (string)nameSerializer.GetValue("MAINCAMERANAME"); } }
        string REMOTECONTROLNAME { get { return (string)nameSerializer.GetValue("REMOTECONTROLNAME"); } }
        string PROJECTORNAME { get { return (string)nameSerializer.GetValue("PROJECTORNAME"); } }
        string LEADPROJECTORNAME { get { return (string)nameSerializer.GetValue("LEADPROJECTORNAME"); } }
        string COCKPITNAME { get { return (string)nameSerializer.GetValue("COCKPITNAME"); } }
        #endregion

        double detectionRange = 1000;
        double projectionDistanceFromCockpit = 30;
        Vector3D cockpitpos;
        Random random;

        LongRangeDetection detection;
        Trajectory trajectoryCalculation;
        ProjectorVisualization projector;
        ProjectorVisualization leadProjector;
        List<IMyCameraBlock> cameras;
        IMyCameraBlock mainCam;
        IMyRemoteControl rc;
        IMyShipController cockpit;

        GyroRotation autoAim;
        GunSequencer guns;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            #region serializer
            nameSerializer.AddValue("MAINCAMERANAME", x => x, "AimCamera");
            nameSerializer.AddValue("REMOTECONTROLNAME", x => x, "Remote");
            nameSerializer.AddValue("PROJECTORNAME", x => x, "Proj");
            nameSerializer.AddValue("LEADPROJECTORNAME", x => x, "LeadProj");
            nameSerializer.AddValue("COCKPITNAME", x => x, "Cockpit");

            string customDat = Me.CustomData;
            nameSerializer.FirstSerialization(ref customDat);
            Me.CustomData = customDat;
            nameSerializer.DeSerialize(Me.CustomData);
            #endregion

            cameras = new List<IMyCameraBlock>();
            GridTerminalSystem.GetBlocksOfType(cameras);
            foreach (var cam in cameras)
                cam.EnableRaycast = true;

            mainCam = GridTerminalSystem.GetBlockWithName(MAINCAMERANAME) as IMyCameraBlock;
            rc = GridTerminalSystem.GetBlockWithName(REMOTECONTROLNAME) as IMyRemoteControl;
            cockpit = GridTerminalSystem.GetBlockWithName(COCKPITNAME) as IMyShipController;
            var proj = GridTerminalSystem.GetBlockWithName(PROJECTORNAME) as IMyProjector;
            var leadproj = GridTerminalSystem.GetBlockWithName(LEADPROJECTORNAME) as IMyProjector;

            var gunList = new List<IMyUserControllableGun>();
            GridTerminalSystem.GetBlocksOfType(gunList, x => x is IMySmallGatlingGun);
            guns = new GunSequencer(gunList);


            projector = new ProjectorVisualization(proj, new Vector3I(0, 1, 0));
            leadProjector = new ProjectorVisualization(leadproj, new Vector3I(0,-2,0));

            autoAim = null;
            DisposeAuto();

            random = new Random();
        }

        public void Main(string argument)
        {
            //MUST BE EXECUTED AT THE START OF EACH TICK!
            cockpitpos = cockpit.GetPosition() + cockpit.WorldMatrix.Backward * 0.8 - cockpit.WorldMatrix.Up * 0.5;

            if (argument.ToUpper() == "TARGET")
            {
                DisposeDetection();
                DisposeAuto();
                AquireTarget();
            }
            else if (argument.ToUpper() == "AUTOMATIC")
            {
                if (autoAim == null)
                    autoAim = new GyroRotation(this, rc);
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
                    autoAim = new GyroRotation(this, rc);
                    if (!guns.firing)
                        guns.Shoot();
                }
                else
                {
                    DisposeAuto();
                    guns.StopShooting();
                }     
            }

                //Eachtick
            if (detection != null)
            {

                if(detection.DoDetect())
                {
                    GetTargetInterception(detection.targetI);
                }

                if (!detection.hasTarget)
                {
                    DisposeDetection();
                    DisposeAuto();
                }
                    
            }
            guns.Main();
            
        }

        public void GetTargetInterception(MyDetectedEntityInfo target)
        {
            trajectoryCalculation = new Trajectory(rc.GetShipVelocities().LinearVelocity, cockpitpos, rc.WorldMatrix.Forward, 400);


            projector.UpdatePosition(cockpitpos + Vector3D.Normalize(target.Position - cockpitpos) * projectionDistanceFromCockpit);

            if (Vector3D.DistanceSquared(target.Position, rc.GetPosition()) < 850 * 850)
            {
                var intersection = trajectoryCalculation.SimulateTrajectory(target);
                if (intersection.HasValue)
                {
                    leadProjector.UpdatePosition(cockpitpos + Vector3D.Normalize(intersection.Value - cockpitpos) * (projectionDistanceFromCockpit - 1));
                    leadProjector.Enable();


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
                    leadProjector.Disable();
                    DisposeAuto();
                }
            } else
            {
                leadProjector.Disable();
                DisposeAuto();
            }

            
        }

        public void AquireTarget()
        {

            for (int i = 0; i < 25; i ++)
            {
                Echo($"Pass: {i}");

                MyDetectedEntityInfo detected = default(MyDetectedEntityInfo);
                if (i == 0)
                {
                    detected = mainCam.Raycast(detectionRange);
                }    
                else
                {
                    float pitch = random.Next(1, 50) / 10f;
                    float yaw = random.Next(1, 50) / 10f;

                    detected = mainCam.Raycast(detectionRange, pitch, yaw);
                }
                    

                
                if (detected.HitPosition.HasValue)
                {
                    detection = new LongRangeDetection(detected.HitPosition.Value, cameras, this);
                    Echo($"Target aquired @ {detected.HitPosition.ToString()}");

                    projector.Enable();

                    break;
                }

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

        public void DisposeDetection()
        {
            detection = null;
            projector.Disable();
            leadProjector.Disable();
        }

        public void DisposeAuto()
        {
            if(autoAim != null)
                autoAim.Dispose();
            autoAim = null;
        }
    }
}