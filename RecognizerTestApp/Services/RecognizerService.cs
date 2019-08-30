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
using Recognizer.Core.Resources.Strings;
using RecognizerTestApp.Helpers;
using RecognizerTestApp.Settings;
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
        private const int MAX_TEXT_LENGTH = 160;

        private const int MIN_TEXT_LENGTH = 50;

        private const int PRIMARY_CROP_BUFFER_VER = 10;
        private const int PRIMARY_CROP_BUFFER_HOR = 15;

        private int _currentBufferHorSize = PRIMARY_CROP_BUFFER_HOR;
        private int _currentBufferVerSize = PRIMARY_CROP_BUFFER_VER;

        private int _searchBoxDelimiter = 1;


        // public float CurrentContrast = 1;

        private readonly Rect _bBox = new Rect();

        private readonly RecognitionResult _finalRecognitionResult = new RecognitionResult();

        private readonly string _referenceString =
            "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower();

        private readonly RecognitionResult _tempRecognitionResult = new RecognitionResult();
        private Context _appContext;

        private int[] _bitmapPixelArray;
        private Size _textureSize;

        private int _currentAccuracy = PRIMARY_ACCURACY;

        private FirebaseApp _defaultFirebaseApp;
        private ProcessImageListener _firebaseProcessImageListener;
        private FirebaseVisionTextRecognizer _firebaseVisionTextDetector;
        private RecognizingActor _recognizingActor;

        private const int LOWER_RECOGNITION_VALUE = 65;
        private const int UPPER_RECOGNITION_VALUE = 79;

        public volatile bool RecognizingTextInProgress;
        public volatile bool SearchComplete;

        public volatile bool IsInitialized;

        private Color _selectedReferenceColor = new Color(65, 113, 127);

        private readonly List<Color> _allReferenceColors = new List<Color>
        {
            new Color(65, 113, 127),
            new Color(158, 74, 74),
        };

        private TesseractApi _tesseractApi;
        private Bitmap _textureViewBitmap;

        public event EventHandler<Rect> OverlayRectUpdated;
        public event EventHandler<Bitmap> CroppedImageUpdated;
        public event EventHandler<string> RecordWasFound;
        public event EventHandler SomeIncorrectTextWasFound;
        public event EventHandler NoTextWasFound;

        private bool _referenceColorIsSet;

        public int SearchBoxDelimiter
        {
            get { return _searchBoxDelimiter; }
            set { _searchBoxDelimiter = value; }
        }


        private void GetCroppingBoundingBox(int updateBitmapHeight, int updateBitmapWidth)
        {
            _bBox.Set(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            _referenceColorIsSet = false;

            try
            {
                for (var j = 0; j < updateBitmapHeight; j = j + SearchBoxDelimiter)
                {
                    for (var i = 0; i < updateBitmapWidth; i = i +SearchBoxDelimiter)
                    {
                        var pixelColor = _bitmapPixelArray[j * updateBitmapWidth + i];
                        var red = Color.GetRedComponent(pixelColor);

                        // applicableColors.AddRange(_allReferenceColors.Where(x => Math.Abs(x.R - red) < _currentAccuracy));

                        var green = Color.GetGreenComponent(pixelColor);

                        //applicableColors = applicableColors.Except(applicableColors.Where(x => Math.Abs(x.G - green) >= _currentAccuracy)).ToList();

                        var blue = Color.GetBlueComponent(pixelColor);

                        //applicableColors = applicableColors.Except(applicableColors.Where(x => Math.Abs(x.B - blue) >= _currentAccuracy)).ToList();

                        if (_referenceColorIsSet)
                        {
                            CheckReferenceColorBbox(j, i, red, green, blue);
                        }
                        else
                        {
                            foreach (var referenceColor in _allReferenceColors)
                            {
                                var dbl_test_red = Math.Pow((double)referenceColor.R - red, 2.0);
                                var dbl_test_green = Math.Pow((double)referenceColor.G - green, 2.0);
                                var dbl_test_blue = Math.Pow((double)referenceColor.B - blue, 2.0);

                                var distance = Math.Sqrt(dbl_test_blue + dbl_test_green + dbl_test_red);

                                if (distance < _currentAccuracy)
                                {
                                    _selectedReferenceColor = referenceColor;
                                    _referenceColorIsSet = true;

                                    CheckReferenceColorBbox(j, i, red, green, blue);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void CheckReferenceColorBbox(int j, int i, int red, int green, int blue)
        {
            var dbl_test_red = Math.Pow((double)_selectedReferenceColor.R - red, 2.0);
            var dbl_test_green = Math.Pow((double)_selectedReferenceColor.G - green, 2.0);
            var dbl_test_blue = Math.Pow((double)_selectedReferenceColor.B - blue, 2.0);

            var distance = Math.Sqrt(dbl_test_blue + dbl_test_green + dbl_test_red);

            if (distance < _currentAccuracy)
            {
                UpdateBoundingBox(i, j);
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

        private void ExportBitmapAsPNG(Bitmap bitmap)
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
                    .SetLanguageHints(new List<string> { "ru" })
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

        public async Task Init(Context appContext, Size size)
        {


            _appContext = appContext ?? throw new ArgumentNullException(nameof(appContext));
            _textureSize = size ?? throw new ArgumentNullException(nameof(size));

            _bitmapPixelArray = new int[_textureSize.Width * _textureSize.Height];

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

            try
            {
                _textureViewBitmap = textureViewBitmap;

                _textureViewBitmap.GetPixels(_bitmapPixelArray, 0, _textureSize.Width, 0, 0, _textureSize.Width,
                    _textureSize.Height);

                _tempRecognitionResult.Invalidate();
                _finalRecognitionResult.Invalidate();

                _currentBufferHorSize = PRIMARY_CROP_BUFFER_HOR;
                _currentBufferVerSize = PRIMARY_CROP_BUFFER_VER;

                _currentAccuracy = PRIMARY_ACCURACY;

                await PerformRecognizing(_textureViewBitmap);

                UpdateFinalRecognitionResult();

                if (GeneralSettings.UseProgressiveSearch)
                {
                    if (_tempRecognitionResult.Quality > LOWER_RECOGNITION_VALUE && _tempRecognitionResult.Quality < UPPER_RECOGNITION_VALUE)
                    {
                        _currentAccuracy = BIGGER_ACCURACY;
                        await PerformRecognizing(_textureViewBitmap);

                        if (_tempRecognitionResult.Quality > _finalRecognitionResult.Quality)
                        {
                            UpdateFinalRecognitionResult();
                        }
                        else
                        {
                            _currentAccuracy = SMALLER_ACCURACY;
                            await PerformRecognizing(_textureViewBitmap);

                            if (_tempRecognitionResult.Quality > _finalRecognitionResult.Quality)
                                UpdateFinalRecognitionResult();
                        }
                    }
                }

                OverlayRectUpdated?.Invoke(this, _tempRecognitionResult.BoundingBox);

                textureViewBitmap.Dispose();

                //timer.Stop();
                //TimeSpan timespan = timer.Elapsed;

                if (_finalRecognitionResult.Quality >= UPPER_RECOGNITION_VALUE)
                {
                    SearchComplete = true;
                    RecordWasFound?.Invoke(this, _finalRecognitionResult.OriginalText);
                }
                else
                {
                    if (_finalRecognitionResult.ResultText.Length >= MIN_TEXT_LENGTH)
                    {
                        SomeIncorrectTextWasFound?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        NoTextWasFound?.Invoke(this,EventArgs.Empty);
                    }
                }

                RecognizingTextInProgress = false;
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

        private async Task PerformRecognizing(Bitmap updatedBitmap)
        {
            try
            {
                GetCroppingBoundingBox(_textureSize.Height, _textureSize.Width);

                if (!_bBox.IsEmpty)
                {
                    await PerformRecognizingInternal(updatedBitmap);
                }
                else
                {
                    _tempRecognitionResult.Invalidate();
                    NoTextWasFound?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task PerformRecognizingInternal(Bitmap updatedBitmap)
        {
            try
            {
                ModifyCroppingBuffer(_currentBufferHorSize, _currentBufferVerSize);

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

                    croppedBitmap = BitmapOperator.DrawDitheringBorder(croppedBitmap, _currentBufferVerSize);
                    //croppedBitmap = BitmapOperator.TurnToGrayScale(croppedBitmap);
                    //croppedBitmap = BitmapOperator.ChangeBitmapContrastBrightness(croppedBitmap, CurrentContrast, 0);
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

                if (GeneralSettings.ShowCroppedImage)
                {
                    CroppedImageUpdated?.Invoke(this, croppedBitmap);
                }
                else
                {
                    croppedBitmap.Dispose();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void ModifyCroppingBuffer(int horVal, int verVal)
        {
            _bBox.Left = _bBox.Left - horVal >= 0 ? _bBox.Left - horVal : _bBox.Left;
            _bBox.Top = _bBox.Top - verVal >= 0 ? _bBox.Top - verVal : _bBox.Top;
            _bBox.Right = _bBox.Right + horVal < _textureSize.Width ? _bBox.Right + horVal : _bBox.Right;
            _bBox.Bottom = _bBox.Bottom + verVal < _textureSize.Height ? _bBox.Bottom + verVal : _bBox.Bottom;
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
                _tempRecognitionResult.BoundingBox = new Rect(_bBox);
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