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

            public Trajectory(Vector3D MyVelocity, Vector3D MyPosition, Vector3D MyDirection, double ProjectileVelocity)
            {
                _myVelocity = MyVelocity;
                _myPosition = MyPosition;
                _myDirection = Vector3D.Normalize(MyDirection);
                _projectileMaxVelocity = ProjectileVelocity;
            }

            public Vector3D? SimulateTrajectory(MyDetectedEntityInfo target)
            {
                var relVel = target.Velocity - _myVelocity;
                var P0 = target.Position;
                var V0 = Vector3D.Normalize(relVel);
                var s0 = relVel.Length();

                if (s0 < 0.01)
                    return P0;

                var P1 = _myPosition;
                var s1 = _projectileMaxVelocity;

                //Yo dawg i heard you liked obscure calculations.
                var a = (V0.X * V0.X) + (V0.Y * V0.Y) + (V0.Z * V0.Z) - (s1 * s1);
                var b = 2 * ((P0.X * V0.X) + (P0.Y * V0.Y) + (P0.Z * V0.Z) - (P1.X * V0.X) - (P1.Y * V0.Y) - (P1.Z * V0.Z));
                var c = (P0.X * P0.X) + (P0.Y * P0.Y) + (P0.Z * P0.Z) + (P1.X * P1.X) + (P1.Y * P1.Y) + (P1.Z * P1.Z) - (2 * P1.X * P0.X) - (2 * P1.Y * P0.Y) - (2 * P1.Z * P0.Z);

                var t1 = (-b + Math.Sqrt((b * b) - (4 * a * c))) / (2 * a);
                var t2 = (-b - Math.Sqrt((b * b) - (4 * a * c))) / (2 * a);

                var t = Math.Max(t1, t2);
                if (t < 0 || double.IsNaN(t))
                    return null;

                Vector3D V = P0 + V0 * s0 * t;

                return V;
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
