using Android.Graphics;

namespace RecognizerTestApp.Helpers
{
    public class BitmapOperator
    {
        public const int MIN_BRIGHTNESS = -255;
        public const int MAX_BRIGHTNESS = 255;
        public const float MIN_CONTRAST = 0;
        public const float MAX_CONTRAST = 10;

        /**
        * 
        * @param bmp input bitmap
        * @param contrast 0..10 1 is default
        * @param brightness -255..255 0 is default
        * @return new bitmap
        */
        public static Bitmap ChangeBitmapContrastBrightness(Bitmap bmp, float contrast, float brightness)
        {
            var cm = new ColorMatrix(new[]
            {
                contrast, 0, 0, 0, brightness,
                0, contrast, 0, 0, brightness,
                0, 0, contrast, 0, brightness,
                0, 0, 0, 1, 0
            });

            var ret = Bitmap.CreateBitmap(bmp.Width, bmp.Height, bmp.GetConfig());

            var canvas = new Canvas(ret);

            var paint = new Paint();
            paint.SetColorFilter(new ColorMatrixColorFilter(cm));
            canvas.DrawBitmap(bmp, 0, 0, paint);

            return ret;
        }


        public static Bitmap TurnToGrayScale(Bitmap bmp)
        {
            var cm = new ColorMatrix(new[]
            {
                0.5f, 0.5f, 0.5f, 0, 0,
                0.5f, 0.5f, 0.5f, 0, 0,
                0.5f, 0.5f, 0.5f, 0, 0,
                0, 0, 0, 1, 0
            });

            var ret = Bitmap.CreateBitmap(bmp.Width, bmp.Height, bmp.GetConfig());

            var canvas = new Canvas(ret);

            var paint = new Paint();
            paint.SetColorFilter(new ColorMatrixColorFilter(cm));
            canvas.DrawBitmap(bmp, 0, 0, paint);

            return ret;
        }

        public static Bitmap DrawDitheringBorder(Bitmap bmp, int borderSize)
        {
            var canvas = new Canvas(bmp);

            var paint = new Paint();
            paint.SetStyle(Paint.Style.Stroke);
            paint.Color = Color.White;
            paint.StrokeWidth = borderSize;
            canvas.DrawRect(0, 0, bmp.Width, bmp.Height, paint);

            return bmp;
        }
    }
}