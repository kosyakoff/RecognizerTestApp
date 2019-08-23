using Android;
using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Firebase;
using Firebase.ML.Vision.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Tesseract.Droid;
using Camera = Android.Hardware.Camera;
using Environment = Android.OS.Environment;
using Exception = Java.Lang.Exception;
using Object = Java.Lang.Object;
using Path = System.IO.Path;
using Task = System.Threading.Tasks.Task;

namespace RecognizerTestApp
{
    public class ProcessImageListener : Object, Android.Gms.Tasks.IOnSuccessListener, 
        Android.Gms.Tasks.IOnCompleteListener, Android.Gms.Tasks.IOnFailureListener
    {
        public EventHandler<string> TextRecognized;

        public void OnComplete(Android.Gms.Tasks.Task task)
        {
        }

        public void OnFailure(Exception e)
        {
        }

        public void OnSuccess(Object result)
        {
            var text = (FirebaseVisionText) result;
            TextRecognized?.Invoke(this, text.Text);
        }
    }

    [Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/Theme.AppCompat.Light.NoActionBar")]
    public class MainActivity : AppCompatActivity, TextureView.ISurfaceTextureListener
    {
        private const int REQUEST_CAMERA_ID = 1001;
        private const int REQUEST_WRITE_ID = 1002;
        private const int REQUEST_INTERNET_ID = 1003;

        public volatile bool _recognizingTextInProgress;

        private readonly int _accuracy = 10;
        private readonly Rect _bBox = new Rect();

        private Camera _camera;

        private FirebaseApp _defaultFirebaseApp;
        private FirebaseVisionTextRecognizer _firebaseVisionTextDetector;
        private ProcessImageListener _firebaseProcessImageListener;

        private int[] _bitmapPixelArray;
        private Camera.Size _previewSize;

        private TesseractApi _tesseractApi;

        private readonly Color _referenceColor = new Color(65, 113, 127);

        private List<Color> _referenceColors = new List<Color>
        {
            new Color(65, 113, 127),
            new Color(83, 116, 125),
            new Color(59, 96, 105)
        };

        private readonly string _referenceString =
            "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower();

       // private bool _shouldUpdate = true;

        private TextureView _textureView;
        private TextView _textView;
        private OverlayView _overlayView;

        //private readonly Timer _updateTimer = new Timer(500)
        //{
        //    AutoReset = false
        //};

        private int _redrawCount;

        public async void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            if (Camera.NumberOfCameras == 0)
            {
                Toast.MakeText(this, "No camera", ToastLength.Long).Show();
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

            _bitmapPixelArray = new int[_previewSize.Width * _previewSize.Height];

            _camera.SetPreviewTexture(surface);
            _camera.StartPreview();

            _textureView.Rotation = 90.0f;

            _tesseractApi = new TesseractApi(ApplicationContext, AssetsDeployment.OncePerVersion);

            await _tesseractApi.Init("rus");
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
                await Task.Factory.StartNew(RecognizeText);
                _overlayView.ForceLayout();
            }
        }

        private async Task RecognizeText()
        {
            //Stopwatch timer = Stopwatch.StartNew();

            Bitmap updatedBitmap = _textureView.GetBitmap(_textureView.Bitmap.Width, _textureView.Bitmap.Height);

            updatedBitmap.GetPixels(_bitmapPixelArray, 0, _previewSize.Width, 0, 0, _previewSize.Width, _previewSize.Height);

            GetCroppingBoundingBox(_previewSize.Height, _previewSize.Width);

            _overlayView.Rect = null;

            if (!_bBox.IsEmpty)
            {
                var searchBuffer = 10;

                _overlayView.Rect = new Rect(_previewSize.Height - _bBox.Bottom, _bBox.Left, _previewSize.Height - _bBox.Top, _bBox.Right);

                Bitmap croppedBitmap;
                using (var matrix = new Matrix())
                {
                    matrix.PostRotate(90);

                    croppedBitmap =
                        Bitmap.CreateBitmap(updatedBitmap,
                            _bBox.Left > searchBuffer ? _bBox.Left - searchBuffer : _bBox.Left,
                            _bBox.Top > searchBuffer ? _bBox.Top - searchBuffer : _bBox.Top,
                            _bBox.Width() + searchBuffer <= _previewSize.Width ? _bBox.Width() + searchBuffer : _bBox.Width(),
                            _bBox.Height() + searchBuffer <= _previewSize.Height ? _bBox.Height() + searchBuffer : _bBox.Height(),
                            matrix,
                            false);
                }

                await TesseractTextRecognizing(croppedBitmap);

                croppedBitmap.Dispose();
                //var image = FirebaseVisionImage.FromBitmap(cropped);

                //if (_redrawCount > 5)
                //{
                //    var task = _firebaseVisionTextDetector.ProcessImage(image);
                //    task.AddOnSuccessListener(_firebaseProcessImageListener);
                //    task.AddOnCompleteListener(_firebaseProcessImageListener);
                //    task.AddOnFailureListener(_firebaseProcessImageListener);

                //    //ExportBitmapAsPNG(cropped);
                //    _redrawCount = 0;
                //}
            }

            updatedBitmap.Dispose();

            //timer.Stop();
            //TimeSpan timespan = timer.Elapsed;

            _recognizingTextInProgress = false;
        }

        private async Task TesseractTextRecognizing(Bitmap croppedBitmap)
        {
            if (_tesseractApi != null && _tesseractApi.Initialized)
            {
                var success = await _tesseractApi.Recognise(croppedBitmap);

                if (success && _tesseractApi.Text.Length < 160)
                {
                    string text = _tesseractApi.Text.Replace("\n", "").Replace(" ", "").ToLower();

                    var diff = StringDistance.GetDamerauLevenshteinDistance(_referenceString, text);

                    double quality = (((double)(_referenceString.Length - diff)) / _referenceString.Length) * 100;

                    _textView.Text = $"Качество: {quality}%";
                }
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            //_defaultFirebaseApp = FirebaseApp.InitializeApp(ApplicationContext);

            SetContentView(Resource.Layout.activity_main);
            _textView = FindViewById<TextView>(Resource.Id.text_view);

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

            //_firebaseProcessImageListener = new ProcessImageListener();
            //_firebaseProcessImageListener.FirebaseTextRecognized += FirebaseTextRecognized;

            //var options = new FirebaseVisionCloudTextRecognizerOptions.Builder()
            //    .SetLanguageHints(new List<string> {"ru"})
            //    .SetModelType(FirebaseVisionCloudTextRecognizerOptions.DenseModel)
            //    .Build();

            //_firebaseVisionTextDetector =
            //    FirebaseVision.GetInstance(_defaultFirebaseApp).GetCloudTextRecognizer(options);
        }

        private void FirebaseTextRecognized(object sender, string text)
        {
            if (text.Length < 160)
            {
                text = text.Replace("\n", "").Replace(" ", "").ToLower();

                var diff = StringDistance.GetDamerauLevenshteinDistance(_referenceString, text);

                var quality = (double) (_referenceString.Length - diff) / _referenceString.Length * 100;

                _textView.Text = $"Качество: {quality}%";
            }
        }

        private void GetCroppingBoundingBox(int updateBitmapHeight, int updateBitmapWidth)
        {
            _bBox.Set(int.MaxValue,int.MaxValue,int.MinValue,int.MinValue);

            for (var j = 0; j < updateBitmapHeight; j++)
            {
                for (var i = 0; i < updateBitmapWidth; i++)
                {
                    var pixelColor = _bitmapPixelArray[j * updateBitmapWidth + i];

                    var red = Color.GetRedComponent(pixelColor);

                    // applicableColors.AddRange(_referenceColors.Where(x => Math.Abs(x.R - red) < _accuracy));

                    if (Math.Abs(_referenceColor.R - red) >= _accuracy)
                        //  if (!applicableColors.Any())
                        continue;

                    var green = Color.GetGreenComponent(pixelColor);

                    //applicableColors = applicableColors.Except(applicableColors.Where(x => Math.Abs(x.G - green) >= _accuracy)).ToList();


                    if (Math.Abs(_referenceColor.G - green) >= _accuracy)
                        //if (!applicableColors.Any())
                        continue;

                    var blue = Color.GetBlueComponent(pixelColor);

                    //applicableColors = applicableColors.Except(applicableColors.Where(x => Math.Abs(x.B - blue) >= _accuracy)).ToList();


                    if (Math.Abs(_referenceColor.B - green) >= _accuracy)
                        //if (!applicableColors.Any())
                        continue;

                    UpdateBoundingBox(i, j);

                    //var dbl_test_red = Math.Pow(((double)referenceColorR - red), 2.0);
                    //var dbl_test_green = Math.Pow(((double)referenceColorG - green), 2.0);
                    //var dbl_test_blue = Math.Pow(((double)referenceColorB - blue), 2.0);

                    //var distance = Math.Sqrt(dbl_test_blue + dbl_test_green + dbl_test_red);
                }
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
    }
}