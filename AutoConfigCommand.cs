using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Diagnostics;
using ABB.Robotics.RobotStudio.Environment;
using ABB.Robotics.RobotStudio.Stations;
using KIRTech.AdditionalTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VrPaintAddin
{
    /// <summary>
    /// Simple adaption of AutoConfig for VR. 
    /// Works on active path, not the selected one.
    /// First target must already be configured.
    /// </summary>
    class AutoConfigCommand
    {
        public void Register()
        {
            var btn = new CommandBarButton("VrAutoConfig");
            UIEnvironment.CommandBarControls.Add(btn);
            btn.UpdateCommandUI += Btn_UpdateCommandUI;
            btn.ExecuteCommand += Btn_ExecuteCommand;

            btn = new CommandBarButton("Set as start position for Auto Config");
            btn.UpdateCommandUI += StartPosBtn_UpdateCommandUI;
            btn.ExecuteCommand += StartPosBtn_ExecuteCommand;

            var menu = UIEnvironment.GetContextMenu(typeof(RsJointTarget));
            menu.Controls.Add(btn);
        }

        void Btn_UpdateCommandUI(object sender, UpdateCommandUIEventArgs e)
        {
            // See AutoConfig in RS
            if (UIEnvironment.CurrentlyExecutingCommand != null) return;

            var path = Station.ActiveStation?.ActiveTask?.ActivePathProcedure;
            if (path == null) return;

            //Need a running controller
            var ctrl = path.Parent.Parent as RsIrc5Controller;
            if (ctrl == null || ctrl.SystemState != SystemState.Started) return;

            //Need at least 2 move instructions
            e.Enabled = (path.Instructions.OfType<RsMoveInstruction>().Count(mi => mi.GetToPointArgument() != null) >= 2);

            // For VR: First target must be configured
            // See CKT_AutoConfig, check seems expensive - for now defer until exec

            e.Enabled = true;
        }

        void Btn_ExecuteCommand(object sender, ExecuteCommandEventArgs e)
        {
            e.CompletionTask = RunAutoConfig();
        }

        async Task RunAutoConfig()
        {
            // Add a very short async delay to give give UI a chance to update
            //(:TODO: Rewrite autoconfig as async)
            await Task.Delay(1);

            var path = Station.ActiveStation.ActiveTask.ActivePathProcedure;
            var autoConfig = new CKT_AutoConfiguration(path, false);
            var startJointTarget = GetStartJointTarget();
            if (startJointTarget != null)
            {
                autoConfig.ExternalStartJointTarget = startJointTarget;
            }
            if (autoConfig.ExternalStartJointTarget == null && !autoConfig.FirstTargetConfigured) return;

            bool success = await autoConfig.ExecuteAsync();

            if (success)
            {
                //set current (=last target in path) jointvalues to the controller
                RsTask task = (RsTask)path.Parent;
                RsIrc5Controller ctrl = (RsIrc5Controller)task.Parent;
                foreach (RsMechanicalUnit mu in ctrl.FindMechanicalUnitsByMechanism(task.Mechanism))
                {
                    await RobotStudio.API.Internal.ControllerHelper.CommitJointValues(mu.Mechanism);
                }
            }
        }

        void StartPosBtn_UpdateCommandUI(object sender, UpdateCommandUIEventArgs e)
        {
            var jt = Selection.SelectedObjects.SingleSelectedObject;
            e.Enabled = (jt != null);
            e.Checked = (jt == GetStartJointTarget());
        }

        void StartPosBtn_ExecuteCommand(object sender, ExecuteCommandEventArgs e)
        {
            var jt = Selection.SelectedObjects.SingleSelectedObject;
            Station.ActiveStation.Attributes.Add("VR.AutoConfigStartTarget", jt);
        }

        RsJointTarget GetStartJointTarget()
        {
            RsJointTarget jt;
            Station.ActiveStation.Attributes.TryGetValue("VR.AutoConfigStartTarget", out jt);
            return jt;
        }
    }
}
