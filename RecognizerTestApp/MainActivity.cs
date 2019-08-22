using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using Android;
using Android.App;
using Android.Content.PM;
using Android.Gms.Tasks;
using Android.Gms.Vision;
using Android.Gms.Vision.Texts;
using Android.Graphics;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using Firebase;
using Firebase.ML.Vision;
using Firebase.ML.Vision.Common;
using Firebase.ML.Vision.Text;
using Java.Nio;
using Java.Util.Concurrent;
using Tesseract;
using Tesseract.Droid;
using Camera = Android.Hardware.Camera;
using Object = Java.Lang.Object;
using Timer = System.Timers.Timer;

namespace RecognizerTestApp
{
    public class ProcessImageListener : Java.Lang.Object, IOnSuccessListener, IOnCompleteListener, IOnFailureListener
    {
        public void OnFailure(Java.Lang.Exception e)
        {
            var tt = e;
        }

        public void OnComplete(Task task)
        {
            var tt = task;
        }

        public void OnSuccess(Object result)
        {
            var text = (FirebaseVisionText)result;
        }
    }

    [Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/Theme.AppCompat.Light.NoActionBar")]
    public class MainActivity : AppCompatActivity, TextureView.ISurfaceTextureListener
    {
        static readonly string TAG = "MainActivity";

        internal static readonly string CHANNEL_ID = "my_notification_channel";
        internal static readonly int NOTIFICATION_ID = 100;

        private TextureView _textureView;
        private TextView _textView;
        private const int REQUEST_CAMERA_ID = 1001;
        private const int REQUEST_WRITE_ID = 1002;
        private const int REQUEST_INTERNET_ID = 1003;
        private ProcessImageListener _processImageListener;
        Camera _camera;
        private Camera.Size _previewSize;
        int _minX = int.MaxValue;
        int _maxX = int.MinValue;
        int _minY = int.MaxValue;
        int _maxY = int.MinValue;
        int[] _pixelArray;
        private bool _screenshotTaken = false;

        private Bitmap _updateBitmap;
        private OverlayView _overlayView;

        private bool _shouldUpdate = true;
        private int _redrawCount = 0;
        private TesseractApi _tesseractApi;

        private string _referenceString = "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ","").ToLower();

        private FirebaseApp _defaultFirebaseApp;

        private Timer _updateTimer = new Timer(700)
        {
            AutoReset = false
        };

        readonly int _accuracy = 10;

        Color _referenceColor = new Color(65, 113, 127);
       // private Color _referenceColor = Color.Black;
        private List<Color> _referenceColors = new List<Color>
        {
            new Color(65, 113, 127),
            new Color(83, 116, 125),
            new Color(59,96,105),
        };



        protected override void OnCreate(Bundle savedInstanceState)
        {

            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            _defaultFirebaseApp = FirebaseApp.InitializeApp(ApplicationContext);

            SetContentView(Resource.Layout.activity_main);
            _textView = FindViewById<TextView>(Resource.Id.text_view);

            _textureView = FindViewById<TextureView>(Resource.Id.surface_view);
            _overlayView = FindViewById<OverlayView>(Resource.Id.overlay_view);

            _textureView.SurfaceTextureListener = this;

            _updateTimer.Elapsed += (sender, e) =>
            {
                _shouldUpdate = true;
            };

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.Camera) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.Camera },
                    REQUEST_CAMERA_ID);
            }

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.WriteExternalStorage) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.WriteExternalStorage },
                    REQUEST_WRITE_ID);
            }

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.Internet) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.Internet },
                    REQUEST_INTERNET_ID);
            }

            _processImageListener = new ProcessImageListener();
        }

        public void OnSurfaceTextureAvailable(Android.Graphics.SurfaceTexture surface, int width, int height)
        {
            if (Camera.NumberOfCameras == 0)
            {
                Toast.MakeText(this, "No camera", ToastLength.Long).Show();
                return;
            }
            _camera = Camera.Open();
            if (_camera == null)
                _camera = Camera.Open(0);

            var pars = _camera.GetParameters();
            pars.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            _camera.SetParameters(pars);

            _previewSize = _camera.GetParameters().PreviewSize;
            _textureView.LayoutParameters =
                new FrameLayout.LayoutParams(_previewSize.Width, _previewSize.Height, GravityFlags.Center);

            _pixelArray = new int[_previewSize.Width * _previewSize.Height];

            try
            {
                _camera.SetPreviewTexture(surface);
                _camera.StartPreview();
            }
            catch (Java.IO.IOException ex)
            {
                Console.WriteLine(ex.Message);
            }

            // this is the sort of thing TextureView enables
            _textureView.Rotation = 90.0f;

            //_tesseractApi = new TesseractApi(ApplicationContext, AssetsDeployment.OncePerVersion);
            
           // await _tesseractApi.Init("rus");
        }

        public bool OnSurfaceTextureDestroyed(Android.Graphics.SurfaceTexture surface)
        {
            _camera.StopPreview();
            _camera.Release();

            return true;
        }

        public void OnSurfaceTextureSizeChanged(Android.Graphics.SurfaceTexture surface, int width, int height)
        {
            // camera takes care of this
        }

        //public static Bitmap RotateBitmap(Bitmap source, float angle)
        //{
        //    Matrix matrix = new Matrix();
        //    matrix.PostRotate(angle);
        //    return Bitmap.CreateBitmap(source, 0, 0, source.Width, source.Height, matrix, true);
        //}



        public void OnSurfaceTextureUpdated(Android.Graphics.SurfaceTexture surface)
        {

            _textView.Text = $"Качество: 0%";
            if (_shouldUpdate)
            {
                _updateTimer.Start();
                _shouldUpdate = false;
                _redrawCount++;
            }
            else
            {
                return;
            }

            _minX = int.MaxValue;
            _maxX = int.MinValue;
            _minY = int.MaxValue;
            _maxY = int.MinValue;

             //var bitmap = _textureView.Bitmap;

            _updateBitmap = _textureView.GetBitmap(_textureView.Bitmap.Width, _textureView.Bitmap.Height);

            //using (Canvas canvas = new Canvas(_updateBitmap))
            //{
            //    _textureView.Draw(canvas);
            //}

            _updateBitmap.GetPixels(_pixelArray, 0, _previewSize.Width, 0, 0, _previewSize.Width, _previewSize.Height);

            var updateBitmapHeight = _previewSize.Height;
            var updateBitmapWidth = _previewSize.Width;

            // List<Color> applicableColors = new List<Color>();

            GetBoundingBox(updateBitmapHeight, updateBitmapWidth);

            bool overlayWasHidden = _overlayView.Rect == null;
            _overlayView.Rect = null;
            if (_maxX > _minX && _maxY > _minY)
            {
                int searchBuffer = 10;

                _overlayView.Rect = new Rect(_previewSize.Height - _maxY, _minX, _previewSize.Height - _minY, _maxX);

                var matrix = new Matrix();
                matrix.PostRotate(90);

                var width = (_maxX - _minX);
                var height = (_maxY - _minY);
                var cropped = Bitmap.CreateBitmap(
                    _updateBitmap,
                    _minX > searchBuffer ? _minX - searchBuffer : _minX,
                    _minY > searchBuffer ? _minY - searchBuffer : _minY,
                    _minX + width <= _previewSize.Width - searchBuffer ? (_maxX - _minX) + searchBuffer : (_maxX - _minX),
                    _minY + height <= _previewSize.Height - searchBuffer ? (_maxY - _minY) + searchBuffer : (_maxY - _minY),
                    matrix,
                    false);

                //if (_tesseractApi != null && _tesseractApi.Initialized)
                //{
                //    var success = await _tesseractApi.Recognise(cropped);
                //    if (success && _tesseractApi.Text.Length < 160)
                //    {
                //        string text = _tesseractApi.Text.Replace("\n", "").Replace(" ", "").ToLower();

                //        var diff = StringDistance.GetDamerauLevenshteinDistance(_referenceString, text);

                //        double quality = (((double)(_referenceString.Length - diff)) / _referenceString.Length) * 100;

                //        _textView.Text = $"Качество: {quality}%";
                //    }
                //}

                var image = FirebaseVisionImage.FromBitmap(cropped);
                var firebaseVisionTextDetector = FirebaseVision.GetInstance(_defaultFirebaseApp).OnDeviceTextRecognizer;

                try
                {
                    
                    firebaseVisionTextDetector.ProcessImage(image).AddOnSuccessListener(_processImageListener);
                    firebaseVisionTextDetector.ProcessImage(image).AddOnCompleteListener(_processImageListener);
                    firebaseVisionTextDetector.ProcessImage(image).AddOnFailureListener(_processImageListener);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
              

                //if (_redrawCount > 5)
                //{
                //    ExportBitmapAsPNG(cropped);
                //    _redrawCount = 0;
                //}
            }

            if (overlayWasHidden && _overlayView.Rect == null)
            {
                return;
            }
            _overlayView.ForceLayout();
        }

        private void GetBoundingBox(int updateBitmapHeight, int updateBitmapWidth)
        {
            for (int j = 0; j < updateBitmapHeight; j++)
            {
                for (int i = 0; i < updateBitmapWidth; i++)
                {
                    int pixelColor = _pixelArray[(j * updateBitmapWidth) + i];

                    int red = Color.GetRedComponent(pixelColor);

                    // applicableColors.AddRange(_referenceColors.Where(x => Math.Abs(x.R - red) < _accuracy));

                    if (Math.Abs(_referenceColor.R - red) >= _accuracy)
                    //  if (!applicableColors.Any())
                    {
                        continue;
                    }

                    int green = Color.GetGreenComponent(pixelColor);

                    //applicableColors = applicableColors.Except(applicableColors.Where(x => Math.Abs(x.G - green) >= _accuracy)).ToList();


                    if (Math.Abs(_referenceColor.G - green) >= _accuracy)
                    //if (!applicableColors.Any())
                    {
                        continue;
                    }

                    int blue = Color.GetBlueComponent(pixelColor);

                    //applicableColors = applicableColors.Except(applicableColors.Where(x => Math.Abs(x.B - blue) >= _accuracy)).ToList();


                    if (Math.Abs(_referenceColor.B - green) >= _accuracy)
                    //if (!applicableColors.Any())
                    {
                        continue;
                    }

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
            if (_minX > x)
                _minX = x;
            if (_maxX < x)
                _maxX = x;

            if (_minY > y)
                _minY = y;
            if (_maxY < y)
                _maxY = y;
        }

        void ExportBitmapAsPNG(Bitmap bitmap)
        {
            var sdCardPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
            var filePath = System.IO.Path.Combine(sdCardPath, "test.png");
            var stream = new FileStream(filePath, FileMode.Create);
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
            stream.Close();
        }
    }
}