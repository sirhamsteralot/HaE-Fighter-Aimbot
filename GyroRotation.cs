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
            private IMyShipController cockpit;
            private PIDController yawPID = new PIDController(10, 0.0, 0.0);
            private PIDController pitchPID = new PIDController(10, 0.0, 0.0);

            public Vector3D target;
            public GyroRotation(Program P, IMyShipController cockpit)
            {
                P.GridTerminalSystem.GetBlocksOfType(gyros);
                this.cockpit = cockpit;
            }

            public void MainRotate(long currentPbTime)
            {
                if (target != null)
                {
                    Rotate(currentPbTime);
                }
            }

            private void Rotate(long currentPbTime)
            {
                if (target != Vector3D.Zero)
                    AimInDirection(target, currentPbTime);
            }

            public void SetTargetDirection(Vector3D NewTarget)
            {
                if (!target.Equals(NewTarget))
                {
                    target = NewTarget;
                }
            }

            private void AimInDirection(Vector3D aimDirection, long currentPbTime)
            {
                var refMatrix = cockpit.WorldMatrix;
                double t = currentPbTime * 1.0/60.0;
                Vector3D forward = refMatrix.Forward;
                Vector3D up = refMatrix.Up;
                Vector3D left = refMatrix.Left;

                Vector3D PitchVector = SafeNormalize(VectorUtils.ProjectOnPlane(left, aimDirection));
                Vector3D YawVector = SafeNormalize(VectorUtils.ProjectOnPlane(up, aimDirection));

                double pitchAngle = VectorUtils.SignedAngle(PitchVector, forward, left);
                double yawAngle = VectorUtils.SignedAngle(YawVector, forward, up);
                double pitchInput = pitchPID.Compute(pitchAngle, t);
                double yawInput = yawPID.Compute(yawAngle, t);

                GyroUtils.ApplyGyroOverride(gyros, refMatrix, pitchInput, yawInput, 0);
            }

            public static Vector3D SafeNormalize(Vector3D v, double tolerance = 1e-8)
            {
                return v.LengthSquared() > tolerance ? Vector3D.Normalize(v) : Vector3D.Zero;
            }


            public void Dispose()
            {
                foreach (IMyGyro gyro in gyros)
                    gyro.GyroOverride = false;
            }
        }
    }
}
