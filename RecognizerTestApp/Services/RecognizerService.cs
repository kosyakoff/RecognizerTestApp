using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Firebase;
using Firebase.ML.Vision;
using Firebase.ML.Vision.Common;
using Firebase.ML.Vision.Text;
using Tesseract.Droid;
using Environment = Android.OS.Environment;
using Path = System.IO.Path;

namespace RecognizerTestApp.Services
{
    public class RecognizerService
    {
        private const int PRIMARY_ACCURACY = 15;
        private const int BIGGER_ACCURACY = 20;
        private const int SMALLER_ACCURACY = 10;

        private readonly Rect _bBox = new Rect();

        private readonly RecognitionResult _finalRecognitionResult = new RecognitionResult();

        private readonly Color _referenceColor = new Color(65, 113, 127);

        private readonly string _referenceString =
            "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower();

        private readonly RecognitionResult _tempRecognitionResult = new RecognitionResult();
        private Context _appContext;

        private int[] _bitmapPixelArray;
        private Size _cameraSize;

        private int _currentAccuracy = PRIMARY_ACCURACY;

        private FirebaseApp _defaultFirebaseApp;
        private ProcessImageListener _firebaseProcessImageListener;
        private FirebaseVisionTextRecognizer _firebaseVisionTextDetector;
        private RecognizingActor _recognizingActor;

        public volatile bool _recognizingTextInProgress;

        public volatile  bool  IsInitialized;

        //private readonly Timer _updateTimer = new Timer(500)
        //{
        //    AutoReset = false
        //};

        private int _redrawCount;
        // private bool _shouldUpdate = true;

        private List<Color> _referenceColors = new List<Color>
        {
            new Color(65, 113, 127),
            new Color(83, 116, 125),
            new Color(59, 96, 105)
        };

        private TesseractApi _tesseractApi;
        private Bitmap _textureViewBitmap;

        public bool RecognizingTextInProgress
        {
            get => _recognizingTextInProgress;
            set => _recognizingTextInProgress = value;
        }

        public event EventHandler<Rect> OverlayRectUpdated;

        private void GetCroppingBoundingBox(int updateBitmapHeight, int updateBitmapWidth)
        {
            _bBox.Set(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);

            for (var j = 0; j < updateBitmapHeight; j++)
            for (var i = 0; i < updateBitmapWidth; i++)
            {
                var pixelColor = _bitmapPixelArray[j * updateBitmapWidth + i];

                var red = Color.GetRedComponent(pixelColor);

                // applicableColors.AddRange(_referenceColors.Where(x => Math.Abs(x.R - red) < _currentAccuracy));

                //if (Math.Abs(_referenceColor.R - red) >= _currentAccuracy)
                //    //  if (!applicableColors.Any())
                //    continue;

                var green = Color.GetGreenComponent(pixelColor);

                //applicableColors = applicableColors.Except(applicableColors.Where(x => Math.Abs(x.G - green) >= _currentAccuracy)).ToList();


                //if (Math.Abs(_referenceColor.G - green) >= _currentAccuracy)
                //    //if (!applicableColors.Any())
                //    continue;

                var blue = Color.GetBlueComponent(pixelColor);

                //applicableColors = applicableColors.Except(applicableColors.Where(x => Math.Abs(x.B - blue) >= _currentAccuracy)).ToList();

                //if (Math.Abs(_referenceColor.B - green) >= _currentAccuracy)
                //    //if (!applicableColors.Any())
                //    continue;

                var dbl_test_red = Math.Pow((double) _referenceColor.R - red, 2.0);
                var dbl_test_green = Math.Pow((double) _referenceColor.G - green, 2.0);
                var dbl_test_blue = Math.Pow((double) _referenceColor.B - blue, 2.0);

                var distance = Math.Sqrt(dbl_test_blue + dbl_test_green + dbl_test_red);

                if (distance < _currentAccuracy) UpdateBoundingBox(i, j);
            }
        }

        private void UpdateBoundingBox(int x, int y)
        {
            if (_bBox.Left > x)
                _bBox.Left = x;
            if (_bBox.Right < x)
                _bBox.Right = x;

            if (_bBox.Top > y)
                _bBox.Top = y;
            if (_bBox.Bottom < y)
                _bBox.Bottom = y;
        }

        //public static Bitmap RotateBitmap(Bitmap source, float angle)
        //{
        //    Matrix matrix = new Matrix();
        //    matrix.PostRotate(angle);
        //    return Bitmap.CreateBitmap(source, 0, 0, source.Width, source.Height, matrix, true);
        //}

        private void ExportBitmapAsPNG(Bitmap bitmap)
        {
            var sdCardPath = Environment.ExternalStorageDirectory.AbsolutePath;
            var filePath = Path.Combine(sdCardPath, "test.png");
            var stream = new FileStream(filePath, FileMode.Create);
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
            stream.Close();
        }

        private void InitFirebase()
        {
            _defaultFirebaseApp = FirebaseApp.InitializeApp(_appContext);

            _firebaseProcessImageListener = new ProcessImageListener();

            var options = new FirebaseVisionCloudTextRecognizerOptions.Builder()
                .SetLanguageHints(new List<string> {"ru"})
                .SetModelType(FirebaseVisionCloudTextRecognizerOptions.DenseModel)
                .Build();

            _firebaseVisionTextDetector =
                FirebaseVision.GetInstance(_defaultFirebaseApp).GetCloudTextRecognizer(options);
        }

        public async Task Init(Context appContext, Size size)
        {
            //_updateTimer.Elapsed += (sender, e) => { _shouldUpdate = true; };

            _appContext = appContext ?? throw new ArgumentNullException(nameof(appContext));
            _cameraSize = size ?? throw new ArgumentNullException(nameof(size));

            _bitmapPixelArray = new int[_cameraSize.Width * _cameraSize.Height];

            InitFirebase();
            await InitTesseract();

            IsInitialized = true;
        }

        private async Task InitTesseract()
        {
            _tesseractApi = new TesseractApi(_appContext, AssetsDeployment.OncePerVersion);

            await _tesseractApi.Init("rus");
        }

        public async Task<RecognitionResult> RecognizeText(Bitmap textureViewBitmap)
        {
            //Stopwatch timer = Stopwatch.StartNew();

            RecognizingTextInProgress = true;

            _textureViewBitmap = textureViewBitmap;

            _textureViewBitmap.GetPixels(_bitmapPixelArray, 0, _cameraSize.Width, 0, 0, _cameraSize.Width,
                _cameraSize.Height);

            _tempRecognitionResult.Invalidate();
            _finalRecognitionResult.Invalidate();

            _currentAccuracy = PRIMARY_ACCURACY;

            await PerformRecognizing(_textureViewBitmap);

            UpdateFinalRecognitionResult();

            if (_tempRecognitionResult.Quality > 50 && _tempRecognitionResult.Quality < 70)
            {
                _currentAccuracy = BIGGER_ACCURACY;
                await PerformRecognizing(_textureViewBitmap);

                if (_tempRecognitionResult.Quality > _finalRecognitionResult.Quality)
                {
                    UpdateFinalRecognitionResult();
                }
                else if (_tempRecognitionResult.Quality > 50 && _tempRecognitionResult.Quality < 70)
                {
                    _currentAccuracy = SMALLER_ACCURACY;
                    await PerformRecognizing(_textureViewBitmap);

                    if (_tempRecognitionResult.Quality > _finalRecognitionResult.Quality)
                        UpdateFinalRecognitionResult();
                }
            }

            var overlayRect = new Rect(
                _cameraSize.Height - _finalRecognitionResult.BoundingBox.Bottom,
                _finalRecognitionResult.BoundingBox.Left,
                _cameraSize.Height - _finalRecognitionResult.BoundingBox.Top,
                _bBox.Right);
            OverlayRectUpdated?.Invoke(this, overlayRect);

            textureViewBitmap.Dispose();

            //timer.Stop();
            //TimeSpan timespan = timer.Elapsed;

            _recognizingTextInProgress = false;

            return _finalRecognitionResult;
        }

        private void UpdateFinalRecognitionResult()
        {
            _finalRecognitionResult.Quality = _tempRecognitionResult.Quality;
            _finalRecognitionResult.ResultText = _tempRecognitionResult.ResultText;
            _finalRecognitionResult.BoundingBox = new Rect(_tempRecognitionResult.BoundingBox);
        }

        private async Task PerformRecognizing(Bitmap updatedBitmap)
        {
            GetCroppingBoundingBox(_cameraSize.Height, _cameraSize.Width);

            if (!_bBox.IsEmpty)
            {
                Bitmap croppedBitmap;
                using (var matrix = new Matrix())
                {
                    matrix.PostRotate(90);

                    croppedBitmap =
                        Bitmap.CreateBitmap(updatedBitmap,
                            _bBox.Left,
                            _bBox.Top,
                            _bBox.Width(),
                            _bBox.Height(),
                            matrix,
                            false);
                }

                switch (_recognizingActor)
                {
                    case RecognizingActor.Client:
                        await TesseractTextRecognizing(croppedBitmap);
                        break;
                    case RecognizingActor.Server:
                        await FirebaseTextRecognizing(croppedBitmap);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                //ExportBitmapAsPNG(cropped);

                croppedBitmap.Dispose();
            }
            else
            {
                _tempRecognitionResult.Invalidate();
            }
        }

        private async Task FirebaseTextRecognizing(Bitmap croppedBitmap)
        {
            var tcs = new TaskCompletionSource<bool>();

            void OnTextRecognized(object sender, string recognizedText)
            {
                _firebaseProcessImageListener.TextRecognized -= OnTextRecognized;

                recognizedText = recognizedText.Replace("\n", "").Replace(" ", "").ToLower();

                SetRecognitionResultFromText(recognizedText.Length < 160 ? recognizedText : string.Empty);

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
            if (_tesseractApi != null && _tesseractApi.Initialized)
            {
                var success = await _tesseractApi.Recognise(croppedBitmap);
                var text = success ? _tesseractApi.Text.Replace("\n", "").Replace(" ", "").ToLower() : string.Empty;

                SetRecognitionResultFromText(text.Length < 160 ? text : string.Empty);
            }
        }

        private void SetRecognitionResultFromText(string text)
        {
            double quality = 0;

            if (text.Length > 0)
            {
                double diff = StringDistance.GetDamerauLevenshteinDistance(_referenceString, text);
                quality = (_referenceString.Length - diff) / _referenceString.Length * 100;
            }

            _tempRecognitionResult.ResultText = text;
            _tempRecognitionResult.Quality = quality;
            _tempRecognitionResult.BoundingBox = new Rect(_bBox);
        }

        public void SetRecognizingActor(RecognizingActor actor)
        {
            _recognizingActor = actor;
        }
    }
}