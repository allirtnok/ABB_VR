using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.API.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using static VrPaintAddin.HelperFunctions;
using System.Drawing;

namespace VrPaintAddin
{
    class SigPos : VrInputMode
    {
        private static int currentSignal; // Counts active visual signal
        VrController _rcontroller; // Right Controller
        VrController _lcontroller; //Left controller
        public bool atTrack; //GraphicPosition is at track
        public bool press;
        public bool _recSig;
        TemporaryGraphic _PathAtTrack;
        TemporaryGraphic _PathAtStick;
        TemporaryGraphic _GraphGfx;
        TemporaryGraphic _GraphPointer;

        public override void Activate(VrSession session)
        {
            Station station = Project.ActiveProject as Station;
            Light light = station.Lights[0];
            light.CastShadows = false;
            Light light2 = station.Lights[1];
            light2.CastShadows = false;
            _rcontroller = session.RightController;
            _lcontroller = session.LeftController;
            SetCurrentSignal(0);
            new RecData().RunPos(); 
            atTrack = true;
            press = false;
            _recSig = true;
            currentSignal = 0;



        }

        public override void Deactivate(VrSession session)
        {
            Station station = Project.ActiveProject as Station;
            if (RecData.JointValuesList.Count > 0)
            {
                RecData.ResetPos();
            }
            RecData.resetList();
            DeleteGfx();
            station.ActiveTask.ActivePathProcedure.Visible = true;
            Graph.DeleteChart();

        }

        public override void Update(VrUpdateArgs args)
        {
            if (Simulator.State == SimulationState.Running)
            {
                _recSig = true;
                RecData.signalList.Clear();
            }
            else
            {
                var session = args.Session;
                var input = session.SemanticInput();
                var rwand = _rcontroller as VrViveWandController;
                if (rwand == null) return;
                var lwand = _lcontroller as VrViveWandController;
                Station station = Project.ActiveProject as Station;

                if (RecData.JointValuesList.Count() < 1) return;

                if (rwand.InputState.IsTouchPadTouched)
                {
                    press = false;
                    double u = rwand.InputState.TouchPadPosition.u;
                    double uPrev = rwand.PreviousInputState.TouchPadPosition.u;

                    if ((u - uPrev) < 0 && Math.Abs(u - uPrev) >= 0.02)
                    {
                        if (RecData.JointValuesList.Count > 0)
                        {
                            station.TemporaryGraphics.Remove(_GraphPointer);
                            _GraphPointer.Delete();
                            RecData.PrevPosition();
                            AddGfxPointer();

                        }
                        else
                        {
                            Logger.AddMessage(new LogMessage("No positions recorded, run simulation first"));
                        }
                    }
                    else if ((u - uPrev) >= 0 && Math.Abs(u - uPrev) >= 0.02)
                    {
                        // Flytt til neste pos
                        if (RecData.JointValuesList.Count > 0)
                        {
                            station.TemporaryGraphics.Remove(_GraphPointer);
                            _GraphPointer.Delete();
                            AddGfxPointer();
                            RecData.NextPosition();
                        }
                        else
                        {
                            Logger.AddMessage(new LogMessage("No positions recorded, run simulation first"));
                        }
                    }

                }

                else if (input.IsSelectPressed && press)
                {
                    if (_recSig)
                    {
                         RecData.Ysignal();//::TODO:: Bedre logikk for kjøring av signal
                        _recSig = false;
                    }

                    Logger.AddMessage(new LogMessage($"ListLentgth: {RecData.ToolValuesList.Count}"));
                    if (atTrack)
                    {
                        if (TrackgraphList.Count() >= 0)
                        {
                            foreach (TemporaryGraphic j in TrackgraphList)
                            {
                                station.TemporaryGraphics.Remove(j);
                            }
                            station.TemporaryGraphics.RemoveAll();
                            TrackgraphList.Clear();

                        }

                        Logger.AddMessage(new LogMessage($"listB: {RecData.listB.Count()}"));
                        Logger.AddMessage(new LogMessage($"signalList: {RecData.signalList[0].Count()}"));
                        int sig = GetCurrentSignal() + 1;
                        SetCurrentSignal(sig);
                        AddGfxAtTrack();
                        AddGfxGraph();
                        AddGfxPointer(); // Not implemented yet
                        station.ActiveTask.ActivePathProcedure.Visible = false;
                        RsPathProcedureCollection a = station.ActiveTask.PathProcedures;
                        foreach(RsPathProcedure b in a)
                        {
                            b.Visible = false;
                        }
                          
                        atTrack = false;
                    }

                    else if (!atTrack)
                    {
                        if (TrackgraphList.Count() >=0)
                        {
                            foreach (TemporaryGraphic j in TrackgraphList)
                            {
                                station.TemporaryGraphics.Remove(j);

                            }
                            TrackgraphList.Clear();
                            station.TemporaryGraphics.RemoveAll();
                            _rcontroller.TemporaryGraphics.RemoveAll();
                           // _rcontroller.TemporaryGraphics.RemoveAll();
                        }
                        //int sig = GetCurrentSignal() + 1;
                        //SetCurrentSignal(sig);
                       // AddGfxAtTrack();

                        atTrack = true;

                    }
                    press = false;

                }
                else if (!input.IsSelectPressed && !press)
                {
                    press = true;
                }
            }
        }

        // Graphics section

        static List<TemporaryGraphic> TrackgraphList = new List<TemporaryGraphic>();
        static List<TemporaryGraphic> StickgraphList = new List<TemporaryGraphic>();
        

        public static void SetCurrentSignal(int currentSig1)
        {
            if (currentSig1 < 0) 
            {

                throw new Exception("currentVal can not be negative");
            }
            else if (currentSig1 >= RecData.signalList.Count())
            {
                currentSignal = 0;
            }
            else
            {
                Logger.AddMessage(new LogMessage($"csig {RecData.signalList.Count()}"));
                currentSignal = currentSig1;
            }
        }

        public static int GetCurrentSignal()
        {
            return currentSignal;
        }

        void AddGfxAtTrack()
        {
            Station station = Project.ActiveProject as Station;
            if (TrackgraphList.Count <= 1)
            {
                RecData.lineStyle(GetCurrentSignal());
            }
            double l = RecData.signalList[GetCurrentSignal()].Count();
            for (int i = 0; i < l-1; i++)
                {
                _PathAtTrack = station.TemporaryGraphics.DrawLine(RecData.transposList[i], RecData.transposList[i + 1], RecData.lineWidthList[i], RecData.colorList[i]);
                TrackgraphList.Add(_PathAtTrack);
            }
        } 
        void AddGfxGraph()
        {
            new Graph().CreateChart();
            Matrix4 Roty = Matrix4.Identity; //Want to rotate 180 degrees around y-axis
            Roty.x = Roty.x * -1;
            Roty.z = Roty.z * -1;
            Matrix4 Rotz = Matrix4.Identity; //Want to rotate 90 degrees around z-axis
            Rotz.x = new Vector4(0, 1, 0, 0);
            Rotz.y = new Vector4(-1, 0, 0, 0);
            Rotz.z = new Vector4(0, 0, 1, 0);
            //Matrix4 Rotx = Matrix4.Identity;
            //Rotx.x = new Vector4()


            Bitmap bmp = new Bitmap(@"mychart.bmp");
            _GraphGfx = _rcontroller.TemporaryGraphics.DrawTexturedRectangle(_rcontroller.PointerOffsetTransform * Rotz * Roty, .4+ RecData.signalList[0].Count()/1000, .2, bmp);
            _GraphGfx.Topmost = true;
            //_GraphGfx = _rcontroller.TemporaryGraphics.DrawFrame(_rcontroller.PointerOffsetTransform, .1,2);

        }

        void AddGfxAtStick()
        {
            if (RecData.colorList.Count ==0)
            {
                RecData.lineStyle(GetCurrentSignal());
            }
            double l = RecData.signalList[currentSignal].Count();
            for (int i = 0; i < l-1; i++)
            {
                _PathAtStick = _rcontroller.TemporaryGraphics.DrawLine(RecData.transposList[i], RecData.transposList[i + 1], RecData.lineWidthList[i], RecData.colorList[i]);
                StickgraphList.Add(_PathAtStick);

            }

        }

        void AddGfxPointer()
        {

            double tipLength = 0.01;
            double width = 2;
            Color color = Color.Red;

            Matrix4 Roty = Matrix4.Identity; //Want to rotate 180 degrees around y-axis
            Roty.x = Roty.x * -1;
            Roty.z = Roty.z * -1;
            Matrix4 Rotz = Matrix4.Identity; //Want to rotate 90 degrees around z-axis
            Rotz.x = new Vector4(0, 1, 0, 0);
            Rotz.y = new Vector4(-1, 0, 0, 0);
            Rotz.z = new Vector4(0, 0, 1, 0);



            int robPos = RecData.GetCurrentValue();
            double xa = Graph.chart.Series[0].Points[robPos].XValue;
            double[] xy = Graph.chart.Series[0].Points[robPos].YValues;
            

            Matrix4 rcontrollerpos = _rcontroller.PointerOffsetTransform;
            Vector3 vorigin = rcontrollerpos.Translation;
            Vector3 voriginmove = new Vector3(RecData.GetCurrentValue()*RecData.ToolValuesList.Count()*0.00000025+.0415, 0.015, -0.02);
            Vector3 vstart = vorigin + voriginmove;
            Vector3 move = new Vector3(0, 0, 0.05);
            Vector3 vend = vstart + move;

           _GraphPointer =_rcontroller.TemporaryGraphics.DrawArrow(vstart,vend,tipLength,width,color);
        }


        void DeleteGfx()
        {
            Station station = Project.ActiveProject as Station;
            if (TrackgraphList.Count() >= 0)
            {
                foreach (TemporaryGraphic j in TrackgraphList)
                {
                    station.TemporaryGraphics.Remove(j);

                }
                TrackgraphList.Clear();
                station.TemporaryGraphics.RemoveAll();
                _rcontroller.TemporaryGraphics.RemoveAll();
            }

        }
    }



}
