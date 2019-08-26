﻿using Android.Graphics;

namespace RecognizerTestApp
{
    public class RecognitionResult
    {
        public RecognitionResult()
        {
            Invalidate();
        }

        public string ResultText { get; set; }
        public double Quality { get; set; }
        public Rect BoundingBox { get; set; }

        public void Invalidate()
        {
            ResultText = string.Empty;
            Quality = 0;
            BoundingBox = new Rect(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        }
    }
}