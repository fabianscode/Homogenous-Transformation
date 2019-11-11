public static class ImageManipulationHelper {
        public static byte[] Crop(SKBitmap bitmap, Point startPosition, Size rectangleSize) {
            // normalize input values
            if (startPosition.X < 0) startPosition.X = 0;
            if (startPosition.Y < 0) startPosition.Y = 0;
            if (rectangleSize.Width > bitmap.Width) rectangleSize.Width = bitmap.Width;
            if (rectangleSize.Height > bitmap.Height) rectangleSize.Height = bitmap.Height;
            
            // return bitmap if input == output
            if (startPosition.X == 0 && startPosition.Y == 0 && rectangleSize.Width == bitmap.Width && rectangleSize.Height == bitmap.Height) return bitmap.Bytes;
            
            var dst = new SKBitmap((int) rectangleSize.Width + 1, (int) rectangleSize.Height + 1);
            var src = bitmap;
            var width = rectangleSize.Width;
            var height = rectangleSize.Height;

            unsafe {
                var dstPtr = (byte*) dst.GetPixels().ToPointer();
                for (var y = 0; y < height; ++y) {
                    for (var x = 0; x < width; ++x) {
                        var (d, d1) = startPosition;
                        var pixel = src.GetPixel((int) (x + d), (int) (y + d1));
                        *dstPtr++ = pixel.Blue;
                        *dstPtr++ = pixel.Green;
                        *dstPtr++ = pixel.Red;
                        *dstPtr++ = pixel.Alpha;
                    }
                }
            }
            
            return dst.PeekPixels().Encode(Preferences.ImageManipulationResultFormat, Preferences.ImageManipulationResultQuality).ToArray();
        }

        // TODO: Performance
        public static byte[] Distort(SKBitmap bitmap, Point uLFrom, Point uLTo, Point uRFrom, Point uRTo, Point bLFrom, Point bLTo, Point bRFrom, Point bRTo, CustomCachedImage image = null) {
            // normalize input values
            if (uLFrom.X < 0) uLFrom.X = 0;
            if (uLFrom.Y < 0) uLFrom.Y = 0;
            if (bRFrom.X > bitmap.Width) bRFrom.X = bitmap.Width;
            if (bRFrom.Y > bitmap.Height) bRFrom.Y = bitmap.Height;
            if (uRFrom.Y < 0) uRFrom.Y = 0;
            if (uRFrom.X > bitmap.Width) uRFrom.X = bitmap.Width;
            if (bLFrom.X < 0) bLFrom.X = 0;
            if (bLFrom.Y > bitmap.Height) bLFrom.Y = bitmap.Height;

            // return bitmap if input == output
            if (uLFrom.X == uLTo.X && uRFrom.Y == uLTo.Y && uRFrom.X == uRTo.X && uRFrom.Y == uRTo.Y &&
                bLFrom.X == bLTo.X && bLFrom.Y == bLTo.Y && bRFrom.X == bRTo.X &&
                bRFrom.Y == bRTo.Y) return bitmap.Bytes;

            long t0;
            
            // scale
            var factor = Preferences.ImageDistortionScalingFactor;
            if (factor != 1.0) {
                t0 = Time.CurrentTimeMillis();
                Logger.Log($"Scale Bitmap (factor: {(double)factor}): {bitmap.Width}x{bitmap.Height} -> {(int) (bitmap.Width / factor)}x{(int) (bitmap.Height / factor)}");
                bitmap.ScalePixels(bitmap = new SKBitmap((int) (bitmap.Width / factor), (int) (bitmap.Height / factor)), SKFilterQuality.High);
                uLFrom.X /= factor;
                uLTo.X /= factor;
                uRFrom.X /= factor;
                uRTo.X /= factor;
                bLFrom.X /= factor;
                bLTo.X /= factor;
                bRFrom.X /= factor;
                bRTo.X /= factor;
            
                uLFrom.Y /= factor;
                uLTo.Y /= factor;
                uRFrom.Y /= factor;
                uRTo.Y /= factor;
                bLFrom.Y /= factor;
                bLTo.Y /= factor;
                bRFrom.Y /= factor;
                bRTo.Y /= factor;
                Logger.Log("Scaling:", Time.CurrentTimeMillis() - t0);
            
                // double check scaled input values
                if (uLFrom.X < 0) uLFrom.X = 0;
                if (uLFrom.Y < 0) uLFrom.Y = 0;
                if (bRFrom.X > bitmap.Width) bRFrom.X = bitmap.Width;
                if (bRFrom.Y > bitmap.Height) bRFrom.Y = bitmap.Height;
                if (uRFrom.Y < 0) uRFrom.Y = 0;
                if (uRFrom.X > bitmap.Width) uRFrom.X = bitmap.Width;
                if (bLFrom.X < 0) bLFrom.X = 0;
                if (bLFrom.Y > bitmap.Height) bLFrom.Y = bitmap.Height;
            } else {
                Logger.Log("No Scaling applied (factor == 1.0)");
            }
            
            // create matrices
            t0 = Time.CurrentTimeMillis();
            var matrixFrom = Matrix3X3.HomogeneousTransformation(uLFrom, uRFrom, bRFrom, bLFrom);
            var matrixTo = Matrix3X3.HomogeneousTransformation(uLTo, uRTo, bRTo, bLTo);
            matrixTo.Inverse();
            var matrixResult = matrixFrom.MultiplyMatrix(matrixTo);
            Logger.Log("Creating matrices:", Time.CurrentTimeMillis() - t0);

            // bitmap info
            var bmpWidth = bitmap.Width;
            var bmpHeight = bitmap.Height;

            Logger.Log($"Distorting Bitmap ({bmpWidth}x{bmpHeight})");

            /* Attempt on Cropping the image first
            var lowestX = (int) new List<double> {uLFrom.X, uRFrom.X, bLFrom.X, bRFrom.X}.Min(i => i);
            var lowestY = (int) new List<double> {uLFrom.Y, uRFrom.Y, bLFrom.Y, bRFrom.Y}.Min(i => i);
            
            var highestX = (int) new List<double> {uLFrom.X, uRFrom.X, bLFrom.X, bRFrom.X}.Max(i => i);
            var highestY = (int) new List<double> {uLFrom.Y, uRFrom.Y, bLFrom.Y, bRFrom.Y}.Max(i => i);

            var minWidth = highestX - lowestX;
            var minHeight = highestY - lowestY;
            */ 
            
            /* Attempt on using SKMatrix
            var skMatrix = new SKMatrix {
                ScaleX = (float) matrixResult.GetValue(1, 1),
                ScaleY = (float) matrixResult.GetValue(2, 2),
                SkewX = (float) matrixResult.GetValue(1, 2),
                SkewY = (float) matrixResult.GetValue(2, 1),
                TransX = (float) matrixResult.GetValue(1, 3),
                TransY = (float) matrixResult.GetValue(2, 3),
                Persp0 = (float) matrixResult.GetValue(3, 1),
                Persp1 = (float) matrixResult.GetValue(3, 2),
                Persp2 = (float) matrixResult.GetValue(3, 3),
            };
            
            var resultBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
            var canvas = new SKCanvas(resultBitmap);
            canvas.DrawBitmap(bitmap, new SKPoint(0, 0));
            canvas.SetMatrix(skMatrix);
            canvas.Flush();
            */ 
            
            t0 = Time.CurrentTimeMillis();
            var resultBitmap = new SKBitmap(bmpWidth, bmpHeight);
            Logger.Log("Creating the resultBitmap:", Time.CurrentTimeMillis() - t0);
            
            // set the pixels
            t0 = Time.CurrentTimeMillis();
            unsafe {
                var pixels = bitmap.Pixels;
                var dstPtr = (byte*) resultBitmap.GetPixels().ToPointer();
                for (var y = 0; y < bmpHeight; y++) {
                    for (var x = 0; x < bmpWidth; x++) {
                        var (x1, y1) = matrixResult.Update(x, y);
                        var pixel = pixels[bitmap.Width * y1 + x1];
                        *dstPtr++ = pixel.Blue;
                        *dstPtr++ = pixel.Green;
                        *dstPtr++ = pixel.Red;
                        *dstPtr++ = pixel.Alpha;
                    }
                }
            }
            Logger.Log("Setting the Pixels:", Time.CurrentTimeMillis() - t0);
            
            t0 = Time.CurrentTimeMillis();
            var resultBytes = resultBitmap.GetPixels();
            var resultImage = SKImage.FromPixelCopy(resultBitmap.Info, resultBytes);
            Logger.Log("Creating an SKImage:", Time.CurrentTimeMillis() - t0);
      
            t0 = Time.CurrentTimeMillis();
            var encodedResult = resultImage.Encode(Preferences.ImageManipulationResultFormat, Preferences.ImageManipulationResultQuality);
            Logger.Log($"Encoding ({(SKEncodedImageFormat)Preferences.ImageManipulationResultFormat}, ({(int)Preferences.ImageManipulationResultQuality})):", Time.CurrentTimeMillis() - t0);
            
            t0 = Time.CurrentTimeMillis();
            var encodedStream = encodedResult.AsStream();
            Logger.Log("Getting the Stream:", Time.CurrentTimeMillis() - t0);
            
            t0 = Time.CurrentTimeMillis();
            var encodedBytes = encodedResult.ToArray();
            Logger.Log("Getting the Array:", Time.CurrentTimeMillis() - t0);
            
            // set the image source if an image was provided
            t0 = Time.CurrentTimeMillis();
            if (image != null) {
                image.Source = ImageSource.FromStream(() => encodedStream);
            }
            Logger.Log("Setting the image source:", Time.CurrentTimeMillis() - t0);
            
            // return the encoded bytes
            return encodedBytes;
        }
    }