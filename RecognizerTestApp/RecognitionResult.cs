using Android.Graphics;

namespace RecognizerTestApp
{
    public class RecognitionResult
    {
        public RecognitionResult()
        {
            Invalidate();
        }

        public RecognitionResult(RecognitionResult result)
        {
            ResultText = result.ResultText;
            OriginalText = result.OriginalText;
            Quality = result.Quality;
            BoundingBox = result.BoundingBox;
        }

        public string ResultText { get; set; }
        public string OriginalText { get; set; }
        public double Quality { get; set; }
        public Rect BoundingBox { get; set; }

        public void Invalidate()
        {
            ResultText = string.Empty;
            OriginalText = string.Empty;
            Quality = 0;
            BoundingBox = new Rect(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        }
    }
}