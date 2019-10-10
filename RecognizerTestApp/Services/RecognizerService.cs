using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        //private readonly string _referenceString =
        //    "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower();

        private readonly string _referenceString =
            "печатник создал форм шрифтов распечатки обр успешно переж веков, но и пере".Replace(" ", "").ToLower();

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

        public volatile bool IsInitialized;

        public volatile bool RecognizingTextInProgress;
        public volatile bool SearchComplete;

        public event EventHandler<Bitmap> VisionImageUpdated;
        public event EventHandler<RecognitionResult> RecordWasFound;

        public void ExportBitmapAsPNG(Bitmap bitmap)
        {
            var sdCardPath = Environment.ExternalStorageDirectory.AbsolutePath;
            var filePath = Path.Combine(sdCardPath, "test.png");
            var stream = new FileStream(filePath, FileMode.Create);
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
            stream.Close();
        }

        private Bitmap ImportFromFile()
        {
            var sdCardPath = Environment.ExternalStorageDirectory.AbsolutePath;
            var filePath = Path.Combine(sdCardPath, "render.jpg");
            return BitmapFactory.DecodeFile(filePath);
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

            InitFirebase();
            await InitTesseract();

            IsInitialized = true;
        }

        private async Task InitTesseract()
        {
            _tesseractApi = new TesseractApi(_appContext, AssetsDeployment.OncePerVersion);

            await _tesseractApi.Init("rus");

            _tesseractApi.SetBlacklist("`~!@#$;%^?&*()-_+=|/<>}{]['…“№*+-¡©´·ˆˇˈˉˊˋˎˏ‘„‚.’—123456789");
        }

        public async Task<RecognitionResult> RecognizeText(Bitmap textureViewBitmap)
        {
            //Stopwatch timer = Stopwatch.StartNew();

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

            return _finalRecognitionResult;
        }

        private void UpdateFinalRecognitionResult()
        {
            _finalRecognitionResult.Quality = _tempRecognitionResult.Quality;
            _finalRecognitionResult.ResultText = _tempRecognitionResult.ResultText;
            _finalRecognitionResult.OriginalText = _tempRecognitionResult.OriginalText;
            _finalRecognitionResult.BoundingBox = new Rect(_tempRecognitionResult.BoundingBox);
        }

        private void GetVisionBitmap()
        {
            var timer = Stopwatch.StartNew();

            _visionBitmap = _textureViewBitmap;

            var currentBitmapPixelArray = new int[_textureRect.Width() * _textureRect.Height()];
            for (var i = 0; i < currentBitmapPixelArray.Length; i++) currentBitmapPixelArray[i] = Color.White;

            var number_of_color = 0;
            var hsv = new float[3];
            for (var j = 0; j < _visionBitmap.Height; j++)
            for (var i = 0; i < _visionBitmap.Width; i++)
            {
                var pixelColor = _bitmapPixelArray[j * _visionBitmap.Width + i];

                //var lume = ColorUtils.CalculateLuminance(pixelColor);
                //if (lume < 0.10 || lume > 0.6)
                //{
                //    _visionBitmap.SetPixel(i, j, Color.White);
                //    continue;
                //}

                var red = Color.GetRedComponent(pixelColor);
                var green = Color.GetGreenComponent(pixelColor);
                var blue = Color.GetBlueComponent(pixelColor);
                Color.RGBToHSV(red, green, blue, hsv);

                if (hsv[2] < 0.15 || hsv[2] > 0.90
                                  // || hsv[1] < 0.2
                                  || hsv[0] < 180 || hsv[0] > 274)
                    continue;

                currentBitmapPixelArray[j * _visionBitmap.Width + i] = _bitmapPixelArray[j * _visionBitmap.Width + i];
                number_of_color++;
            }

            _visionBitmap.SetPixels(currentBitmapPixelArray, 0, _textureRect.Width(), 0, 0, _textureRect.Width(),
                _textureRect.Height());

            _visionBitmap = BitmapOperator.TurnToGrayScale(_visionBitmap);
            _visionBitmap = BitmapOperator.ChangeBitmapContrastBrightness(_visionBitmap, CurrentContrast, 0);

            timer.Stop();
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
                    double diff = StringDistance.GetDamerauLevenshteinDistance(_referenceString, text);
                    quality = (_referenceString.Length - diff) / _referenceString.Length * 100;
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