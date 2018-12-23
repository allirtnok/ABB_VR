using System;
using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio.Stations;
using System.Linq;
using RobotStudio.API.Internal;
using ABB.Robotics.RobotStudio.Stations.Forms;
using ABB.Robotics.RobotStudio.Environment;
using System.Collections.Generic;

// Not yet used, review

namespace VrPaintAddin
{
    internal class MagneticSnap
    {
        public static double SnapCenterRadius { get; set; } = 0.05;
        static double SnapEaseRadius => SnapCenterRadius * 1.7;

        internal static Matrix4 ModifyTransform(Matrix4 pointerTransform)
        {
            var snaps = GetSnapTransforms(pointerTransform);
            var snapsWithDistance = snaps.Select(snap => new
            {
                Distance = (snap - pointerTransform.Translation).Length(),
                SnapPoint = snap
            }).ToList();
            snapsWithDistance.Reverse();

            Matrix4 result = pointerTransform;

            foreach (var snap in snapsWithDistance)
            {
                if (snap.Distance < SnapCenterRadius)
                {
                    result.Translation = snap.SnapPoint;
                }
                else if (snap.Distance < SnapEaseRadius)
                {
                    double t = 1 - (snap.Distance - SnapCenterRadius) / (SnapEaseRadius - SnapCenterRadius);
                    result.Translation += t * (snap.SnapPoint - result.Translation);
                }
            }
            return result;

        }

        // Maybe even better if we could do this based on distance only, and not pointing.
        static List<Vector3> GetSnapTransforms(Matrix4 pointerTransform)
        {
            var gc = (GraphicControl)UIEnvironment.Windows.FirstOrDefault(w => w.Control is GraphicControl)?.Control;
            if (gc == null || gc.IsDisposed) return new List<Vector3> { Vector3.ZeroVector };

            var result = new List<Vector3>();

            var pickManager = new PickManager();

            PickRay ray = new PickRay
            {
                Ray = new Ray
                {
                    origin = pointerTransform.Translation,
                    direction = pointerTransform.UpperLeft.z
                }
            };

            PickData res = new PickData();
            // Ordered by priority
            var snapModes = new[] { SnapMode.Snap, SnapMode.Edge };
            foreach (var snapMode in snapModes)
            {
                pickManager.SnapMode = snapMode;
                pickManager.PickOneObject(gc, ray, true, out res);
                result.Add(res.snapPos);
            }
            // Add the raw hitpoint as well as the least prioritized point
            result.Add(res.rawPos);

            return result;
        }
    }
}