using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.API.Internal;
using RobotStudio.Services.Controller;
using RobotStudio.Services.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static VrPaintAddin.HelperFunctions;

namespace VrPaintAddin
{
    class SpeedInputMode : VrInputMode
    {
        List<RsPathProcedure> _restoreShowSpeeds = new List<RsPathProcedure>();
        VrPicker _picker = new VrPicker();
        VrPickPreview _pickPreview = new VrPickPreview();
        RsMoveInstruction _currentMoveInstruction;
        VrScrollSelector _selector;
        Vector3 _pickedPos;
        bool _hasScrolled;
        bool _newSelection;
        const double _stickyDist = 0.1;

        public SpeedInputMode()
        {
            _picker.SelectionModes = SelectionModes.Instruction;
            _picker.Filter = pd => pd.selectedObject is RsMoveInstruction;
        }

        public override void Activate(VrSession session)
        {
            var stn = Station.ActiveStation;
            if (stn == null) return;

            var tasks = stn.Irc5Controllers.SelectMany(t => t.Tasks).ToList();
            tasks.Add(stn.DefaultTask);

            foreach(var task in tasks)
            {
                foreach(RsPathProcedure p in task.PathProcedures)
                {
                    if(!p.ShowSpeeds)
                    {
                        p.ShowSpeeds = true;
                        _restoreShowSpeeds.Add(p);
                    }
                }
            }
        }

        public override void Deactivate(VrSession session)
        {
            _pickPreview.Clear();
            foreach (var p in _restoreShowSpeeds)
            {
                p.ShowSpeeds = false;
            }
            _restoreShowSpeeds.Clear();
        }

        public override void Update(VrUpdateArgs args)
        {
            var session = args.Session;

            // KNARK - Rift
            var wand = session.RightController as VrViveWandController;
            if (wand == null) return;

            RsMoveInstruction mi = null;
            if (_currentMoveInstruction != null)
            {
                // "Sticky" selection - don't remove the UI until 1) user releases the touchpad and 2) moves controller a bit away from path
                //:NOTE: Sticky distance can be annoying when editing nearby segments - review. Use shorter dist if another MI is closer/picked?
                if (wand.InputState.IsTouchPadTouched || _pickedPos.Distance(wand.PointerTransform.Translation) < _stickyDist && _hasScrolled)
                {
                    mi = _currentMoveInstruction;
                }
            }

            if (mi == null && !wand.InputState.IsGripPressed && !wand.InputState.IsTouchPadPressed && !wand.InputState.IsTriggerPressed)
            {
                mi = GetPickedMoveInstruction(session);
            }

            if (mi != _currentMoveInstruction)
            {
                // Selected MI has changed, delete any old UI and create a new if needed
                if (_selector != null) { _selector.Dispose(); _selector = null; _hasScrolled = false; }
                _currentMoveInstruction = mi;
                if (mi != null)
                {
                    _pickedPos = session.RightController.PointerTransform.Translation;
                    var task = mi.GetInternalParentOfType<RsTask>();
                    //:TODO: Some paint stations have too many speeddatas for this type of UI to make sense. How to handle?

                    string taskName = task.Name;
                    if (string.IsNullOrEmpty(taskName)) taskName = "T_ROB1"; // station/dummy tasks

                    var speeds = task.GetTaskContext().SymbolTable.GetVisibleDataDeclarations("/RAPID/" + taskName, "speeddata")
                        .Where(s => !s.Name.StartsWith("vrot")) //???
                        .Take(60) //TEST
                        .Select(s => s.Name)
                        .ToList();

                    speeds.Sort(ABB.Robotics.RobotStudio.UI.UIServices.NaturalOrderSort);
                    string speed = GetSpeedArg(mi).Value;
                    int tmp = speeds.FindIndex(s => s.Equals(speed, StringComparison.OrdinalIgnoreCase));
                    if (tmp == -1)
                    {
                        speeds.Insert(0, "Unknown");
                        speed = speeds[0];
                    }
                    else
                    {
                        speed = speeds[tmp]; // fix capitalization
                    }
                    string title = "->" + mi.GetToTarget().Name; // TEST - what do we want here?
                    _selector = new VrScrollSelector(session.RightController, title, speeds, speed);
                    _newSelection = true;
                }
            }
            else if (_currentMoveInstruction != null && _selector != null)
            {
                if (wand.InputState.IsTouchPadTouched) _hasScrolled = true;

                // Same selection - update _selector and apply results
                bool ok = _selector.Update();
                if (ok && _selector.SelectedValue != "Unknown")
                {
                    Action foo = () => { GetSpeedArg(_currentMoveInstruction).Value = _selector.SelectedValue; };
                    if (_newSelection)
                    {
                        _newSelection = false;
                        WithUndo("VR Edit Speed", foo);
                    }
                    else
                    {
                        WithUndoAppend("VR Edit Speed", foo);
                    }
                }
            }
        }

        RsMoveInstruction GetPickedMoveInstruction(VrSession session)
        {
            var pd = _picker.PickNearest(session);
            _pickPreview.Update(session, pd);
            return pd.selectedObject as RsMoveInstruction;
        }

        RsInstructionArgument GetSpeedArg(RsMoveInstruction mi)
        {
            return mi.GetArgumentsByDataType("speeddata").FirstOrDefault();
        }
    }
}
