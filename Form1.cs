using System;
using System.Collections;
using System.Collections.Generic;
using Tekla.Structures;
using Tekla.Structures.Geometry3d;
using t3d = Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using tm = Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using Tekla.Structures.Model.UI;
using tu = Tekla.Structures.Model.UI;
using Tekla.Structures.Solid;
using System.Linq;
using System.Windows.Forms;


namespace adding_filler_Plate
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        Model myModel = new Model();
        private void button1_Click(object sender, EventArgs e)
        {
            double plateThik = double.Parse(textBox1.Text);
            double webthik=0;
            this.myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane());
            Picker input = new Picker();
            Part main = input.PickObject(Picker.PickObjectEnum.PICK_ONE_PART) as Part;
            Part sec = input.PickObject(Picker.PickObjectEnum.PICK_ONE_PART) as Part;
            main.GetReportProperty("WEB_THICKNESS", ref webthik);
            CoordinateSystem coo = main.GetCoordinateSystem();
            CoordinateSystem coo2 = sec.GetCoordinateSystem();
            Vector vecX = coo.AxisX;
            Vector vecY = coo.AxisY;
            Vector vecZ = vecY.Cross(vecX);
            Vector vecZ2 = coo2.AxisX.Cross(coo2.AxisY);
            vecX.Normalize();
            vecY.Normalize();
            vecZ.Normalize();
            vecZ2.Normalize();
          
            ArrayList centerPoints = main.GetCenterLine(true);
            t3d.Point C1 = centerPoints[0] as t3d.Point;
            GeometricPlane plane = new GeometricPlane(C1, vecX, vecY);
            ArrayList centerPoints2 = sec.GetCenterLine(true);
            t3d.Point C2 = centerPoints2[0] as t3d.Point;
            t3d.Point C2Projected = Projection.PointToPlane(C2, plane);
            Vector dirZ = new Vector(C2 - C2Projected);
            dirZ.Normalize();
            if (sec.GetType().Name=="Beam")
            {
                Beam secBeam = sec as Beam;
                t3d.Point p1 = Projection.PointToPlane(secBeam.StartPoint, plane);
                t3d.Point p2 = Projection.PointToPlane(secBeam.EndPoint, plane);
                OBB mainObb = CreateObb(main);

             LineSegment Line = new LineSegment(p1,p2);
             LineSegment l = mainObb.IntersectionWith(Line);
             
                
                p1 = l.Point1 + (plateThik / 2 + webthik / 2) * dirZ;
                p2 = l.Point2 + (plateThik / 2 + webthik / 2) * dirZ;
                double width =0;
                sec.GetReportProperty("HEIGHT",ref width);

              Beam filler =  insertFiller(p1, p2, sec.Material.MaterialString, width,plateThik);
          
        ModelObjectEnumerator me =      main.GetBolts();
        while (me.MoveNext())
        {
            BoltArray bolt = me.Current as BoltArray ;
            bolt.AddOtherPartToBolt(filler);
            bolt.Modify();
        }
            }

            else if (sec.GetType().Name == "ContourPlate")
            {
                ContourPlate cplate = sec as ContourPlate;
                Beam mainBeam = main as Beam;
                ArrayList CPoints = cplate.GetCenterLine(true);
                TransformationPlane TransformationPlane = new TransformationPlane(coo);
                TransformationPlane current = myModel.GetWorkPlaneHandler().GetCurrentTransformationPlane();
                ArrayList mainBeamSenter = mainBeam.GetCenterLine(true);
                t3d.Point stBeam = mainBeamSenter[0] as t3d.Point;
                t3d.Point edBeam = mainBeamSenter[1] as t3d.Point;
                stBeam = TransformationPlane.TransformationMatrixToLocal.Transform(current.TransformationMatrixToGlobal.Transform(stBeam));
                edBeam = TransformationPlane.TransformationMatrixToLocal.Transform(current.TransformationMatrixToGlobal.Transform(edBeam));
                ArrayList insidePoints = new ArrayList();
                int factor = 1;
                for (int i = 0; i < CPoints.Count; i++)
                {
                    t3d.Point p = CPoints[i] as t3d.Point;
                    p = TransformationPlane.TransformationMatrixToLocal.Transform(current.TransformationMatrixToGlobal.Transform(p));
                    if (p.X > stBeam.X && p.X < edBeam.X)
                    {
                        p = current.TransformationMatrixToLocal.Transform(TransformationPlane.TransformationMatrixToGlobal.Transform(p));
                        insidePoints.Add(p);
                        if (p.X < stBeam.X)
                        {
                            factor = -1;
                        }
                    }
                }

                t3d.Point p1 = insidePoints[0] as t3d.Point;
                t3d.Point p2 = insidePoints[1] as t3d.Point;
                double width = Distance.PointToPoint(p1, p2);

                p1 = CallulateCenterPoint(p1, p2);
                p2 = p1 + vecX * factor * 10000;


                p1 = Projection.PointToPlane(p1, plane);
                p2 = Projection.PointToPlane(p2, plane);
                OBB mainObb = CreateObb(main);

                LineSegment Line = new LineSegment(p1, p2);
                LineSegment l = mainObb.IntersectionWith(Line);


                p1 = l.Point1 + (plateThik / 2 + webthik / 2) * dirZ;
                p2 = l.Point2 + (plateThik / 2 + webthik / 2) * dirZ;


                Beam filler = insertFiller(p1, p2, sec.Material.MaterialString, width, plateThik);

                ModelObjectEnumerator me = main.GetBolts();
                while (me.MoveNext())
                {
                    BoltArray bolt = me.Current as BoltArray;
                    bolt.AddOtherPartToBolt(filler);
                    bolt.Modify();
                }
            }
            myModel.CommitChanges();
        }
        public Beam insertFiller(t3d.Point p1, t3d.Point p2,string material,double width,double th)
        {
            Beam filler = new Beam();
            filler.StartPoint = p1;
            filler.EndPoint = p2;
            filler.Profile.ProfileString = "PL"+width+"*"+th;
            filler.Material.MaterialString = material;
            filler.PartNumber.Prefix = "L";
            filler.PartNumber.StartNumber = 101;
            filler.AssemblyNumber.Prefix = "";
            filler.AssemblyNumber.StartNumber = 9001;
            filler.Name = "PLATE";
            filler.Position.Depth = Position.DepthEnum.MIDDLE;
            filler.Position.Rotation = Position.RotationEnum.TOP;
            filler.Position.Plane = Position.PlaneEnum.MIDDLE;
            filler.Insert();




            return filler;
        }
        public OBB CreateObb(Part currentBeam)
        {
            OBB obb = (OBB)null;
            if (currentBeam != null)
            {
                WorkPlaneHandler workPlaneHandler = this.myModel.GetWorkPlaneHandler();
                this.myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane());
                Tekla.Structures.Model.Solid solid1 = currentBeam.GetSolid(Solid.SolidCreationTypeEnum.NORMAL);
                Point center = this.CallulateCenterPoint(solid1.MaximumPoint, solid1.MinimumPoint);
                CoordinateSystem coordinateSystem = currentBeam.GetCoordinateSystem();
                workPlaneHandler.SetCurrentTransformationPlane(new TransformationPlane(coordinateSystem));
                Tekla.Structures.Model.Solid solid2 = currentBeam.GetSolid();
                Point maximumPoint = solid2.MaximumPoint;
                Point minimumPoint = solid2.MinimumPoint;
                double extent0 = (maximumPoint.X - minimumPoint.X) / 2.0;
                double extent1 = (maximumPoint.Y - minimumPoint.Y) / 2.0;
                double extent2 = (maximumPoint.Z - minimumPoint.Z) / 2.0;
                obb = new OBB(center, coordinateSystem.AxisX, coordinateSystem.AxisY, coordinateSystem.AxisX.Cross(coordinateSystem.AxisY), extent0, extent1, extent2);
                this.myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane());
            }
            return obb;
        }
        public Point CallulateCenterPoint(Point minpoint, Point maxpoint)
        {
            return new Point(minpoint.X + (maxpoint.X - minpoint.X) / 2.0, minpoint.Y + (maxpoint.Y - minpoint.Y) / 2.0, minpoint.Z + (maxpoint.Z - minpoint.Z) / 2.0);
        }
    }
}
