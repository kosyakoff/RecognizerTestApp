using System;
using System.Collections.Generic;
using System.Text;

namespace Recognizer.Core
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
        }

        public string ResultText { get; set; }
        public string OriginalText { get; set; }
        public double Quality { get; set; }

        public void Invalidate()
        {
            ResultText = string.Empty;
            OriginalText = string.Empty;
            Quality = 0;
        }
    }
}
