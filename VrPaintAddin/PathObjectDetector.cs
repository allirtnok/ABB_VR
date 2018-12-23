using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.API.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//:TODO: Requires multiple passes to detect multiple object types [perf]
namespace VrPaintAddin
{
    class PathObjectDetector // IObjectDetector?
    {
        double MaximumDetectionDistance => 0.05;
        readonly Station _station;

        // TODO: Invalidate cache
        Dictionary<RsMoveInstruction, RsTarget> _targets = new Dictionary<RsMoveInstruction, RsTarget>();
        RsTarget GetTarget(RsMoveInstruction instruction)
        {
            RsTarget target;
            if (!_targets.TryGetValue(instruction, out target))
            {
                target = instruction.GetToTarget();
                _targets[instruction] = target;
            }
            return target;
        }

        IEnumerable<RsPathProcedure> AllPaths => _station.Irc5Controllers
                .SelectMany(c => c.Tasks)
                .SelectMany(t => t.PathProcedures)
                .Concat(_station.DefaultTask.PathProcedures);

        public PathObjectDetector(Station station)
        {
            _station = station;
        }

        // For testing
        public bool FoundPathBox(Vector3 testPoint)
        {
            return AllPaths.Where(p => BoxCull(p, testPoint)).Any();
        }

        public IHitResult DetectMoveInstruction(Vector3 testPoint)
        {
            var instructions = AllPaths
                .Where(p => BoxCull(p, testPoint))
                .SelectMany(p => p.Instructions)
                .OfType<RsMoveInstruction>();
            double minSquareDistance = double.PositiveInfinity;
            RsMoveInstruction closestInstruction = null;
            foreach (var instruction in instructions)
            {
                var toRobTarget = GetTarget(instruction);
                // If it is a joint target.
                if (toRobTarget == null) continue;
                var p = toRobTarget.GetGfx().GlobalMatrix.Translation;
                double distance = p.SquareDistance(testPoint);
                if (distance < minSquareDistance)
                {
                    minSquareDistance = distance;
                    closestInstruction = instruction;
                }
            }
            if (minSquareDistance < MaximumDetectionDistance * MaximumDetectionDistance)
            {
                return new HitResult<RsMoveInstruction>(
                    closestInstruction,
                    minSquareDistance,
                    GetTarget(closestInstruction).GetGfx().GlobalMatrix.Translation);
            }
            else
            {
                return null;
            }
        }

        public IHitResult DetectPathSegment(Vector3 testPoint)
        {
            double minSquareDistance = double.PositiveInfinity;
            HitResult<PathSegment> result = null;

            RsTarget prevTarget;
            foreach (var path in AllPaths)
            {
                if (!BoxCull(path, testPoint)) continue;

                if (path.Instructions.Count == 0) continue;
                prevTarget = path.Instructions.OfType<RsMoveInstruction>().Select(i => GetTarget(i)).FirstOrDefault(t => t != null);
                if (prevTarget == null) continue;
                foreach (var instruction in path.Instructions.OfType<RsMoveInstruction>())
                {
                    var curTarget = GetTarget(instruction);
                    if (curTarget == null) continue;
                    var startPoint = prevTarget.GetGfx().GlobalMatrix;
                    var endPoint = curTarget.GetGfx().GlobalMatrix;
                    var hitResult = SquareDistanceFromSegment(startPoint.Translation, endPoint.Translation, testPoint);
                    if (hitResult.SquareDistance < minSquareDistance)
                    {
                        minSquareDistance = hitResult.SquareDistance;
                        result = new HitResult<PathSegment>(new PathSegment(prevTarget, instruction), minSquareDistance, hitResult.HitPoint);
                    }
                    prevTarget = curTarget;
                }
            }
            if (minSquareDistance < MaximumDetectionDistance * MaximumDetectionDistance)
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        SegmentHitResult SquareDistanceFromSegment(Vector3 startPoint, Vector3 endPoint, Vector3 testPoint)
        {
            // Vector along segment.
            var a = endPoint - startPoint;
            var direction = a.Normalized();

            var b = testPoint - startPoint;

            // Impact parameter along a.
            double x = direction.Dot(b);

            SegmentHitResult result = new SegmentHitResult();
            if (x < 0)
            {
                result.SquareDistance = startPoint.SquareDistance(testPoint);
                result.HitPoint = startPoint;
            }
            else if (x * x > endPoint.SquareDistance(startPoint))
            {
                result.SquareDistance = endPoint.SquareDistance(testPoint);
                result.HitPoint = endPoint;
            }
            else
            {
                result.SquareDistance = (b - x * direction).SquareLength();
                result.HitPoint = startPoint + direction * x;
            }
            return result;
        }


        //:TODO: Use it for other action instructions with gfx as well?
        public IHitResult DetectSetBrushInstruction(Vector3 testPoint)
        {
            var instructions = AllPaths
                .Where(p => BoxCull(p, testPoint))
                .SelectMany(p => p.Instructions)
                .OfType<RsActionInstruction>()
                .Where(instr => instr.Name == "SetBrush");

            foreach (var instr in instructions)
            {
                var mat = ((IGfxObject)instr).Gfx.GlobalMatrix; // seems like mat ori has the axis that defines the setbrush pointing along the path
                var instrPos = mat.Translation;

                //:TODO: Tweak (non-spherical hitvolume; check if next instr is nearer)
                double dist2 = instrPos.SquareDistance(testPoint);
                if (dist2 < MaximumDetectionDistance * MaximumDetectionDistance /*&& Math.Abs(mat.GetAxisVector(Axis.Z).Dot(testPoint - instrPos)) < 0.01*/)
                {
                    return new HitResult<RsActionInstruction>(instr, dist2, instrPos);
                }
            }

            return null;
        }

        bool BoxCull(IGfxObject obj, Vector3 point)
        {
            var bbox = obj.Gfx.GetBoundingBox(true);
            bbox = bbox.Expand(MaximumDetectionDistance);
            return bbox.Contains(point);
        }

        struct SegmentHitResult
        {
            public double SquareDistance { get; set; }
            public Vector3 HitPoint { get; set; }
        }


    }

    class PathSegment
    {
        public RsTarget PreviousTarget { get; }
        public RsMoveInstruction MoveInstruction { get; }

        public PathSegment(RsTarget previousTarget, RsMoveInstruction moveInstruction)
        {
            PreviousTarget = previousTarget;
            MoveInstruction = moveInstruction;
        }
    }

    class HitResult<T> : IHitResult
    {
        public double SquareDistance { get; }
        public Vector3 HitPoint { get; }
        public T HitObject { get; }

        public HitResult(T hitObject, double squareDistance, Vector3 hitPoint)
        {
            HitObject = hitObject;
            SquareDistance = squareDistance;
            HitPoint = hitPoint;
        }

    }

    /// <summary>
    ///  Cast to <see cref="HitResult{T}"/> to get hit object.
    /// </summary>
    internal interface IHitResult
    {
        double SquareDistance { get; }
        Vector3 HitPoint { get; }
    }
}
