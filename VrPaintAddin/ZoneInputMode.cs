using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.API.Internal;
using RobotStudio.Services.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Not yet used, review

namespace VrPaintAddin
{
    // TODO - see if we can do both speed&zone in same tool??


    class ZoneInputMode : VrInputMode
    {
        List<RsPathProcedure> _restoreShowZones = new List<RsPathProcedure>();
        PathObjectDetector _detector;

        double _distOffset;
        RsMoveInstruction _dragMoveInstr;
        RsInstructionArgument _zoneArg;

        static ZoneInputMode()
        {
            var availableZones = new SortedDictionary<double, string>
            {
				//{0, "fine"},
				{0.3e-3, "z0"},
                {1e-3, "z1"},
                { 5e-3,"z5"},
                { 10e-3,"z10"},
                { 15e-3,"z15"},
                { 20e-3,"z20"},
                { 30e-3,"z30"},
                { 40e-3,"z40"},
                { 50e-3, "z50"},
                { 60e-3, "z60"},
                { 80e-3, "z80"},
                { 100e-3, "z100"},
                { 150e-3, "z150"},
                { 200e-3, "z200"}
            };
        }

        public override void Activate(VrSession session)
        {
            var stn = Station.ActiveStation;
            if (stn == null) return;

            _detector = new PathObjectDetector(stn);

            var tasks = stn.Irc5Controllers.SelectMany(t => t.Tasks).ToList();
            tasks.Add(stn.DefaultTask);

            foreach(var task in tasks)
            {
                foreach(RsPathProcedure p in task.PathProcedures)
                {
                    if(p.ZoneVisualization != ZoneVisualization.Programmed)
                    {
                        p.ZoneVisualization = ZoneVisualization.Programmed; // actual?
                        _restoreShowZones.Add(p);
                    }
                }
            }
        }

        public override void Deactivate(VrSession session)
        {
            foreach(var p in _restoreShowZones)
            {
                p.ZoneVisualization = ZoneVisualization.None;
            }
            _restoreShowZones.Clear();
        }

        public override void Update(VrSession session)
        {
            var ctrl = session.RightController;

            if(_dragMoveInstr != null)
            {
                if (ctrl.InputState.TriggerPressed)
                {
                    //double newZoneVal = 
                }
                else
                {
                    _dragMoveInstr = null;
                }
            }

            var ctrlPos = ctrl.PointerTransform.Translation;
            var hitResult = _detector.DetectMoveInstruction(ctrlPos) as HitResult<RsMoveInstruction>;
            if(hitResult != null)
            {
                //:TODO: Update some preview

                if(ctrl.InputState.TriggerPressed && !ctrl.PreviousInputState.TriggerPressed)
                {
                    // Improve?
                    _zoneArg = hitResult.HitObject.InstructionArguments["Z"];
                    if (_zoneArg == null) return;
                    int zoneVal = 0;
                    if (_zoneArg.Name.StartsWith("z") && _zoneArg.Enabled) int.TryParse(_zoneArg.Value, out zoneVal);
                    _distOffset = ctrlPos.Distance(hitResult.HitPoint) - zoneVal;
                    _dragMoveInstr = hitResult.HitObject;
                }
            }
        }
    }
}
