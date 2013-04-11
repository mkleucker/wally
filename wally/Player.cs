using Microsoft.Kinect;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace wally
{

    public class Player
    {
        private ArrayList myPonylines;
        private Polyline currentLine;
        private Skeleton mySkel;
        private int kinectProcessID;
        private System.Windows.Point NewPoint;
        private System.Windows.Media.Brush currentColor;
        private double currentStroke;


        public Player(int ID, Skeleton skel, Polyline line)
        {
            this.currentLine = line;
            this.currentColor = System.Windows.Media.Brushes.White;
            this.currentStroke = 5;
            this.mySkel = skel;
            this.kinectProcessID = ID;
            this.myPonylines = new ArrayList();
        }

        public void addLine(Polyline line)
        {
            this.myPonylines.Add(line);
            this.currentLine = line;
        }
        public Polyline getCurrentLine()
        {
            return this.currentLine;
        }
        public System.Windows.Media.Brush getCurrentColor()
        {
            return this.currentColor;
        }
        public double getCurrentStroke()
        {
            return this.currentStroke;
        }
        public System.Windows.Point getPoint()
        {
            return this.NewPoint;
        }
        public void setCurrentLine(Polyline line)
        {
            this.currentLine = line;
            this.currentColor = line.Stroke;
            this.currentStroke = line.StrokeThickness;
        }
        public void setCurrentColor(System.Windows.Media.Brush brush)
        {
            this.currentColor = brush;
        }
        public void setPoint(System.Windows.Point point)
        {
            this.NewPoint = point;
        }
        public void setCurrentStroke(double stroke)
        {
            this.currentStroke = stroke;
        }
        public Skeleton getSkeleton()
        {
            return this.mySkel;
        }
        public Skeleton setSkeleton(Skeleton skel)
        {
            this.mySkel = skel;
            return this.mySkel;
        }
        public int getPlayersKinectId()
        {
            return this.kinectProcessID;
        }
        public ArrayList getPonyLines()
        {
            return this.myPonylines;
        }
        public int getPonyCount()
        {
            return this.myPonylines.Count;
        }
        public void addPointToCurrentLine(Point point)
        {
            this.currentLine.Points.Add(point);
        }

    }
}