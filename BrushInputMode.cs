using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.API.Internal;
using RobotStudio.Services.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static VrPaintAddin.HelperFunctions;


namespace VrPaintAddin
{
    class BrushInputMode : VrInputMode
    {
        PathObjectDetector _detector;
        VrScrollSelector _brushSelector;

        RsMoveInstruction _trackedMoveInstruction;
        RsActionInstruction _trackedBrushInstruction;

        const double _brushHitRadius = 0.05;

        TemporaryGraphic _brushTempGfx;

        //:TODO: Where to get valid brush numbers from? Add name mapping somewhere?
        string[] _brushValues = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
        string _lastBrushNum = "1";


        public override void Activate(VrSession session)
        {
            _detector = new PathObjectDetector(Station.ActiveStation);
        }

        public override void Deactivate(VrSession session)
        {
            if (_brushSelector != null)
            {
                _brushSelector.Dispose();
                _brushSelector = null;
            }

            DeleteBrushTempGfx();
        }

        void DeleteBrushTempGfx()
        {
            if (_brushTempGfx != null)
            {
                _brushTempGfx.Delete();
                _brushTempGfx = null;
            }
        }

        public override void Update(VrUpdateArgs args)
        {
            var session = args.Session;
            DeleteBrushTempGfx();

            var input = session.SemanticInput();

            if (_trackedBrushInstruction != null)
            {
                if (input.IsSelectPressed)
                {
                    // Drag brush
                    var relatedMI = FindRelatedMoveInstruction(_trackedBrushInstruction);
                    if (relatedMI != null)
                    {
                        Matrix4 wobjMat = relatedMI.GetWorkObject().ObjectFrame.GlobalMatrix;
                        Vector3 point = wobjMat.InverseRigid().MultiplyPoint(session.RightController.PointerTransform.Translation);
                        var pos = PathEditingHelper.GetBrushEventPosition(_trackedBrushInstruction);
                        string val = ((int)(point[(int)pos.Item1 - 1] * 1000)).ToString();
                        if (val != pos.Item2.Value)
                        {
                            WithUndoAppend("VR Move SetBrush", () => { pos.Item2.Value = val; });
                        }
                        _brushSelector.Caption = _trackedBrushInstruction.DisplayName;
                    }
                    return;
                }
            }

            var hitObj = GetHitObject(session.RightController.PointerTransform.Translation);

            var trackedMoveInstruction = hitObj?.Item1 as RsMoveInstruction;
            if (trackedMoveInstruction != null)
            {
                //:TODO: Supported for PaintC??
                if (trackedMoveInstruction.Name != "PaintL" && trackedMoveInstruction.Name != "PaintC" ||
                    trackedMoveInstruction.GetToTarget() == null)
                {
                    trackedMoveInstruction = null;
                }
            }

            var trackedBrushInstr = hitObj?.Item1 as RsActionInstruction;
            if (trackedBrushInstr != null && !string.Equals(trackedBrushInstr.Name, "SetBrush", StringComparison.OrdinalIgnoreCase)) trackedBrushInstr = null;

            if (trackedMoveInstruction != _trackedMoveInstruction)
            {
                _trackedMoveInstruction = trackedMoveInstruction;
                if (_brushSelector == null)
                {
                    _brushSelector = new VrScrollSelector(session.RightController, "", _brushValues, _lastBrushNum);
                }
                _brushSelector.Caption = (_trackedMoveInstruction != null) ? "Add SetBrush" : "Brush Number";
            }
      
            else if (trackedBrushInstr != _trackedBrushInstruction || _brushSelector == null)
            {
                if (_brushSelector != null) _brushSelector.Dispose();

                _trackedBrushInstruction = trackedBrushInstr;
                if (_trackedBrushInstruction != null)
                {
                    var numarg = _trackedBrushInstruction.InstructionArguments["BrushNumber"];
                    _brushSelector = new VrScrollSelector(session.RightController, _trackedBrushInstruction.DisplayName, _brushValues, numarg.Value);
                }
                else
                {
                    _brushSelector = new VrScrollSelector(session.RightController, (_trackedMoveInstruction != null) ? "Add SetBrush" : "Brush Number", _brushValues, _lastBrushNum);
                }
            }

            if (_trackedBrushInstruction != null && input.DeleteClick)
            {
                var path = (RsPathProcedure)_trackedBrushInstruction.Parent;
                WithUndo("VR Delete SetBrush", () => { path.Instructions.Remove(_trackedBrushInstruction); });
                _trackedBrushInstruction = null;
                return;
            }

            bool brushNumUpdated = _brushSelector.Update();
            if (brushNumUpdated) _lastBrushNum = _brushSelector.SelectedValue;

            if (_trackedMoveInstruction != null && _trackedBrushInstruction == null)
            {
                Matrix4 wobjMat = _trackedMoveInstruction.GetWorkObject().ObjectFrame.GlobalMatrix;

                CreateBrushPlanePreview(hitObj.Item2, wobjMat);

                if (input.SelectClick)
                {
                    Vector3 point = wobjMat.InverseRigid().MultiplyPoint(hitObj.Item2);
                    var axis = ComputeBrushAxis();
                    int val = (int)(point[(int)axis - 1] * 1000);
                    WithUndo("VR Create SetBrush", () =>
                        {
                            PathEditingHelper.CreateSetBrush(
                                _trackedMoveInstruction,
                                axis,
                                val,
                                _brushSelector.SelectedValue);
                        }
                        );
                    DeleteBrushTempGfx();
                }
            }
            else if (_trackedBrushInstruction != null && brushNumUpdated)
            {
                var numarg = _trackedBrushInstruction.InstructionArguments["BrushNumber"];
                WithUndo("VR Edit SetBrush", () => { numarg.Value = _brushSelector.SelectedValue; });
                _brushSelector.Caption = _trackedBrushInstruction.DisplayName;
            }
        }

        Tuple<ProjectObject, Vector3> GetHitObject(Vector3 point)
        {
            var sbHitResult = _detector.DetectSetBrushInstruction(point) as HitResult<RsActionInstruction>;
            if (sbHitResult != null) return Tuple.Create((ProjectObject)sbHitResult.HitObject, sbHitResult.HitPoint);

            var hitResult = _detector.DetectPathSegment(point) as HitResult<PathSegment>;
            if (hitResult != null && hitResult.HitObject.MoveInstruction != null)
            {
                return Tuple.Create((ProjectObject)hitResult.HitObject.MoveInstruction, hitResult.HitPoint);
            }

            return null;
        }

        Axis ComputeBrushAxis()
        {
            var path = (RsPathProcedure)_trackedMoveInstruction.Parent;
            int idx = path.Instructions.IndexOf(_trackedMoveInstruction);
            var prevInstr = path.Instructions.Where((mi, i) => i < idx).OfType<RsMoveInstruction>().Where(m => m.GetToTarget() != null).LastOrDefault();
            var p1 = prevInstr.GetToTarget().Transform.Matrix.Translation;
            var p2 = _trackedMoveInstruction.GetToTarget().Transform.Matrix.Translation;
            var dir = p2 - p1;
            double x = Math.Abs(dir.x);
            double y = Math.Abs(dir.y);
            double z = Math.Abs(dir.z);
            var plane = Axis.X;
            if (y > x) plane = Axis.Y;
            if (z > x && z > y) plane = Axis.Z;
            return plane;
        }

        void CreateBrushPlanePreview(Vector3 point, Matrix4 wobjMat)
        {
            var axis = ComputeBrushAxis();

            Vector3 v1, v2;
            switch (axis)
            {
                case Axis.X: v1 = wobjMat.GetAxisVector(Axis.Y); v2 = wobjMat.GetAxisVector(Axis.Z); break;
                case Axis.Y: v1 = wobjMat.GetAxisVector(Axis.Z); v2 = wobjMat.GetAxisVector(Axis.X); break;
                case Axis.Z: v1 = wobjMat.GetAxisVector(Axis.X); v2 = wobjMat.GetAxisVector(Axis.Y); break;
                default: throw new Exception();
            }

            double size = 0.05;
            v1 = v1 * size; v2 = v2 * size;
            point = point - v1 * 0.5 - v2 * 0.5;

            var corners = new Vector3[] { point, point + v1, point + v1 + v2, point + v2, point };
            _brushTempGfx = Station.ActiveStation.TemporaryGraphics.DrawLineStrip(corners, 1, System.Drawing.Color.FromArgb(100, 255, 255, 100));
        }

        RsMoveInstruction FindRelatedMoveInstruction(RsActionInstruction setBrushInstr)
        {
            var path = (RsPathProcedure)setBrushInstr.Parent;
            int idx = path.Instructions.IndexOf(setBrushInstr);
            return path.Instructions.Where((mi, i) => i > idx).OfType<RsMoveInstruction>().Where(m => m.GetToTarget() != null).FirstOrDefault();
        }
    }
}
