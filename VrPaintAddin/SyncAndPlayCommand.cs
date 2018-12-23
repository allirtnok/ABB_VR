using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Diagnostics;
using ABB.Robotics.RobotStudio.Environment;
using ABB.Robotics.RobotStudio.Stations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VrPaintAddin
{
    class SyncPathAndPlayCommand
    {
        // It would be nicer if we could add this state to the button(command really) instead.
        static bool _blocked;

        public static void Register()
        {
            var btn = new CommandBarButton("SyncPathAndPlay");
            UIEnvironment.CommandBarControls.Add(btn);
            btn.UpdateCommandUI += Btn_UpdateCommandUI;
            btn.ExecuteCommand += Btn_ExecuteCommand;

            // Add to ribbon for testing
            btn.Caption = "Sync & Play";
            UIEnvironment.RibbonTabs["Home"].Groups[Addin.AddinTabGroupId].Controls.Add(btn);
        }

        static async void Btn_ExecuteCommand(object sender, ExecuteCommandEventArgs e)
        {
            try
            {
                _blocked = true;
                var task = Station.ActiveStation.ActiveTask;
                var path = task.ActivePathProcedure;
                Logger.AddMessage(new LogMessage("Synchronizing..."));
                await task.SyncPathProcedureAsync($"{path.ModuleName}/{path.Name}", SyncDirection.ToController, null);
                Logger.AddMessage(new LogMessage("Starting simulation..."));
                await Simulator.StartAsync();
            }
            catch (Exception ex)
            {
                // TODO: Indicate failure in VR in some way ....
                ApplicationLogger.LogException(ex);
            }
            finally
            {
                _blocked = false;
            }
        }

        static void Btn_UpdateCommandUI(object sender, UpdateCommandUIEventArgs e)
        {
            if (!_blocked && Station.ActiveStation?.ActiveTask?.ActivePathProcedure != null && Simulator.State != SimulationState.Running)
            {
                e.Enabled = true;
            }
            else
            {
                e.Enabled = false;
            }
        }
    }
}
