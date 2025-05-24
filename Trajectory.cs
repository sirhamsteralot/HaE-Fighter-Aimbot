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
        public class Trajectory
        {
            private Vector3D _projectileVelocity;
            private Vector3D _myVelocity;
            private Vector3D _myPosition;
            private Vector3D _myDirection;
            private double _projectileMaxVelocity;
            private const double AimTimeConstant = 0.2;

            public Trajectory(Vector3D MyVelocity, Vector3D MyPosition, Vector3D MyDirection, double ProjectileVelocity)
            {
                _myVelocity = MyVelocity;
                _myPosition = MyPosition;
                _myDirection = Vector3D.Normalize(MyDirection);
                _projectileMaxVelocity = ProjectileVelocity;
            }

            public Vector3D? SimulateTrajectory(MyDetectedEntityInfo target, Vector3D acceleration)
            {
                Vector3D relVelocity = target.Velocity - _myVelocity;
                Vector3D P0 = target.Position;
                Vector3D V0 = relVelocity;
                double speedTarget = V0.Length();

                if (speedTarget < 0.01)
                    return P0;

                Vector3D P1 = _myPosition;
                double projectileSpeed = _projectileMaxVelocity;

                // Coefficients for the quartic equation: A*t^4 + B*t^3 + C*t^2 + D*t + E = 0
                // Derived from the distance equation |P_target(t) - P1| = projectileSpeed * t

                Vector3D D = P0 - P1;
                Vector3D A = 0.5 * acceleration;

                double a = 0.25 * acceleration.LengthSquared();                           // (1/2 a)^2
                double b = Vector3D.Dot(acceleration, V0);                               // a · v
                double c = V0.LengthSquared() + Vector3D.Dot(acceleration, D) - projectileSpeed * projectileSpeed;
                double d = 2 * Vector3D.Dot(V0, D);
                double e = D.LengthSquared();

                // Solve the quartic equation: a*t^4 + b*t^3 + c*t^2 + d*t + e = 0
                double? t = FastRootSolver.SolveQuarticFast(a, b, c, d, e);

                if (t == null)
                    return null;

                double time = t.Value + AimTimeConstant;
                Vector3D futureTargetPosition = P0 + V0 * time + 0.5 * acceleration * time * time;

                return futureTargetPosition;
            }


            private double Quadratic(double value)
            {
                return value * value;
            }

            private Vector3D Quadratic(Vector3D vector)
            {
                return vector * vector;
            }

            private Vector3D Sqrt(Vector3D vector)
            {
                return new Vector3D(Math.Sqrt(vector.X), Math.Sqrt(vector.Y), Math.Sqrt(vector.Z));
            }

            private double Angle(Vector3D one, Vector3D two)
            {
                return Math.Acos(Vector3D.Dot(one, two) / (one.Length() * two.Length()));
            }
        }
    }
}
