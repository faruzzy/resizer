using System;
using System.Collections.Generic;
using System.Text;
using GD;
using System.Drawing.Drawing2D;
using ImageResizer.Util;
using System.Drawing;
using ImageResizer.Resizing;



namespace ImageResizer.Plugins.Gd
{
    public class GdResizerPlugin : BuilderExtension, IPlugin, IQuerystringPlugin
    {
        public GdResizerPlugin()
        {
        }
        public IPlugin Install(Configuration.Config c)
        {
            c.Plugins.add_plugin(this);
            return this;
        }

        public bool Uninstall(Configuration.Config c)
        {
            c.Plugins.remove_plugin(this);
            return true;
        }

        public IEnumerable<string> GetSupportedQuerystringKeys()
        {
            return new string[] { "gd.scale" };

        }


        public static GD.IMode ParseResizeAlgorithm(string sf, GD.IMode defaultValue, out bool valid)
        {
            GD.IMode filter = defaultValue;

            valid = true;
            if ("bicubic".Equals(sf, StringComparison.OrdinalIgnoreCase)) filter = IMode.Bicubic;
            else if ("bilinear-fixed".Equals(sf, StringComparison.OrdinalIgnoreCase)) filter = IMode.BilinearFixed;
            else if ("box".Equals(sf, StringComparison.OrdinalIgnoreCase)) filter = IMode.Box;
            else if ("bspline".Equals(sf, StringComparison.OrdinalIgnoreCase)) filter = IMode.Bspline;
            else if ("catmullrom".Equals(sf, StringComparison.OrdinalIgnoreCase)) filter = IMode.Catmullrom;
            else if ("nearest".Equals(sf, StringComparison.OrdinalIgnoreCase)) filter = IMode.NearestNeighbour;
            else valid = false;

            return filter;
        }

        protected override RequestedAction PreRenderImage(ImageState s)
        {
            //Skip this when we are doing simulations
            if (s.destGraphics == null) return RequestedAction.None;

            string sf = s.settings["gd.scale"];
            if (string.IsNullOrEmpty(sf)) return RequestedAction.None;
            bool validAlg = false;
            GD.IMode filter = ParseResizeAlgorithm(sf, GD.IMode.Bicubic, out validAlg);
            //if (!validAlg) throw new ImageProcessingException("The specified resizing filter '" + sf + "' did not match bicubic, bilinear, box, bspline, catmullrom, or nearest neighbor.");

            //Set copy attributes
            s.copyAttibutes.SetWrapMode(WrapMode.TileFlipXY);


            //The minimum dimensions of the temporary bitmap.
            SizeF targetSize = PolygonMath.getParallelogramSize(s.layout["image"]);
            targetSize = new SizeF((float)Math.Ceiling(targetSize.Width), (float)Math.Ceiling(targetSize.Height));

            s.ApplyCropping();
            s.EnsurePreRenderBitmap();

            //The size of the temporary bitmap. 
            //We want it larger than the size we'll use on the final copy, so we never upscale it
            //- but we also want it as small as possible so processing is fast.
            //SizeF tempSize = PolygonMath.ScaleOutside(targetSize, s.copyRect.Size);
            //int tempWidth = (int)Math.Ceiling(tempSize.Width);
            //int tempHeight = (int)Math.Ceiling(tempSize.Height);
            //FIBITMAP src = FIBITMAP.Zero;
            //FIBITMAP midway = FIBITMAP.Zero;
            //try
            //{
            //    var oldbit = s.preRenderBitmap ?? s.sourceBitmap;
            //    //Crop if needed, Convert, scale, then convert back.
            //    src = FreeImage.CreateFromBitmap(oldbit);

            //    midway = FreeImage.Rescale(src, tempWidth, tempHeight, filter);
            //    FreeImage.UnloadEx(ref src);
            //    //Clear the old pre-rendered image if needed
            //    if (s.preRenderBitmap != null) s.preRenderBitmap.Dispose();
            //    //Reassign the pre-rendered image
            //    s.preRenderBitmap = FreeImage.GetBitmap(midway);
            //    s.copyRect = new RectangleF(0, 0, s.preRenderBitmap.Width, s.preRenderBitmap.Height);
            //    FreeImage.UnloadEx(ref midway);
            //    s.preRenderBitmap.MakeTransparent();

            //}
            //finally
            //{
            //    if (!src.IsNull) FreeImage.UnloadEx(ref src);
            //    if (!midway.IsNull) FreeImage.UnloadEx(ref midway);
            //}


            return RequestedAction.Cancel;
        }
    }
}
