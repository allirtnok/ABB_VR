using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;

using RobotStudio.API.Internal;
using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.Services.Graphics;

using static VrPaintAddin.HelperFunctions;

namespace VrPaintAddin
{
    internal sealed class PathEditingInputMode : EventDrivenInputMode
    {
        TemporaryGraphic _frameGfx;
        IntPtr _pointerToSelGfx;
        //TemporaryGraphic _hoverGfx;
        RsMoveInstruction _newInstruction;
        Matrix4 _dragOffset;

        VrController _controller;

        // TEST - must cleanup implementation
        readonly static Color _highlightColor = Color.FromArgb(128, 255, 255, 255);
        RsTarget _highlightTarget;
        RsTarget _newHighlightTarget; // transient, TODO: Fix base class hover api instead
        bool _forceUpdateLine;


        public override void Activate(VrSession session)
        {
            base.Activate(session);

            PathEditingHelper.EnsurePath();

            _controller = session.RightController;
            AddGfx();
            //Click += HandleClick;
            AlternateClick += HandleAlternateClick;
            DeltaDrag += HandleDeltaDrag;
            BeginDrag += HandleBeginDrag;
            EndDrag += HandleDeltaDrag;
            HoverObject += HandleHover;
        }


        public override void Deactivate(VrSession session)
        {
            base.Deactivate(session);
            DeleteGfx();
            _controller = null;
            //Click -= HandleClick;
            AlternateClick -= HandleAlternateClick;
            DeltaDrag -= HandleDeltaDrag;
            BeginDrag -= HandleBeginDrag;
            EndDrag -= HandleEndDrag;
            
            //:TODO:
            //view.OpenVRRemoveAttachedLine(_pointerToSelGfx);

            ClearHover();

            if(_pointerToSelGfx != IntPtr.Zero)
            {
                var view = (GraphicView)GraphicControl.ActiveGraphicControl.GetView();
                view.OpenVRRemoveAttachedLine(_pointerToSelGfx);
                _pointerToSelGfx = IntPtr.Zero;
            }
        }

        private void ClearHover()
        {
            if (_highlightTarget != null) { _highlightTarget.ResetHighlight(); _highlightTarget = null; }
        }

        public override void Update(VrUpdateArgs args)
        {
            var session = args.Session;

            _newHighlightTarget = null;
            _forceUpdateLine = false;

            base.Update(args);

            var view = (GraphicView)GraphicControl.ActiveGraphicControl.GetView();

            if (_newHighlightTarget != _highlightTarget)
            {
                ClearHover();
                _highlightTarget = _newHighlightTarget;
                _newHighlightTarget = null;
                _highlightTarget?.Highlight(System.Drawing.Color.FromArgb(128, 255, 255, 255));

                if (_highlightTarget != null)
                {
                    _frameGfx.Visible = false;
                    _pointerToSelGfx = view.OpenVRAddAttachedLine(
                        VrAttachedTo.RightController, session.RightController.PointerOffsetTransform.Translation, System.Drawing.Color.White,
                        VrAttachedTo.Station, _highlightTarget.Transform.GlobalMatrix.Translation, System.Drawing.Color.Orange);
                }
                else
                {
                    _frameGfx.Visible = true;
                    if (_pointerToSelGfx != IntPtr.Zero) view.OpenVRRemoveAttachedLine(_pointerToSelGfx);
                    _pointerToSelGfx = IntPtr.Zero;
                }
            }
            else if(_forceUpdateLine && _highlightTarget != null)
            {
                view.OpenVRRemoveAttachedLine(_pointerToSelGfx);
                _pointerToSelGfx = view.OpenVRAddAttachedLine(
                    VrAttachedTo.RightController, session.RightController.PointerOffsetTransform.Translation, System.Drawing.Color.White,
                    VrAttachedTo.Station, _highlightTarget.Transform.GlobalMatrix.Translation, System.Drawing.Color.White);
            }
        }


        void HandleBeginDrag(VrEventArgs args)
        {
            _frameGfx.Visible = false;

            var hitInstruction = args.HitResult as HitResult<RsMoveInstruction>;
            var hitSegment = args.HitResult as HitResult<PathSegment>;
            if (hitInstruction != null)
            {
                _dragOffset = args.Frame.InverseRigid() * hitInstruction.HitObject.GetToTarget().Transform.GlobalMatrix;
                WithUndo("VR Drag", () => PathEditingHelper.MoveInstructionToFrame(hitInstruction.HitObject, args.Frame * _dragOffset));
                _newHighlightTarget = hitInstruction?.HitObject?.GetToTarget();
            }
            else if (hitSegment != null)
            {
                WithUndo("VR Drag", () => _newInstruction = PathEditingHelper.InsertMoveInstruction(hitSegment.HitObject, args.Frame));
            }
            else if (args.HitResult != null)
            {
                throw new InvalidOperationException("unrecognized HitResult");
            }
            else
            {
                WithUndo("VR Create Instruction", () => _newInstruction = PathEditingHelper.CreateNewInstructionInActivePath(args.Frame));
                _dragOffset = Matrix4.Identity;
                args.CreatedObject = () => new HitResult<RsMoveInstruction>(_newInstruction, 0, args.Frame.Translation);
            }
        }

        void HandleDeltaDrag(VrEventArgs args)
        {
            //_frameGfx.Visible = false;

            var hitInstruction = args.HitResult as HitResult<RsMoveInstruction>;
            var hitSegment = args.HitResult as HitResult<PathSegment>;
            if (hitInstruction != null)
            {
                WithUndoAppend("VR Drag", () => PathEditingHelper.MoveInstructionToFrame(hitInstruction.HitObject, args.Frame * _dragOffset));
                _newHighlightTarget = hitInstruction?.HitObject?.GetToTarget();
                _forceUpdateLine = true;
            }
            else if (hitSegment != null)
            {
                WithUndoAppend("VR Drag", () => PathEditingHelper.MoveInstructionToFrame(_newInstruction, args.Frame));
            }
            else if (args.HitResult != null)
            {
                throw new InvalidOperationException("unrecognized HitResult");
            }
        }

        void HandleEndDrag(VrEventArgs args)
        {
            HandleDeltaDrag(args);
            _frameGfx.Visible = true;
        }

        void HandleAlternateClick(VrEventArgs args)
        {
            var hitInstruction = args.HitResult as HitResult<RsMoveInstruction>;
            if (hitInstruction != null)
            {
                WithUndo("VR Delete", () => PathEditingHelper.DeleteInstruction(hitInstruction.HitObject));
            }
        }

        void HandleHover(VrEventArgs args)
        {
            var hitInstruction = args.HitResult as HitResult<RsMoveInstruction>;
            _newHighlightTarget = hitInstruction?.HitObject?.GetToTarget();
        }

        void AddGfx()
        {
            _frameGfx = _controller.TemporaryGraphics.DrawFrame(
                _controller.PointerOffsetTransform,
                .1,
                2);
        }

        void DeleteGfx()
        {
            _frameGfx.Delete();
        }
    }
}