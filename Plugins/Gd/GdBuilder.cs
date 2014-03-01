using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Resizing;
using ImageResizer.Encoding;
using ImageResizer.Configuration.Issues;
using System.Drawing;
using ImageResizer.Util;
using System.Drawing.Imaging;
using ImageResizer.Plugins.Basic;
using System.IO;
using ImageResizer.Configuration;

namespace ImageResizer.Plugins.GdBuilder
{

    public class GdBuilderPlugin :BuilderExtension, IPlugin, IIssueProvider {

        public GdBuilderPlugin()
        {
        }

        Config c;
        public IPlugin Install(Configuration.Config c) {
            c.Plugins.add_plugin(this);
            this.c = c;
            return this;
        }

        public bool Uninstall(Configuration.Config c) {
            c.Plugins.remove_plugin(this);
            return true;
            
        }

        protected GD.Enc GetGdFormatFromMime(string mimetype)
        {
            if ("image/png".Equals(mimetype, System.StringComparison.OrdinalIgnoreCase)) return GD.Enc.PNG;
            if ("image/jpeg".Equals(mimetype, System.StringComparison.OrdinalIgnoreCase)) return GD.Enc.JPEG;
            if ("image/gif".Equals(mimetype, System.StringComparison.OrdinalIgnoreCase)) return GD.Enc.GIF;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds alternate pipeline based on FreeImage. Invoked by &builder=freeimage. 
        /// This method doesn't handle job.DisposeSource or job.DesposeDest or settings filtering, that's handled by ImageBuilder.
        /// All the bitmap processing is handled by buildFiBitmap, this method handles all the I/O
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        protected override RequestedAction BuildJob(ImageJob job) {
            if (!"gd".Equals(job.Settings["builder"])) return RequestedAction.None;
           
            //StringBuilder log = new StringBuilder();

            //FreeImageAPI.FreeImageEngine.Message += (delegate(FREE_IMAGE_FORMAT fmt, string msg) {
            //    log.AppendLine(msg);
            //});

            // Variables
            Stream s = null;
            bool disposeStream = !(job.Source is Stream);
            long originalPosition = 0;
            bool restoreStreamPosition = false;

            //Get a Stream instance for the job
            string path;
            s = c.CurrentImageBuilder.GetStreamFromSource(job.Source, job.Settings, ref disposeStream, out path, out restoreStreamPosition);
            if (s == null) return RequestedAction.None;
            if (job.ResetSourceStream) restoreStreamPosition = true;
            job.SourcePathData = path;

            //Save the original stream positione
            originalPosition = (restoreStreamPosition) ? s.Position : -1;
            try {
                //What is our destination format
                IEncoder managedEncoder = c.Plugins.GetEncoder(job.Settings, job.SourcePathData); //Use the existing pipeline to parse the querystring
                //This prevents us from supporting output formats that don't already have registered encoders. Good, right?
                var destFormat = GetGdFormatFromMime(managedEncoder.MimeType);

                bool supportsTransparency = managedEncoder.SupportsTransparency;

                var data = new GD.ImageData(new BinaryReader(s), GD.Enc.JPEG);

                var original = data.decode();
                var mayUnloadOriginal = true;

                    try {
                        //Do all the bitmap stuff in another method
                        var b = buildGDBitmap(ref original, job, supportsTransparency, mayUnloadOriginal);
                        if (b == null) return RequestedAction.None;

                        // Try to save the bitmap
                        if (job.Dest is string || job.Dest is Stream) {
                            if (job.Dest is string) {
                                //Make physical and resolve variable references all at the same time.
                                job.FinalPath = job.ResolveTemplatedPath(job.Dest as string,
                                    delegate(string var) {
                                        if ("width".Equals(var, StringComparison.OrdinalIgnoreCase)) return b.sx.ToString();
                                        if ("height".Equals(var, StringComparison.OrdinalIgnoreCase)) return b.sy.ToString();
                                        if ("ext".Equals(var, StringComparison.OrdinalIgnoreCase)) return managedEncoder.Extension;
                                        return null;
                                    });
                                //If requested, auto-create the parent directory(ies)
                                if (job.CreateParentDirectory) {
                                    string dirName = Path.GetDirectoryName(job.FinalPath);
                                    if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);
                                }
                            }

                            Stream outstream = null;
                            if (job.Dest is String){
                                
                            }else if (job.Dest is Stream) {
                                b.encode(destFormat).save(new BinaryWriter((Stream)job.Dest));

                            }

                        } else if (job.Dest == typeof(Bitmap)) {
                            throw new NotImplementedException();
                        } else return RequestedAction.None;
                        return RequestedAction.Cancel;
                    } finally {
                        //if (!b.IsNull && b != original) FreeImage.UnloadEx(ref b);
                    }
                
            } finally {
                if (s != null && restoreStreamPosition && s.CanSeek) s.Seek(originalPosition, SeekOrigin.Begin);
                if (disposeStream) s.Dispose();
            }

        }
   

        /// <summary>
        /// Builds an FIBitmap from the stream and job.Settings 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="job"></param>
        /// <returns></returns>
        protected GD.Image buildGDBitmap(ref GD.Image original, ImageJob job, bool supportsTransparency, bool mayUnloadOriginal)
        {

            ResizeSettings settings = job.Settings;

            //Find the image size
            Size orig = new Size((int)original.sx, (int)original.sy);

            //Calculate the new size of the image and the canvas.
            ImageState state = new ImageState(settings, orig, true);
            c.CurrentImageBuilder.Process(state);
            RectangleF imageDest = PolygonMath.GetBoundingBox(state.layout["image"]);

            GD.Image final = null;
            if (imageDest.Width != orig.Width || imageDest.Height != orig.Height) {
                //Rescale
                bool temp;
                original.interpolation_method = GD.IMode.Bicubic;
                final = original.scale((uint)imageDest.Width, (uint)imageDest.Height);
                //if (mayUnloadOriginal) FreeImage.UnloadEx(ref original);
                //if (final.IsNull) return FIBITMAP.Zero;
            } else {
                final = original;
            }

            Color bgcolor = settings.BackgroundColor;
            if (settings.BackgroundColor == Color.Transparent && !supportsTransparency)
                bgcolor = Color.White;

            //If we need to leave padding, do so.
            BoxPadding outsideImage = new BoxPadding(imageDest.Left, imageDest.Top, state.destSize.Width - imageDest.Right, state.destSize.Height - imageDest.Bottom);

            if (outsideImage.All != 0) {
                var old = final;
                //Extend canvas
                final = new GD.Image(state.destSize.Width,state.destSize.Height);
                if (bgcolor != Color.Transparent)
                    final.filledRectangle(0, 0, final.sx, final.sy, bgcolor.ToArgb());
                final.copyFrom(old,(int)imageDest.Left,(int)imageDest.Top,0,0,old.sx,old.sy);

                if (old == original) {
                    //if (mayUnloadOriginal) {
                    //    FreeImage.UnloadEx(ref original);
                    //    old = original;
                    //}
                } else {
                    //FreeImage.UnloadEx(ref old); //'old' has the original value of 'final', which we allocated.
                }
                //if (final.IsNull) return FIBITMAP.Zero;
            }

            return final;

        }


        public IEnumerable<IIssue> GetIssues() {
            List<IIssue> issues = new List<IIssue>();
            //if (!FreeImageAPI.FreeImage.IsAvailable()) issues.Add(new Issue("The FreeImage library is not available! All FreeImage plugins will be disabled.", IssueSeverity.Error));
            return issues;
        }
    }
}

