using System;

namespace Recognizer.Android.Library
{
    internal class ColorConverterHelper
    {
        public static void RGBToHSL(int red, int green, int blue, float[] hsl)
        {
            float r = (red / 255f);
            float g = (green / 255f);
            float b = (blue / 255f);

            float min = Math.Min(Math.Min(r, g), b);
            float max = Math.Max(Math.Max(r, g), b);
            float delta = max - min;

            float h = 0;
            float s = 0;
            float l = (float)((max + min) / 2.0f);

            if (delta != 0)
            {
                if (l < 0.5f)
                {
                    s = (float)(delta / (max + min));
                }
                else
                {
                    s = (float)(delta / (2.0f - max - min));
                }

                if (r == max)
                {
                    h = (g - b) / delta;
                }
                else if (g == max)
                {
                    h = 2f + (b - r) / delta;
                }
                else if (b == max)
                {
                    h = 4f + (r - g) / delta;
                }
            }

            h = h * 60f;
            if (h < 0)
                h += 360;

            hsl[0] = h;
            hsl[1] = s;
            hsl[2] = l;
        }
    }
}