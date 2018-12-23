using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.API.Internal;
using RobotStudio.Services.Graphics;
using static VrPaintAddin.HelperFunctions;

namespace VrPaintAddin 
{
    class ViewTest : VrInputMode
    {

        // Mulig å legge ting til view i robotstudio, eller må jeg ha tilgang til camera(head)?

        // ---> Attatch screen(graph of signals) to controller 


        TemporaryGraphic _frameGfx;
        TemporaryGraphic _lineGfx;
        TemporaryGraphic _pospointer;
        VrController _controller;

        public override void Activate(VrSession session)
        {
            _controller = session.RightController;
            AddGfx();
        }

        public override void Deactivate(VrSession session)
        {
            DeleteGfx();
        }

        public override void Update(VrUpdateArgs args)
        {
            var session = args.Session;
            Vector3 xorigin = _controller.PointerOffsetTransform.Translation;


            Vector3 btrans = _controller.PointerOffsetTransform.Translation;

            var rwand = VrEnvironment.Session.RightController as VrViveWandController;

            if (rwand.InputState.IsTouchPadTouched)
            {
                double u = rwand.InputState.TouchPadPosition.u;
                double uPrev = rwand.PreviousInputState.TouchPadPosition.u;
                if ((u - uPrev) < 0 && Math.Abs(u - uPrev) >= 0.1 && btrans.x > xorigin.x)
                {
                    btrans.x = btrans.x - 10;
                    _pospointer.Delete();

                }

                else if ((u - uPrev) >= 0 && Math.Abs(u - uPrev) >= 0.1)
                {

                    btrans.x = btrans.x + 10;
                    _pospointer.Delete();



                }


            }



            Matrix4 boxpos = _controller.PointerOffsetTransform;
            boxpos.Translation = new Vector3(btrans.x, btrans.y, btrans.z);
            Vector3 box = new Vector3(.01, .01, .01);
            _pospointer = _controller.TemporaryGraphics.DrawBox(boxpos, box, Color.DarkBlue);
            


        }


        // 1. Attatch random object to right controller:
        public void Attatch()
        {
            VrSession session = VrEnvironment.Session;
            _controller = session.RightController;

        }
        
        void AddGfx()
        {

            Matrix4 Roty = Matrix4.Identity; //Want to rotate 180 degrees around y-axis
            Roty.x = Roty.x * -1;
            Roty.z = Roty.z * -1;



            Matrix4 Rotz = Matrix4.Identity; //Want to rotate 90 degrees around z-axis

            Rotz.x = new Vector4(0, 1, 0, 0);
            Rotz.y = new Vector4(-1, 0, 0, 0);
            Rotz.z = new Vector4(0, 0, 1, 0);

            
            

            //_attachOffset = VrEnvironment.Session.RightController.PointerOffsetTransform * paintToolOffset * tooldata.Frame.Matrix.InverseRigid();
            Bitmap bmp = new Bitmap(@"C:\Users\Kinect\source\repos\chart1\chart1\bin\Debug\mychart.bmp");
           _frameGfx = _controller.TemporaryGraphics.DrawTexturedRectangle(_controller.PointerOffsetTransform*Rotz*Roty, .4, .2, bmp); // Fin størrelse. Må få bedre graf
           
          //_frameGfx = _controller.TemporaryGraphics.DrawFrame(_controller.PointerOffsetTransform,.1,2);
          //  _frameGfx = _controller.TemporaryGraphics.DrawLineStrip

        }

       

        void DeleteGfx()
        {
            if (_frameGfx != null)
            {
                _frameGfx.Delete();
            }
            if (_pospointer != null)
            {
                _pospointer.Delete();
            }


            
        }

        //Drawing bitmap:
        

        private Bitmap DrawGraph(int x, int y)
        {
            Ysignal();
            lineStyle2();
            Bitmap bmp = new Bitmap(x, y);
            using (Graphics graph =Graphics.FromImage(bmp))
            {
                Rectangle ImageSize = new Rectangle(0, 0, x, y);
                graph.FillRectangle(Brushes.White, ImageSize);
                for (int i = 0; i < signalList.Count() - 1; i++)
                {
                    Pen pen = new Pen(colorList[i], 2);
                     
                    //graph.DrawLine(pen,(float)signalList[i].x,(float)signalScaleList[i], (float)signalList[i + 1].x,(float)signalScaleList[i + 1]);
                    graph.DrawLine(pen, (float)signalScaleList[i], (float)signalList[i].x, (float)signalScaleList[i + 1],(float)signalList[i + 1].x);

                }
            }



            return bmp;

        }
        public static List<Vector3> signalList = new List<Vector3>();
        public static List<double> lineWidthList = new List<double>();
        public static List<Color> colorList = new List<Color>();

        public static void Ysignal()
        {
            Logger.AddMessage(new LogMessage("Lager ysig"));
            double sampleRate = 100000;
            double amp = 120;
            double freq = 2000;
            int l = 100;
            double[] y = new double[l];
            double[] x1 = new double[l];
            double[] z1 = new double[l];
            for (int n = 0; n < l; n++)
            {

                y[n] = (double)(amp * Math.Sin((2 * Math.PI * n * freq) / sampleRate));
                x1[n] = (double)n;
                z1[n] = (double)1;
                //y[n] = (double)1000;
                Vector3 points = new Vector3(x1[n], y[n], z1[n]);
                signalList.Add(points);
            }

            

        }
        public static List<double> signalScaleList = new List<double>();
        public static void lineStyle2()
        {


            int l = signalList.Count();
            double[] yy = new double[l];
            for (int i = 0; i < l; i++)
            {
                yy[i] = signalList[i].y;
            }
            double yMax = yy.Max();
            double yMin = yy.Min();
            double yAvg = yy.Average();

            foreach (double a in yy)
            {
                if (a >= (yMax * 0.98))
                {
                    colorList.Add(Color.DarkRed);
                    lineWidthList.Add(2);
                }
                else if (a <= (yMin * 0.98))
                {
                    colorList.Add(Color.DarkBlue);
                    lineWidthList.Add(2);
                }
                else
                {
                    colorList.Add(Color.Green);
                    lineWidthList.Add(1);
                }
                double aa = ((a / yMax)+(a/yAvg)) * 100;
                signalScaleList.Add(aa);
            }

        }




    }
}
