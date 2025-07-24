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
        public static class FastRootSolver
        {
            public static double? SolveQuarticFast(double a, double b, double c, double d, double e, int maxIterations = 20)
            {
                if (Math.Abs(a) < 1e-8)
                {
                    return SolveCubicFast(b, c, d, e);
                }

                double[] guesses = new double[] { 0.1, 0.5, 1.0, 2.0 };
                double bestRoot = double.PositiveInfinity;

                for (int i = 0; i < guesses.Length; i++)
                {
                    double? root = NewtonRaphsonQuartic(a, b, c, d, e, guesses[i], maxIterations);
                    if (root.HasValue && root.Value > 0 && root.Value < bestRoot)
                    {
                        bestRoot = root.Value;
                    }
                }

                return double.IsInfinity(bestRoot) ? (double?)null : bestRoot;
            }

            private static double? NewtonRaphsonQuartic(double a, double b, double c, double d, double e, double x0, int maxIterations)
            {
                double x = x0;
                for (int i = 0; i < maxIterations; i++)
                {
                    double fx = ((a * x + b) * x + c) * x * x + d * x + e;
                    double fpx = (4 * a * x + 3 * b) * x * x + 2 * c * x + d;

                    if (Math.Abs(fpx) < 1e-8)
                        break;

                    double xNew = x - fx / fpx;
                    if (Math.Abs(xNew - x) < 1e-6)
                        return xNew;

                    x = xNew;
                }

                return x > 0 ? (double?)x : null;
            }

            private static double? SolveCubicFast(double a, double b, double c, double d)
            {
                if (Math.Abs(a) < 1e-8)
                {
                    return SolveQuadraticFast(b, c, d);
                }

                b /= a;
                c /= a;
                d /= a;

                double q = (3.0 * c - b * b) / 9.0;
                double r = (9.0 * b * c - 27.0 * d - 2.0 * b * b * b) / 54.0;
                double discriminant = q * q * q + r * r;

                if (discriminant > 0)
                {
                    double sqrtD = Math.Sqrt(discriminant);
                    double s = CubeRoot(r + sqrtD);
                    double t = CubeRoot(r - sqrtD);
                    double root = -b / 3.0 + (s + t);
                    return root > 0 ? (double?)root : null;
                }
                else
                {
                    double theta = Math.Acos(r / Math.Sqrt(-q * q * q));
                    double sqrtQ = Math.Sqrt(-q);

                    for (int k = 0; k < 3; k++)
                    {
                        double angle = (theta + 2 * Math.PI * k) / 3;
                        double root = 2 * sqrtQ * Math.Cos(angle) - b / 3.0;
                        if (root > 0)
                            return root;
                    }
                }

                return null;
            }

            public static double? SolveQuadraticFast(double a, double b, double c)
            {
                if (Math.Abs(a) < 1e-8)
                {
                    if (Math.Abs(b) > 1e-8)
                        return -c / b;
                    return null;
                }

                double discriminant = b * b - 4 * a * c;
                if (discriminant < 0)
                    return null;

                double sqrtD = Math.Sqrt(discriminant);
                double t1 = (-b + sqrtD) / (2 * a);
                double t2 = (-b - sqrtD) / (2 * a);

                bool t1Valid = t1 > 0;
                bool t2Valid = t2 > 0;

                if (t1Valid && t2Valid)
                    return Math.Min(t1, t2);
                if (t1Valid)
                    return t1;
                if (t2Valid)
                    return t2;

                return null;
            }

            private static double CubeRoot(double x)
            {
                return x >= 0 ? Math.Pow(x, 1.0 / 3.0) : -Math.Pow(-x, 1.0 / 3.0);
            }
        }
    }
}