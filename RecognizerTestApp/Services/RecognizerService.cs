using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Firebase;
using Firebase.ML.Vision;
using Firebase.ML.Vision.Common;
using Firebase.ML.Vision.Text;
using RecognizerTestApp.Helpers;
using RecognizerTestApp.Settings;
using Tesseract.Droid;
using Environment = Android.OS.Environment;
using Path = System.IO.Path;

namespace RecognizerTestApp.Services
{
    public class RecognizerService
    {
        private const int MAX_TEXT_LENGTH = 160;

        private const int RECOGNITION_VALUE = 80;

        private readonly RecognitionResult _finalRecognitionResult = new RecognitionResult();

        private string _currentReferenceString;

        private string[] _referenceStrings = new[]
        {
            "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower(),
            "печатник создал форм шрифтов распечатки обр успешно переж веков, но и пере".Replace(" ", "").ToLower()
        };

        private double[][] _referenceHues = {
            new double[]{180,274}, //blue
            new double[]{185,195} //violette
        };

        private readonly RecognitionResult _tempRecognitionResult = new RecognitionResult();
        private Context _appContext;

        private int[] _bitmapPixelArray;

        private FirebaseApp _defaultFirebaseApp;
        private ProcessImageListener _firebaseProcessImageListener;
        private FirebaseVisionTextRecognizer _firebaseVisionTextDetector;
        private RecognizingActor _recognizingActor;

        private TesseractApi _tesseractApi;
        private Rect _textureRect;
        private Bitmap _textureViewBitmap;

        private Bitmap _visionBitmap;

        public float CurrentContrast = 0.9f;
        private int[] _currentBitmapPixelArray;

        public volatile bool IsInitialized;

        public volatile bool RecognizingTextInProgress;
        public volatile bool SearchComplete;

        public event EventHandler<Bitmap> VisionImageUpdated;
        public event EventHandler<RecognitionResult> RecordWasFound;

        public void StartRecognizingText()
        {
            RecognizingTextInProgress = true;
        }

        public void StopRecognizingText()
        {
            RecognizingTextInProgress = false;
        }

        private void InitFirebase()
        {
            try
            {
                _defaultFirebaseApp = FirebaseApp.InitializeApp(_appContext);

                _firebaseProcessImageListener = new ProcessImageListener();

                var options = new FirebaseVisionCloudTextRecognizerOptions.Builder()
                    .SetLanguageHints(new List<string> {"ru"})
                    .SetModelType(FirebaseVisionCloudTextRecognizerOptions.DenseModel)
                    .Build();

                var firebaseVision = FirebaseVision.GetInstance(_defaultFirebaseApp);

                _firebaseVisionTextDetector =
                    firebaseVision.GetCloudTextRecognizer(options);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task Init(Context appContext, Rect rect)
        {
            _appContext = appContext ?? throw new ArgumentNullException(nameof(appContext));
            _textureRect = rect ?? throw new ArgumentNullException(nameof(rect));

            _bitmapPixelArray = new int[rect.Width() * rect.Height()];
            _currentBitmapPixelArray = new int[_textureRect.Width() * _textureRect.Height()];

            InitFirebase();
            await InitTesseract();

            IsInitialized = true;
        }

        private async Task InitTesseract()
        {
            _tesseractApi = new TesseractApi(_appContext, AssetsDeployment.OncePerVersion);

            await _tesseractApi.Init("rus");

            _tesseractApi.SetBlacklist(":,.123456789`~!@#$;%^?&*()-_+=|/<>}{]['…“№*+-¡©´·ˆˇˈˉˊˋˎˏ‘„‚.’—");
        }

        public async Task<RecognitionResult> RecognizeText(Bitmap textureViewBitmap)
        {
            try
            {
                _textureViewBitmap = textureViewBitmap;

                _textureViewBitmap.GetPixels(_bitmapPixelArray, 0, _textureRect.Width(), 0, 0, _textureRect.Width(),
                    _textureRect.Height());

                _tempRecognitionResult.Invalidate();
                _finalRecognitionResult.Invalidate();

                await Task.Factory.StartNew(GetVisionBitmap);

                await TesseractTextRecognizing(_visionBitmap);
                UpdateFinalRecognitionResult();

                if (_finalRecognitionResult.Quality >= RECOGNITION_VALUE)
                {
                    var result = new RecognitionResult(_finalRecognitionResult);
                    RecordWasFound?.Invoke(this, result);
                }

                if (GeneralSettings.ShowVisionImage) VisionImageUpdated?.Invoke(this, _visionBitmap);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                RecognizingTextInProgress = false;
            }

            return _finalRecognitionResult;
        }

        private void UpdateFinalRecognitionResult()
        {
            _finalRecognitionResult.Quality = _tempRecognitionResult.Quality;
            _finalRecognitionResult.ResultText = _tempRecognitionResult.ResultText;
            _finalRecognitionResult.OriginalText = _tempRecognitionResult.OriginalText;
            _finalRecognitionResult.BoundingBox = new Rect(_tempRecognitionResult.BoundingBox);
        }

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

        private void GetVisionBitmap()
        {
            //var timer = Stopwatch.StartNew(); 

             _visionBitmap = _textureViewBitmap;
            var h = _visionBitmap.Height;
            var w = _visionBitmap.Width;

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

                    RGBToHSL(red, green, blue, hsl);

                    if (hsl[2] < 0.08 || hsl[2] > 0.90
                                      || hsl[1] < 0.2)
                    {
                        continue;
                    }

                    foreach (var referenceHue in _referenceHues)
                    {
                        if (hsl[0] >= referenceHue[0] && hsl[0] <= referenceHue[1])
                        {
                            _currentBitmapPixelArray[j * w + i] = _bitmapPixelArray[j * w + i];
                            break;
                        }
                    }
                }
            }

            _visionBitmap.SetPixels(_currentBitmapPixelArray, 0, _textureRect.Width(), 0, 0, _textureRect.Width(),
                _textureRect.Height());

            _visionBitmap = BitmapOperator.TurnToGrayScale(_visionBitmap);
            _visionBitmap = BitmapOperator.ChangeBitmapContrastBrightness(_visionBitmap, CurrentContrast, 0);

            //timer.Stop();
        }

        private async Task FirebaseTextRecognizing(Bitmap croppedBitmap)
        {
            var tcs = new TaskCompletionSource<bool>();

            void OnTextRecognized(object sender, string recognizedText)
            {
                _firebaseProcessImageListener.TextRecognized -= OnTextRecognized;

                SetRecognitionResultFromText(recognizedText.Length < MAX_TEXT_LENGTH ? recognizedText : string.Empty);

                tcs.SetResult(true);
            }

            _firebaseProcessImageListener.TextRecognized += OnTextRecognized;

            var image = FirebaseVisionImage.FromBitmap(croppedBitmap);

            var task = _firebaseVisionTextDetector.ProcessImage(image);
            task.AddOnSuccessListener(_firebaseProcessImageListener);
            task.AddOnCompleteListener(_firebaseProcessImageListener);
            task.AddOnFailureListener(_firebaseProcessImageListener);

            await tcs.Task;
        }

        private async Task TesseractTextRecognizing(Bitmap croppedBitmap)
        {
            try
            {
                if (_tesseractApi != null && _tesseractApi.Initialized)
                {
                    var success = await _tesseractApi.Recognise(croppedBitmap);
                    var text = success ? _tesseractApi.Text : string.Empty;

                    SetRecognitionResultFromText(text.Length < MAX_TEXT_LENGTH ? text : string.Empty);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void SetRecognitionResultFromText(string originalText)
        {
            try
            {
                double quality = 0;

                var text = originalText.Replace("\n", "").Replace(" ", "").ToLower();

                if (text.Length > 0)
                {
                    foreach (string str in _referenceStrings)
                    {
                        double diff = StringDistance.GetDamerauLevenshteinDistance(str, text);
                        var currQuality = (str.Length - diff) / str.Length * 100;

                        if (currQuality > quality)
                        {
                            _currentReferenceString = str;
                            quality = currQuality;
                        }
                    }
                }

                _tempRecognitionResult.OriginalText = originalText;
                _tempRecognitionResult.ResultText = text;
                _tempRecognitionResult.Quality = quality;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void SetRecognizingActor(RecognizingActor actor)
        {
            _recognizingActor = actor;
        }

        public RecognizingActor GetRecognizingActor()
        {
            return _recognizingActor;
        }

        public bool CanBeRecognizedFromServer()
        {
            return _firebaseVisionTextDetector != null;
        }
    }
}