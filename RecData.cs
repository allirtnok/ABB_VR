using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace VrPaintAddin
{
    class RecData
    {
        public static List<double[]> JointValuesList = new List<double[]>();
        public static List<Vector4> ToolValuesList = new List<Vector4>();
        public static List<Vector3> transposList = new List<Vector3>();
        public static List<int> lineWidthList = new List<int>();
        public static List<Color> colorList = new List<Color>();
        public static List<double[]> signalList = new List<double[]>();

        // Running eventhandler
        public void RunPos()
        {
            Station station = Project.ActiveProject as Station;
            station.ActiveTask.Mechanism.JointValuesChanged += new EventHandler(MyJointListener);

        }

        // Listener
        static void MyJointListener(object sender, EventArgs e)
        {

            //Getting jointposition
            Station station = Project.ActiveProject as Station;

            Mechanism mech = station.ActiveTask.Mechanism;
            double[] jointValues = mech.GetJointValues();
              
            //Getting tool coordinates
            RsToolData tool = station.ActiveTask.ActiveTool;
            Vector4 trans = tool.Frame.GlobalMatrix.t;
            Matrix4 transmat = tool.Frame.GlobalMatrix;
            Vector3 transpos = new Vector3(trans.x, trans.y, trans.z);


            // Only recording values if simulation is running
            if (Simulator.State == SimulationState.Running)
            {

                JointValuesList.Add(jointValues);
                ToolValuesList.Add(trans);
                transposList.Add(transpos);
            }




        }

        // Emptying position lists
        public static void resetList()
        {
            JointValuesList.Clear();
            ToolValuesList.Clear();
            signalList.Clear();
            transposList.Clear();
            lineWidthList.Clear();
            colorList.Clear();
        }

        private static Markup myMark;
        private static int currentVal; //The current chosen position

        //Set current position variable
        public static void SetCurrentValue(int currentVal1)
        {
            if (currentVal1 < 0)
            {

                throw new Exception("currentVal can not be negative");
            }
            currentVal = currentVal1;
        }
        //get current position variable
        public static int GetCurrentValue()
        {
            return currentVal;
        }


        //Method that sets robots position
        public static void SetRobPos(int setVal)
        {
            Station station2 = Project.ActiveProject as Station;
            Mechanism mech = station2.ActiveTask.Mechanism;
            mech.SetJointValues(RecData.JointValuesList[setVal], false);
            //AddMarkup();


        }
        //add markup with signal value at tool position
        public static void AddMarkup()
        {
            double y1;
            Station station2 = Project.ActiveProject as Station;
            myMark = new Markup();
            if (signalList[SigPos.GetCurrentSignal()].Count() > 1)
            {
                y1 = signalList[SigPos.GetCurrentSignal()][currentVal];
            }
            else y1 = 1;
            myMark.Text = "Sig" + (SigPos.GetCurrentSignal() + 1) + ": " + y1.ToString();
            myMark.Transform.Translation = new ABB.Robotics.Math.Vector3(RecData.ToolValuesList[currentVal].x, RecData.ToolValuesList[currentVal].y, RecData.ToolValuesList[currentVal].z);
            station2.Markups.Add(myMark);
        }

        void LineStyle()
        {

        }



        //remove markup
        public static void RemoveMarkup(Markup mark)
        {
            Station station2 = Project.ActiveProject as Station;
            station2.Markups.Remove(mark);
            // station2.Markups.Remove(myMark);

        }
        //Set robot in next position
        public static void NextPosition()
        {
            int a = GetCurrentValue();
            //RemoveMarkup(myMark);
            if (a == RecData.JointValuesList.Count - 1)
            {
                a = 0;
            }
            else a++;

            SetCurrentValue(a);
            SetRobPos(a);



        }
        //set robot in prev position
        public static void PrevPosition()
        {
            int a = GetCurrentValue();
            //RemoveMarkup(myMark);
            if (a <= 0)
            {
                a = RecData.JointValuesList.Count - 1;
            }
            else
            {
                a--;
            }


            SetCurrentValue(a);
            SetRobPos(a);
        }

        public static void Ysignal()
        {
            RealSignal();
            double sampleRate = 12000;
            double amp = 120;
            double freq = 100;
            int l = transposList.Count;
            int l2 = listB.Count;
            double[] y1 = new double[l];
            double[] x1 = new double[l];
            double[] z1 = new double[l];
            double[] y2 = new double[l];
            double[] y3 = new double[l];
            double[] y4 = new double[l];

            double[] yr = new double[l];

            for (int n = 0; n < l; n++)
            {
                y1[n] = (double)(amp * Math.Sin((2 * Math.PI * n * freq) / sampleRate));
                y2[n] = n;
                y3[n] =-n;
                yr[n] = listB[n];
                // y4[n] = (double)(amp * Math.Sin((2 * Math.PI * n * freq*10) / sampleRate));
            }
            for (int n = 0; n < l2; n++)
            {
                //yr[n] = listB[n];
            }
            signalList.Add(yr);
           // signalList.Add(y1);
            //signalList.Add(y2);
            //signalList.Add(y3);
            //signalList.Add(y4);


        }


        public static void lineStyle(int csig)
        {

            if (colorList.Count() >= 0)
            {
                colorList.Clear();
                lineWidthList.Clear();
            }
            int l = signalList[csig].Count();
            double[] ySignal = new double[l];
            for (int i = 0; i < l; i++)
            {
                ySignal[i] = signalList[csig][i];
            }
            int line;
            
            double yMax = ySignal.Max();
            double yMin = ySignal.Min();

            foreach (double y in ySignal)
            {
                if (y == 0)
                {
                    colorList.Add(Color.Green);
                    lineWidthList.Add(3);
                }
                else if (y == Math.Abs(y))
                {

                    double yProp = (y / yMax) * 100;

                    double r = yProp > 50 ? 255 : ((2 * yProp / 100.0) * 255);
                    double g = yProp < 50 ? (255) : ((2 * (100 - yProp) / 100.0) * 255);
                    int rA= (int)Math.Floor(r);
                    int gA = (int)Math.Floor(g);
                    int bA = 0;

                    if (yProp > 0.98 * yMax)
                    {
                        line = 4;
                    }
                    else
                    {
                        line = 3;
                    }
                    colorList.Add(Color.FromArgb(rA, gA, bA));
                    lineWidthList.Add(line);
                }
                else
                {
                    double aaa = (y / yMin) * 100;
                    double gAverage = aaa < 50 ? 255 : ((2 * (100 - aaa) / 100.0) * 255);
                    double bAverage = aaa > 50 ? (255) : ((2 * aaa / 100.0) * 255);
                    int rA = 0;
                    int gA = (int)Math.Floor(gAverage);
                    int bA = (int)Math.Floor(bAverage);
                    line = 3;
                    colorList.Add(Color.FromArgb(rA, gA, bA));
                    lineWidthList.Add(line);
                }

            }






        }

        public static void lineStyle2()
        {


            int l = signalList[SigPos.GetCurrentSignal()].Count();
            double[] yy = new double[l];
            for (int i = 0; i < l; i++)
            {
                yy[i] = signalList[SigPos.GetCurrentSignal()][i];
            }
            double yMax = yy.Max();
            double yMin = yy.Min();

            foreach (double a in yy)
            {
                if (a >= (yMax * 0.98))
                {
                    colorList.Add(Color.DarkRed);
                    lineWidthList.Add(3);
                }
                else if (a <= (yMin * 0.98))
                {
                    colorList.Add(Color.DarkBlue);
                    lineWidthList.Add(3);
                }
                else
                {
                    colorList.Add(Color.Green);
                    lineWidthList.Add(2);
                }

            }

        }

        public static void SetRandomVal()
        {

            Random randn = new Random();
            int randnr = randn.Next(1, RecData.ToolValuesList.Count);
            RemoveMarkup(myMark);
            SetCurrentValue(randnr);
            SetRobPos(randnr);
        }

        public static void ResetPos()
        {
            // RemoveMarkup(myMark);
            SetCurrentValue(0);
            SetRobPos(0);
            //RemoveMarkup(myMark);


        }
        public static List<double> listB = new List<double>();

        public static void RealSignal()
        {
            using (var reader = new StreamReader(@"C:\Users\Kinect\source\repos\VrPaintAddin\sigLog1.csv"))
            {



                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    listB.Add((Convert.ToDouble(double.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture))));




                }
            }
        }
    }
    }

