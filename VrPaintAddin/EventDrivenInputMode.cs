using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using RobotStudio.API.Internal;
using ABB.Robotics.RobotStudio.Stations.Forms;

namespace VrPaintAddin
{
    //:TODO: Separate selection stuff from controller input state management
    // Also, use the new 'PreviousInputState' property on controller where appropriate

    internal class EventDrivenInputMode : VrInputMode
    {
        public static double MaxClickDistance { get; set; } = 0.05;
        bool _dragging;
        //Matrix4 _pressedAt;
        IHitResult _hitResult; // needed any longer?

        public static bool Snap { get; set; }

        // Things that don't belong here...
        PathObjectDetector _objectDetector;

        protected event VrAlternateClickEventHandler AlternateClick; //:TODO: Rename to match SemanticInput (DeleteClick?)
        protected event VrBeginDragEventHandler BeginDrag;
        protected event VrDeltaDragEventHandler DeltaDrag;
        protected event VrEndDragEventHandler EndDrag;
        protected event VrHoverEventHandler HoverObject;

        public override void Activate(VrSession session)
        {
            _objectDetector = new PathObjectDetector(Station.ActiveStation);
        }

        public override void Deactivate(VrSession session)
        {
            _objectDetector = null;
        }

        public override void Update(VrUpdateArgs args)
        {
            var session = args.Session;
            var input = session.SemanticInput();

            Matrix4 pointer = PointerFilter(session.RightController.PointerTransform);
            var hitResult = GetClosestHit(pointer.Translation);

            //KNARK: Review Deletebutton usage (used to capture on down, execute on up)

            /*if(input.SelectClick || input.DeleteClick)
            {
                _pressedAt = pointer;
            }
            else */if (input.IsSelectPressed && !_dragging)
            {
                _dragging = true;
                _hitResult = hitResult;

                var eventArgs = new VrEventArgs(pointer, _hitResult);
                BeginDrag?.Invoke(eventArgs);
                if (eventArgs.CreatedObject != null)
                {
                    _hitResult = eventArgs.CreatedObject();
                }
            }
            else if (_dragging && input.IsSelectPressed)
            {
                DeltaDrag?.Invoke(new VrEventArgs(pointer, _hitResult));
            }
            else if (!input.IsSelectPressed && _dragging)
            {
                _dragging = false;
                EndDrag?.Invoke(new VrEventArgs(pointer, _hitResult));
            }
            //else if (_gripPressed && !session.RightController.InputState.GripPressed
            //    && CloseEnough(_pressedAt, pointer))
            else if(input.DeleteClick)
            {
                AlternateClick?.Invoke(new VrEventArgs(pointer, hitResult));
            }
            else if (hitResult != null)
            {
                HoverObject?.Invoke(new VrEventArgs(pointer, hitResult));
            }
        }

        Matrix4 PointerFilter(Matrix4 pointerTransform)
        {
            if (Snap) return MagneticSnap.ModifyTransform(pointerTransform);
            else return pointerTransform;
        }

        bool CloseEnough(Matrix4 mat1, Matrix4 mat2)
        {
            return mat1.Translation.SquareDistance(mat2.Translation) < MaxClickDistance * MaxClickDistance;
        }

        //:TODO: This will cause multiple traversals of the paths [perf]. Review.
        IHitResult GetClosestHit(Vector3 testPoint)
        {
            var closestMoveInstruction = _objectDetector.DetectMoveInstruction(testPoint);
            if (closestMoveInstruction != null) return closestMoveInstruction;
            var closestPathSegment = _objectDetector.DetectPathSegment(testPoint);
            if (closestPathSegment != null) return closestPathSegment;
            return null;
        }

    }

    delegate void VrClickEventHandler(VrEventArgs args);
    delegate void VrAlternateClickEventHandler(VrEventArgs args);
    delegate void VrBeginDragEventHandler(VrEventArgs args);
    delegate void VrDeltaDragEventHandler(VrEventArgs args);
    delegate void VrEndDragEventHandler(VrEventArgs args);
    delegate void VrHoverEventHandler(VrEventArgs args);

    internal class VrEventArgs
    {
        public VrEventArgs(Matrix4 frame, IHitResult hitResult)
        {
            Frame = frame;
            HitResult = hitResult;
        }
        public Matrix4 Frame { get; }
        public IHitResult HitResult { get; }
        public Func<IHitResult> CreatedObject { get; set; }
    }


}
