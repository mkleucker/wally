using Microsoft.Kinect;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace wally
{

    public class Player
    {
        private bool isActive;
        private ArrayList myPonylines;
        private Polyline currentLine;
        private Skeleton mySkel;
        private int kinectProcessID;
        // private System.Windows.Point NewPoint;
        private System.Windows.Media.Brush currentColor;
        private double currentStroke;
        private Canvas myCanvas;
        private String lastPngImage;

        public Player()
        {
            this.isActive = false;
        }

        public void activatePlayer(int ID, Skeleton skel, Polyline line, Canvas canvas)
        {
            this.isActive = true;
            this.currentLine = line;
            this.currentColor = System.Windows.Media.Brushes.White;
            this.currentStroke = 5;
            this.mySkel = skel;
            this.kinectProcessID = ID;
            this.myPonylines = new ArrayList();
            this.myCanvas = canvas;
        }

        public void deactivatePlayer()
        {
            this.isActive = false;
            this.currentLine = null;
            this.currentColor = null;
            this.currentStroke = 0;
            this.mySkel = null;
            this.kinectProcessID = 0;
            this.myPonylines = null;
            this.myCanvas = null;
        }

        public void addLine(Polyline line)
        {
            this.myPonylines.Add(line);
            this.setCurrentLine(line);
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
        //public System.Windows.Point getPoint()
        //{
        //    return this.NewPoint;
        //}
        public void setCurrentLine(Polyline line)
        {
            this.currentLine = line;
            this.currentColor = line.Stroke;
            this.currentStroke = line.StrokeThickness;
        }
        public void setLastPngImg(String path)
        {
            this.lastPngImage = path;
        }
        public String getLastPngImg()
        {
            return this.lastPngImage;
        }
        public void setCurrentColor(System.Windows.Media.Brush brush)
        {
            this.currentColor = brush;
        }
        //public void setPoint(System.Windows.Point point)
        //{
        //    this.NewPoint = point;
        //}
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
        public Canvas getMyCanvas()
        {
            return this.myCanvas;
        }
        public void addPointToCurrentLine(Point point)
        {
            this.currentLine.Points.Add(point);
        }
        public bool getState()
        {
            return this.isActive;
        }
        public void setState(bool state)
        {
            this.isActive = state;
        }

    }
}