using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio.Stations;
using RobotStudio.API.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VrPaintAddin
{
    static class PathEditingHelper
    {
        static bool _targetNumberInitialized;
        static int _targetNumber = 0;
        const string TargetNamePrefix = "VR_Target_";

        // Ensures we have an active path to edit
        internal static void EnsurePath()
        {
            if (Station.ActiveStation.ActiveTask.ActivePathProcedure != null) return;

            string pathName = Station.ActiveStation.ActiveTask.GetValidRapidName("VrPath", "_", 1);
            var path = new RsPathProcedure(pathName);
            Station.ActiveStation.ActiveTask.PathProcedures.Add(path);
            Station.ActiveStation.ActiveTask.ActivePathProcedure = path;
        }

        public static RsMoveInstruction InsertMoveInstruction(PathSegment hitObject, Matrix4 frame)
        {
            var nextInstruction = hitObject.MoveInstruction;
            var path = (RsPathProcedure)nextInstruction.Parent;
            var task = (RsTask)path.Parent;
            var wobj = nextInstruction.GetToTarget().WorkObject;

            var robTarget = CreateRobTargetAndTarget(frame, task, wobj);

            // Take from nextInstruction instead?
            var procDef = task.ActiveProcessDefinition;
            var procTemplate = procDef.ActiveProcessTemplate;

            var instruction = new RsMoveInstruction(task, procDef.Name, procTemplate.Name, MotionType.Linear, wobj.Name, robTarget.Name, task.ActiveTool.Name);

            path.Instructions.Insert(path.Instructions.IndexOf(nextInstruction), instruction);

            return instruction;
        }

        public static RsMoveInstruction CreateNewInstructionInActivePath(Matrix4 frame)
        {
            var path = Station.ActiveStation?.ActiveTask?.ActivePathProcedure;
            if (path == null) throw new InvalidOperationException();

            var task = path.Parent as RsTask;
            if (task == null) throw new InvalidOperationException();

            var wobj = task.ActiveWorkObject;

            var robTarget = CreateRobTargetAndTarget(frame, task, wobj);

            var procDef = task.ActiveProcessDefinition;
            var procTemplate = procDef.ActiveProcessTemplate;

            var instruction = new RsMoveInstruction(task, procDef.Name, procTemplate.Name, MotionType.Linear, wobj.Name, robTarget.Name, task.ActiveTool.Name);

            path.Instructions.Add(instruction);

            return instruction;
        }

        public static RsActionInstruction CreateSetBrush(RsMoveInstruction insertBefore, Axis axis, int value, string brushNum)
        {
            //:TODO: Check that SetBrush template is available?

            var path = (RsPathProcedure)insertBefore.Parent;
            var task = (RsTask)path.Parent;
            int index = path.Instructions.IndexOf(insertBefore);

            var ai = new RsActionInstruction(task, "SetBrush", axis.ToString());
            ai.InstructionArguments[axis.ToString()].Value = value.ToString();
            ai.InstructionArguments["BrushNumber"].Value = brushNum;
            path.Instructions.Insert(index, ai);
            return ai;
        }

        public static void DeleteInstruction(RsMoveInstruction instruction)
        {
            var path = (RsPathProcedure)instruction.Parent;
            var target = instruction.GetToTarget();
            var robTarget = instruction.GetToRobTarget();
            var task =  (RsTask)target.Parent;
            path.Instructions.Remove(instruction);
            task.Targets.Remove(target);
            task.DataDeclarations.Remove(robTarget);
        }

        public static void MoveInstructionToFrame(RsMoveInstruction instruction, Matrix4 frame)
        {
            if (instruction == null) return;

            var target = instruction.GetToTarget();
            target.Transform.GlobalMatrix = frame;
        }

        public static Tuple<Axis, RsInstructionArgument> GetBrushEventPosition(RsActionInstruction instr)
        {
            var x = instr.InstructionArguments["X"];
            if (x != null && x.Enabled) return Tuple.Create(Axis.X, x);
            var y = instr.InstructionArguments["Y"];
            if (y != null && y.Enabled) return Tuple.Create(Axis.Y, y);
            var z = instr.InstructionArguments["Z"];
            if (z != null && z.Enabled) return Tuple.Create(Axis.Z, z);
            throw new ArgumentException();
        }

        static RsRobTarget CreateRobTargetAndTarget(Matrix4 frame, RsTask task, RsWorkObject wobj)
        {
            if (!_targetNumberInitialized)
            {
                InitializeTargetNumber(task);
            }
            var robTarget = new RsRobTarget();
            robTarget.Frame.GlobalMatrix = frame;
            robTarget.Name = TargetNamePrefix + _targetNumber++;
            // HACK: Ensure that eax values are set for conveyor. *Will* lead to issues with other
            // external axes.
            robTarget.SetExternalAxes(new ExternalAxisValues
                {
                    Eax_a = 0,
                    Eax_b = 0,
                    Eax_c = 0,
                    Eax_d = 0,
                    Eax_e = 0,
                    Eax_f = 0
                }, false);
            task.DataDeclarations.Add(robTarget);

            var target = new RsTarget(wobj, robTarget);
            target.Transform.GlobalMatrix = frame;

            task.Targets.Add(target);
            return robTarget;
        }

        static void InitializeTargetNumber(RsTask task)
        {
            _targetNumberInitialized = true;
            var targetNumbers = task.DataDeclarations.OfType<RsRobTarget>()
                .Where(t => t.Name.StartsWith(TargetNamePrefix))
                .SelectMany(t => t.Name.Split('_'))
                .Select(s =>
                {
                    int i = 0;
                    if (int.TryParse(s, out i)) return i;
                    else return 0;
                });
            if (targetNumbers.Any())
            {
                _targetNumber = targetNumbers.Max() + 1;
            }
            else
            {
                _targetNumber = 1;
            }
        }
    }
}
