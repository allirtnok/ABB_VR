using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using RobotStudio.API.Internal;
using RobotStudio.Services.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static VrPaintAddin.HelperFunctions;

namespace VrPaintAddin
{
    public class PaintTextureInputMode : VrInputMode
    {
        GfxTexturePainter _painter;
        Part _part;
        TemporaryGraphic _paintCone;
        int _curColorIndex;
        Color[] _colors = { Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Brown, Color.LightBlue, Color.DarkSeaGreen, Color.LightSeaGreen };

        public PaintTextureInputMode()
        {
        }

        public override void Activate(VrSession session)
        {
            //:TODO: How to determine which part to paint?
            var part = Station.ActiveStation.FindGraphicComponentsByType(typeof(Part)).OfType<Part>().FirstOrDefault(p => p.Name == "PaintPart");
            if(part != null)
            {
                _painter = new GfxTexturePainter(part);
                _painter.Range = 0.500;
                _painter.SizeX = 0.150;
                _painter.SizeY = 0.400;
                _painter.Strength = 0.1;
                _painter.ShowPaint(true);
                _part = part;

                UpdatePreview(session);
            }
        }

        private void UpdatePreview(VrSession session)
        {
            if (_paintCone != null) _paintCone.Delete();
            var mesh = _painter.CreatePreviewGraphics();
            var pm = session.RightController.PointerOffsetTransform;
            _paintCone = session.RightController.TemporaryGraphics.DrawMesh(pm, mesh);
        }

        public override void Deactivate(VrSession session)
        {
            if(_painter != null)
            {
                _painter.Dispose();
                _painter = null;
            }

            if (_paintCone != null)
            {
                _paintCone.Delete();
                _paintCone = null;
            }

            _part = null;
        }

        public override void Update(VrUpdateArgs args)
        {
            if (_painter == null) return;

            var session = args.Session;
            var input = session.SemanticInput();

            // Cycle colors each time trigger is pressed
            if (input.SelectClick)
            {
                _curColorIndex++;
                if (_curColorIndex >= _colors.Length) _curColorIndex = 0;
                _painter.Color = _colors[_curColorIndex];
                UpdatePreview(session);
            }

            if (input.IsSelectPressed)
            {
                var transform = session.RightController.PointerTransform;
                transform.CleanRigid(); //:TODO: Should not be needed. Review
                _painter.Render(transform);
            }

            if(input.DeleteClick)
            {
                _painter.Clear();
            }
        }
    }
}
