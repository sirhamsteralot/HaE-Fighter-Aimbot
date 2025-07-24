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
        double ProjectileVelocity { get { return (double)parameterSerializer.GetValue("projectileVelocity"); } }
        double _projectileVelocity;
        #endregion

        private readonly string[] runningIndicator = new string[] { "- - - - -", "- - 0 - -", "- 0 - 0 -", "0 - 0 - 0", "- 0 - 0 -", "- - 0 - -" };
        const string versionString = "v1.1.7";

        int update100Counter = 0;
        double averageRuntime = 0;

        Random random;

        TurretDetection turretDetection;
        IMyShipController cockpit;

        GyroRotation autoAim;

        Vector3D oldTargetAcceleration;
        Vector3D oldMyAcceleration;
        Vector3D oldMyVelocity;
        Scheduler gunScheduler;

        List<IMyUserControllableGun> guns = new List<IMyUserControllableGun>();
        bool activeShooting = false;
        int currentlyShootingGun = -1;

        double FullCycleIntent = 6;

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

            GridTerminalSystem.GetBlocksOfType(guns);

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
            _projectileVelocity = ProjectileVelocity;

            gunScheduler = new Scheduler();
            gunScheduler.AddTask(GunLoop());
        }

        public void Main(string argument, UpdateType updateSource)
        {
            averageRuntime = averageRuntime * 0.05 + Runtime.LastRunTimeMs * 0.95;

            if ((updateSource & UpdateType.Update100) == UpdateType.Update100)
            {
                Echo($"HaE Fighter Aimbot {versionString}");
                Echo(runningIndicator[update100Counter++ % runningIndicator.Length]);
                Echo($"shooting: {activeShooting}");
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

            if ((updateSource & UpdateType.Update1) == UpdateType.Update1)
            {
                gunScheduler.Main();
            }

            if (autoAim != null || ((updateSource & UpdateType.Update10) == UpdateType.Update10))
            {
                turretDetection.GetTarget(Runtime.LifetimeTicks);

                if (turretDetection.currentlyTracking)
                {
                    GetTargetInterception(turretDetection.detected, turretDetection.oldDetected);
                }
                CheckShooting();
            }

            if ((updateSource & UpdateType.Update1) == UpdateType.Update1 && autoAim != null)
            {
                autoAim.MainRotate(Runtime.LifetimeTicks);
            }
        }

        public IEnumerator<bool> GunLoop()
        {
            int gunCount = guns.Count;
            int tickDelay = (int)Math.Floor(FullCycleIntent*60 / (double)gunCount) - gunCount;


            while (true)
            {
                if (activeShooting)
                {
                    for (int i = 0; i < gunCount; i++)
                    {
                        if (i == currentlyShootingGun)
                        {
                            continue;
                        }

                        guns[i].Enabled = false;
                    }

                    for (int i = 0; i < gunCount; i++)
                    {
                        if (i == currentlyShootingGun)
                        {
                            continue;
                        }

                        guns[i].Enabled = true;

                        if (!activeShooting)
                            break;

                        yield return true;

                        if (!activeShooting)
                            break;

                        guns[i].Enabled = false;

                        for (int j = 0; j < tickDelay; j++)
                        {
                            if (!activeShooting)
                                break;

                            yield return true;
                        }
                    }
                }

                yield return true;
            }
        }

        public void CheckShooting()
        {
            if (activeShooting)
            {
                if (!guns[currentlyShootingGun].IsShooting)
                {
                    activeShooting = false;
                    currentlyShootingGun = -1;

                    foreach (var gun in guns)
                    {
                        gun.Enabled = true;
                    }
                }

                return;
            }

            for (int i = 0; i < guns.Count; i++)
            {
                if (guns[i].IsShooting)
                {
                    currentlyShootingGun = i;
                    activeShooting = true;
                    return;
                }
            }

            activeShooting = false;
            currentlyShootingGun = -1;
        }

        
        public void GetTargetInterception(MyDetectedEntityInfo target, MyDetectedEntityInfo oldTarget)
        {
            Vector3D cockpitPosition = cockpit.WorldMatrix.Translation;
            Vector3D velocity = cockpit.GetShipVelocities().LinearVelocity;
            Vector3D tAcceleration = -cockpit.GetNaturalGravity();
            Vector3D myAcceleration = velocity - oldMyVelocity;
            oldMyVelocity = velocity;

            myAcceleration = Vector3D.Lerp(myAcceleration * 60, oldMyAcceleration, 0.8);
            oldMyAcceleration = myAcceleration;

            if (!oldTarget.IsEmpty())
            {
                Vector3D targetAcceleration = Vector3D.Lerp((target.Velocity - oldTarget.Velocity) * 60, oldTargetAcceleration, 0.8);
                oldTargetAcceleration = targetAcceleration;

                tAcceleration += targetAcceleration;
            }

            var intersection = SimulateTrajectory(target, tAcceleration, cockpitPosition, velocity, myAcceleration, _projectileVelocity);
            if (intersection.HasValue)
            {
                if (autoAim != null)
                {
                    var dist = intersection.Value - cockpitPosition;
                    var direction = Vector3D.Normalize(dist);

                    autoAim.SetTargetDirection(Vector3D.Normalize(direction));
                }
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

        public Vector3D? SimulateTrajectory(MyDetectedEntityInfo target, Vector3D targetAccel, Vector3D myPosition, Vector3D myVelocity, Vector3D myAcceleration, double projectileSpeed)
        {
            double ticksUntilShoot = 2;

            double stepTime = 1.0 / 60.0;
            double dt = stepTime * ticksUntilShoot;

            Vector3D predMyPos = myPosition + myVelocity * dt + 0.5*myAcceleration*dt*dt;

            Vector3D relVelocity = target.Velocity - myVelocity;
            Vector3D D = target.Position - predMyPos;

            double a = relVelocity.LengthSquared() - projectileSpeed * projectileSpeed;
            double b = 2 * Vector3D.Dot(D, relVelocity);
            double c = D.LengthSquared();

            // Solve quadratic (since no acceleration is assumed)
            double? t = FastRootSolver.SolveQuadraticFast(a, b, c);
            if (!t.HasValue || t.Value < 0)
            return null;

            double time = t.Value + (dt * ticksUntilShoot);
            Vector3D futureTargetPosition = target.Position + relVelocity * time + targetAccel * 0.5 * time * time;
            return futureTargetPosition;
        }
    }
}