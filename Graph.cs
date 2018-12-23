using ABB.Robotics.Math;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace VrPaintAddin
{
    public partial class Graph : Form
    {
        public Graph()
        {
            InitializeComponent();
        }

        public Graphics aa;
        public static Chart chart;
        public void CreateChart()
        {

            int signr = SigPos.GetCurrentSignal();
            int l = RecData.signalList[signr].Count();
            for (int i = 0; i < l; i++)
            {
                chart1.Series[0].Points.Add(RecData.signalList[0][i]);
                chart1.Series[0].Points[i].Color = RecData.colorList[i];
                chart1.Series[0].Points[i].BorderWidth = RecData.lineWidthList[i];
                
                
            }
            chart1.Size = new Size(1100 + RecData.signalList[signr].Count()*2, 520);
            //Series MIN = chart1.Series.Add($"Count: {RecData.colorList.Count().ToString()}");
           // MIN.Font = new Font("Times", 72f);
            this.chart1.SaveImage(@"mychart.bmp", ChartImageFormat.Bmp);
            aa = this.chart1.CreateGraphics();
            chart = this.chart1;
        }

        public static void DeleteChart()
        {
            if (File.Exists(@"mychart.bmp"))
            {
               // File.Delete(@"mychart.bmp");
            }
            RecData.resetList();
        }


        private void chart1_Click(object sender, EventArgs e)
        {

        }
    }
}
