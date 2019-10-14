using System;
using System.Drawing;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using RecognizerTestApp.Helpers;
using Tesseract.Droid;
using Color = Android.Graphics.Color;

namespace Recognizer.Android.Library
{
    public class AuCodeLibrary
    {
        private const int MAX_TEXT_LENGTH = 160;

        private const float APPLIED_CONTRAST = 0.99f;

        private TesseractApi _tesseractApi;

        private Context _appContext;
        private int[] _bitmapPixelArray;
        private int[] _currentBitmapPixelArray;

        public async Task Init(Context context)
        {
            _appContext = context;

            await InitTesseract();
        }

        private async Task InitTesseract()
        {
            _tesseractApi = new TesseractApi(_appContext, AssetsDeployment.OncePerVersion);
            
            await _tesseractApi.Init("rus");

            _tesseractApi.SetBlacklist(":,.`~!@#$;%^?&*()-_+=|/<>}{]['…“№*+-¡©´·ˆˇˈˉˊˋˎˏ‘„‚.’—");
        }

        private async Task<string> TesseractTextRecognizing(Bitmap croppedBitmap)
        {
            try
            {
                if (_tesseractApi != null && _tesseractApi.Initialized)
                {
                    var success = await _tesseractApi.Recognise(croppedBitmap);
                    var text = success ? _tesseractApi.Text : string.Empty;

                    return text.Length < MAX_TEXT_LENGTH ? text : string.Empty;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return string.Empty;
        }

        public async Task<string> RecognizeText(Bitmap bitmap, double[][] textColorHues, global::Android.Util.Size size)
        {
            var h = bitmap.Height;
            var w = bitmap.Width;

            if (_bitmapPixelArray == null || _bitmapPixelArray.Length != w * h)
            {
                _bitmapPixelArray = new int[w * h];
                _currentBitmapPixelArray = new int[w * h];
            }

            bitmap.GetPixels(_bitmapPixelArray, 0, w, 0, 0, w,
                h);

            for (var i = 0; i < _currentBitmapPixelArray.Length; i++)
            {
                _currentBitmapPixelArray[i] = Color.White;
            }

            int pixelColor = Color.White;
            int red = 0;
            int green = 0;
            int blue = 0;

            var hsl = new float[3];
            for (var j = 0; j < h; j++)
            {
                for (var i = 0; i < w; i++)
                {
                    pixelColor = _bitmapPixelArray[j * w + i];

                    red = Color.GetRedComponent(pixelColor);
                    green = Color.GetGreenComponent(pixelColor);
                    blue = Color.GetBlueComponent(pixelColor);

                    ColorConverterHelper.RGBToHSL(red, green, blue, hsl);

                    if (hsl[2] < 0.08 || hsl[2] > 0.90
                                      || hsl[1] < 0.2)
                    {
                        continue;
                    }

                    foreach (var referenceHue in textColorHues)
                    {
                        if (hsl[0] >= referenceHue[0] && hsl[0] <= referenceHue[1])
                        {
                            _currentBitmapPixelArray[j * w + i] = _bitmapPixelArray[j * w + i];
                            break;
                        }
                    }
                }
            }

            bitmap.SetPixels(_currentBitmapPixelArray, 0, w, 0, 0, w,
                h);

            bitmap = BitmapOperator.TurnToGrayScale(bitmap);
            bitmap = BitmapOperator.ChangeBitmapContrastBrightness(bitmap, APPLIED_CONTRAST, 0);

            string result = await TesseractTextRecognizing(bitmap);

            return result;
        }     
    }
}