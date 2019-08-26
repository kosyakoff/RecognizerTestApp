using System;
using System.Collections.Generic;
using System.IO;
using Android;
using Android.App;
using Android.Content.PM;
using Android.Gms.Tasks;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Firebase;
using Firebase.ML.Vision;
using Firebase.ML.Vision.Common;
using Firebase.ML.Vision.Text;
using Recognizer.Core.Resources.Strings;
using Tesseract.Droid;
using Camera = Android.Hardware.Camera;
using Environment = Android.OS.Environment;
using Exception = Java.Lang.Exception;
using Object = Java.Lang.Object;
using Path = System.IO.Path;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace RecognizerTestApp
{
    public class ProcessImageListener : Object, IOnSuccessListener,
        IOnCompleteListener, IOnFailureListener
    {
        public EventHandler<string> TextRecognized;

        public void OnComplete(Task task)
        {
        }

        public void OnFailure(Exception e)
        {
            TextRecognized?.Invoke(this, string.Empty);
        }

        public void OnSuccess(Object result)
        {
            var text = (FirebaseVisionText) result;
            TextRecognized?.Invoke(this, text.Text);
        }
    }

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

    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, TextureView.ISurfaceTextureListener
    {
        private const int REQUEST_CAMERA_ID = 1001;
        private const int REQUEST_WRITE_ID = 1002;
        private const int REQUEST_INTERNET_ID = 1003;

        private const int PRIMARY_ACCURACY = 15;
        private const int BIGGER_ACCURACY = 20;
        private const int SMALLER_ACCURACY = 10;

        private readonly Rect _bBox = new Rect();
        private readonly RecognitionResult _finalRecognitionResult = new RecognitionResult();

        private readonly Color _referenceColor = new Color(65, 113, 127);

        private readonly string _referenceString =
            "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower();

        private readonly RecognitionResult _tempRecognitionResult = new RecognitionResult();

        private int[] _bitmapPixelArray;

        private Camera _camera;

        private int _currentAccuracy = PRIMARY_ACCURACY;

        private FirebaseApp _defaultFirebaseApp;
        private ProcessImageListener _firebaseProcessImageListener;
        private FirebaseVisionTextRecognizer _firebaseVisionTextDetector;
        private OverlayView _overlayView;
        private Camera.Size _previewSize;

        private RecognizingActor _recognizingActor;

        public volatile bool _recognizingTextInProgress;

        //private readonly Timer _updateTimer = new Timer(500)
        //{
        //    AutoReset = false
        //};

        private int _redrawCount;

        private List<Color> _referenceColors = new List<Color>
        {
            new Color(65, 113, 127),
            new Color(83, 116, 125),
            new Color(59, 96, 105)
        };

        private TesseractApi _tesseractApi;

        // private bool _shouldUpdate = true;

        private TextureView _textureView;
        private TextView _textView;
        private Toolbar _toolbar;

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            if (Camera.NumberOfCameras == 0)
            {
                Toast.MakeText(this, CommonResources.no_camera, ToastLength.Long).Show();
                return;
            }

            _camera = Camera.Open();
            if (_camera == null)
                _camera = Camera.Open(0);

            var cameraParams = _camera.GetParameters();
            cameraParams.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            _camera.SetParameters(cameraParams);

            _previewSize = _camera.GetParameters().PreviewSize;

            _textureView.LayoutParameters =
                new FrameLayout.LayoutParams(_previewSize.Width, _previewSize.Height, GravityFlags.Center);

            _camera.SetPreviewTexture(surface);
            _camera.StartPreview();

            _textureView.Rotation = 90.0f;

            _bitmapPixelArray = new int[_previewSize.Width * _previewSize.Height];

        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            _camera.StopPreview();
            _camera.Release();

            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            // camera takes care of this
        }

        public async void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            //if (_shouldUpdate)
            //{
            //    _updateTimer.Start();
            //    _shouldUpdate = false;
            //    _redrawCount++;
            //    _textView.Text = $"Качество: 0%";
            //}
            //else
            //{
            //    return;
            //}

            if (!_recognizingTextInProgress)
            {
                _recognizingTextInProgress = true;
                await System.Threading.Tasks.Task.Factory.StartNew(RecognizeText);

                _overlayView.ForceLayout();
            }
        }

        private async System.Threading.Tasks.Task InitTesseract()
        {
            _tesseractApi = new TesseractApi(ApplicationContext, AssetsDeployment.OncePerVersion);

            await _tesseractApi.Init("rus");
        }

        private async System.Threading.Tasks.Task RecognizeText()
        {
            //Stopwatch timer = Stopwatch.StartNew();

            var updatedBitmap = _textureView.GetBitmap(_textureView.Bitmap.Width, _textureView.Bitmap.Height);

            updatedBitmap.GetPixels(_bitmapPixelArray, 0, _previewSize.Width, 0, 0, _previewSize.Width,
                _previewSize.Height);

            _tempRecognitionResult.Invalidate();
            _finalRecognitionResult.Invalidate();

            _currentAccuracy = PRIMARY_ACCURACY;

            await PerformRecognizing(updatedBitmap);

            UpdateFinalRecognitionResult();

            if (_tempRecognitionResult.Quality > 50 && _tempRecognitionResult.Quality < 70)
            {
                _currentAccuracy = BIGGER_ACCURACY;
                await PerformRecognizing(updatedBitmap);

                if (_tempRecognitionResult.Quality > _finalRecognitionResult.Quality)
                {
                    UpdateFinalRecognitionResult();
                }
                else if (_tempRecognitionResult.Quality > 50 && _tempRecognitionResult.Quality < 70)
                {
                    _currentAccuracy = SMALLER_ACCURACY;
                    await PerformRecognizing(updatedBitmap);

                    if (_tempRecognitionResult.Quality > _finalRecognitionResult.Quality)
                        UpdateFinalRecognitionResult();
                }
            }

            _overlayView.Rect = new Rect(
                _previewSize.Height - _finalRecognitionResult.BoundingBox.Bottom,
                _finalRecognitionResult.BoundingBox.Left,
                _previewSize.Height - _finalRecognitionResult.BoundingBox.Top,
                _bBox.Right);

            updatedBitmap.Dispose();

            //timer.Stop();
            //TimeSpan timespan = timer.Elapsed;

            _textView.Text = $"{CommonResources.common_quality}: {_finalRecognitionResult.Quality}%";
            _recognizingTextInProgress = false;
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.toolbar_menu, menu);
            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.menu_client:
                    SupportActionBar.Title = CommonResources.on_client;
                    Toast.MakeText(this, CommonResources.on_client, ToastLength.Short).Show();
                    _recognizingActor = RecognizingActor.Client;
                    break;
                case Resource.Id.menu_server:
                    SupportActionBar.Title = CommonResources.on_server;
                    Toast.MakeText(this, CommonResources.on_server, ToastLength.Short).Show();
                    _recognizingActor = RecognizingActor.Server;
                    break;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void UpdateFinalRecognitionResult()
        {
            _finalRecognitionResult.Quality = _tempRecognitionResult.Quality;
            _finalRecognitionResult.ResultText = _tempRecognitionResult.ResultText;
            _finalRecognitionResult.BoundingBox = new Rect(_tempRecognitionResult.BoundingBox);
        }

        private async System.Threading.Tasks.Task PerformRecognizing(Bitmap updatedBitmap)
        {
            GetCroppingBoundingBox(_previewSize.Height, _previewSize.Width);

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

        private async System.Threading.Tasks.Task FirebaseTextRecognizing(Bitmap croppedBitmap)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

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

        private async System.Threading.Tasks.Task TesseractTextRecognizing(Bitmap croppedBitmap)
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

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource

            SetContentView(Resource.Layout.activity_main);
            _textView = FindViewById<TextView>(Resource.Id.text_view);

            _toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(_toolbar);
            SupportActionBar.Title = CommonResources.on_client;

            _textureView = FindViewById<TextureView>(Resource.Id.surface_view);
            _overlayView = FindViewById<OverlayView>(Resource.Id.overlay_view);

            _textureView.SurfaceTextureListener = this;

            //_updateTimer.Elapsed += (sender, e) => { _shouldUpdate = true; };

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.Camera) != Permission.Granted)
                ActivityCompat.RequestPermissions(this, new[] {Manifest.Permission.Camera},
                    REQUEST_CAMERA_ID);

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.WriteExternalStorage) !=
                Permission.Granted)
                ActivityCompat.RequestPermissions(this, new[] {Manifest.Permission.WriteExternalStorage},
                    REQUEST_WRITE_ID);

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.Internet) !=
                Permission.Granted)
                ActivityCompat.RequestPermissions(this, new[] {Manifest.Permission.Internet},
                    REQUEST_INTERNET_ID);

            InitFirebase();
            await InitTesseract();
        }

        private void InitFirebase()
        {
            _defaultFirebaseApp = FirebaseApp.InitializeApp(ApplicationContext);

            _firebaseProcessImageListener = new ProcessImageListener();

            var options = new FirebaseVisionCloudTextRecognizerOptions.Builder()
                .SetLanguageHints(new List<string> {"ru"})
                .SetModelType(FirebaseVisionCloudTextRecognizerOptions.DenseModel)
                .Build();

            _firebaseVisionTextDetector =
                FirebaseVision.GetInstance(_defaultFirebaseApp).GetCloudTextRecognizer(options);
        }

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

        private enum RecognizingActor
        {
            Client,
            Server
        }
    }
}