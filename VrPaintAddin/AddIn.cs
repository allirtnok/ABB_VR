using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RobotStudio.API.Internal;
using ABB.Robotics.RobotStudio.Environment;
using RobotStudio.UI.Graphics.VR;
using ABB.Robotics.RobotStudio.Stations;

namespace VrPaintAddin
{
    public static class Addin
    {
        public const string AddinTabGroupId = "VrPaintAddinTab";

        const string SyncAndPlay = "SyncAndPlay";
        const string VrSpeed = "VrSpeed";
        //const string VrSnap = "VrSnap";

        //static CommandBarButton _snap = new CommandBarButton(VrSnap);

        public static void AddinMain()
        {
            VrEnvironment.SessionStarted += VrEnvironment_SessionStarted;
            var group = new RibbonGroup(AddinTabGroupId, "VR addin");
            UIEnvironment.RibbonTabs["Home"].Groups.Add(group);

            new AutoConfigCommand().Register();
            SyncPathAndPlayCommand.Register();
            //_snap.DefaultEnabled = true;
            //_snap.ExecuteCommand += (s, e) => { EventDrivenInputMode.Snap = !EventDrivenInputMode.Snap; };
        }

        static void VrEnvironment_SessionStarted(object sender, EventArgs e)
        {
            // For now, only add these buttons if VR is connected to active station
            if (Station.ActiveStation != VrEnvironment.Session.Station) return;

            var pane = VrEnvironment.Session.UserInterface.MenuCube.MenuPanes[3];

            pane.Items.Add(new VrMenuInputModeButton(new PaintPathInputMode(), "Paint"));
            pane.Items.Add(new VrMenuInputModeButton(new PathEditingInputMode(), "Edit"));
            pane.Items.Add(new VrMenuInputModeButton(new BrushInputMode(), "Brush"));
            pane.Items.Add(new VrMenuInputModeButton(new SpeedInputMode(), "Speed"));
            pane.Items.Add(new VrMenuInputModeButton(new VrJogMode(), "Jog"));

            pane.Items.Add(new VrMenuCommandButton("VrAutoConfig"));
            pane.Items.Add(new VrMenuCommandButton("SyncPathAndPlay"));
            pane.Items.Add(new VrMenuCommandButton("SimulationStop"));
            //pane.Items.Add(new VrMenuCommandButton(VrSnap));
        }
    }
}
