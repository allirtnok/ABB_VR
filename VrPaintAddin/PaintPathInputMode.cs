using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.API.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using static VrPaintAddin.HelperFunctions;

namespace VrPaintAddin
{
    public class PaintPathInputMode : VrInputMode
    {
        PathBuilder _pathBuilder;
        List<TemporaryGraphic> _toolGfx = new List<TemporaryGraphic>();
        TemporaryGraphic _previewTrace;
        Matrix4 _attachOffset;

        public PaintPathInputMode()
        {
        }

        public override void Activate(VrSession session)
        {
            PathEditingHelper.EnsurePath();

            //:TODO: Update if active tool changes
            CreateToolGraphics();
        }

        void CreateToolGraphics()
        {
            var tooldata = Station.ActiveStation.ActiveTask.ActiveTool;

            //:TODO: Find a better way of selecting tool
            var toolMech = Station.ActiveStation.FindGraphicComponentsByType(typeof(Mechanism))
                .Cast<Mechanism>()
                .Where(m => m.MechanismType == MechanismType.Tool)
                .Where(m => m.GetToolDataInfo().Any(t => t.Name == tooldata.Name))
                .FirstOrDefault();

            // The TCP should appear 120 mm ahead of the controller.
            Matrix4 paintToolOffset = Matrix4.Identity;
            paintToolOffset.Translation = new Vector3(0, 0, .120);

            _attachOffset = VrEnvironment.Session.RightController.PointerOffsetTransform * paintToolOffset * tooldata.Frame.Matrix.InverseRigid();
            if (!_attachOffset.IsRigid()) throw new InvalidOperationException();

            if (toolMech != null)
            {
                var toolParts = toolMech.Descendants(true).OfType<Part>();
                foreach (var toolPart in toolParts)
                {
                    var tf = toolPart.Transform.GetRelativeTransform(toolMech);
                    _toolGfx.Add(VrEnvironment.Session.RightController.TemporaryGraphics.DrawPart(_attachOffset * tf, toolPart));
                }
            }

            _toolGfx.Add(VrEnvironment.Session.RightController.TemporaryGraphics.DrawFrame(_attachOffset * tooldata.Frame.Matrix, 0.01, 1));
        }

        public override void Deactivate(VrSession session)
        {
            foreach (var g in _toolGfx) g.Delete();
            if (_previewTrace != null) _previewTrace.Delete();
            _toolGfx.Clear();
        }

        public override void Update(VrUpdateArgs args)
        {
            var session = args.Session;
            if (session.SemanticInput().IsSelectPressed)
            {
                var transform = session.RightController.Transform;
                transform.CleanRigid(); //:TODO: Should not be needed. Review
                transform = transform * _attachOffset * Station.ActiveStation.ActiveTask.ActiveTool.Frame.Matrix;

                if (_pathBuilder == null)
                {
                    _pathBuilder = new PathBuilder(Station.ActiveStation);
                    _previewTrace = Station.ActiveStation.TemporaryGraphics.DrawTrace(transform.Translation, 1, 100);
                }
                else
                {
                    _previewTrace.ContinueTrace(transform.Translation, System.Drawing.Color.LightYellow, false);
                }

                session.RightController.HapticFeedback(1);
                _pathBuilder.AddTransform(transform);
            }
            else
            {
                if (_pathBuilder != null)
                {
                    _pathBuilder.Finish();
                    _pathBuilder = null;

                    _previewTrace.Delete();
                    _previewTrace = null;
                }
            }
        }
    }

    public class PathBuilder
    {
        Station _stn;
        List<Matrix4> _transforms = new List<Matrix4>();
        RsPathProcedure _path;

        public PathBuilder(Station stn)
        {
            _stn = stn;
        }

        public void AddTransform(Matrix4 transform)
        {
            _transforms.Add(transform);
        }

        public void Finish()
        {
            WithUndo("VR Paint", () =>
            {
                _path = _stn.ActiveTask.ActivePathProcedure;

                var transforms = ReduceTransforms(_transforms);
                foreach (var tf in transforms)
                {
                    string instrName = _stn.ActiveTask.GetValidRapidName("VrTarget", "_", 1);
                    CreateMoveInstruction(tf, instrName);
                }

                _stn.ActiveTask.ActivePathProcedure = _path;
            });
        }

        void CreateMoveInstruction(Matrix4 tf, string name)
        {
            var moveInstr = PathEditingHelper.CreateNewInstructionInActivePath(tf);
        }

        // Simple greedy point reduction - removes intermediate transforms if the resulting trans/rot error is small enough
        // Supports linear motion only.
        IEnumerable<Matrix4> ReduceTransforms(IList<Matrix4> transforms)
        {
            if (transforms.Count < 2) yield break;

            yield return transforms[0];
            int prevIdx = 0;
            for (int i = 2; i < transforms.Count; i++)
            {
                for (int j = prevIdx + 1; j < i; j++)
                {
                    bool ok = CheckDeviation(transforms[prevIdx], transforms[i], transforms[j]);
                    if (!ok)
                    {
                        yield return transforms[i - 1];
                        prevIdx = i - 1;
                        i++;
                        break; //j
                    }
                }
            }
            if (prevIdx != transforms.Count - 1) yield return transforms.Last();
        }

        bool CheckDeviation(Matrix4 start, Matrix4 end, Matrix4 mid)
        {
            const double maxLinDev = 0.008;
            //const double maxRotDev = 0.1;

            // Linear
            double p;
            if (DistanceToSegment(start.Translation, end.Translation, mid.Translation, out p) > maxLinDev) return false;

            // Rotational
            //var slerpQuat = start.Quaternion.Interpolate(end.Quaternion, p);
            //double dev = 1 - (slerpQuat.Dot(mid.Quaternion));
            //if (dev > maxRotDev) return false;

            return true;
        }

        double DistanceToSegment(Vector3 start, Vector3 end, Vector3 mid, out double p)
        {
            double distance;

            Vector3 vec = end - start;
            Vector3 vecP = mid - start;

            double lvec = vec.Length();
            vec.Normalize();

            double sca = vecP.Dot(vec);
            if (sca < 0)
            {
                distance = vecP.Length();
                p = 0;
            }
            else if (sca > lvec)
            {
                distance = (mid - end).Length();
                p = 1;
            }
            else
            {
                Vector3 Pt = start + vec * sca;
                distance = (mid - Pt).Length();
                p = sca / lvec;
            }

            return distance;
        }
    }
}
