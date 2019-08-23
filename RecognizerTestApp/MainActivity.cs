﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
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
using Camera = Android.Hardware.Camera;
using Console = System.Console;
using Environment = Android.OS.Environment;
using Exception = Java.Lang.Exception;
using IOException = Java.IO.IOException;
using Object = Java.Lang.Object;
using Path = System.IO.Path;

namespace RecognizerTestApp
{
    public class ProcessImageListener : Object, IOnSuccessListener, IOnCompleteListener, IOnFailureListener
    {
        public EventHandler<string> TextRecognized;

        public void OnComplete(Task task)
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
        private static readonly string TAG = "MainActivity";

        private readonly int _accuracy = 10;
        private Camera _camera;

        private FirebaseApp _defaultFirebaseApp;

        private FirebaseVisionTextRecognizer _firebaseVisionTextDetector;
        private int _maxX = int.MinValue;
        private int _maxY = int.MinValue;
        private int _minX = int.MaxValue;
        private int _minY = int.MaxValue;
        private OverlayView _overlayView;
        private int[] _pixelArray;
        private Camera.Size _previewSize;
        private ProcessImageListener _processImageListener;
        private int _redrawCount;

        private readonly Color _referenceColor = new Color(65, 113, 127);

        private List<Color> _referenceColors = new List<Color>
        {
            new Color(65, 113, 127),
            new Color(83, 116, 125),
            new Color(59, 96, 105)
        };

        private readonly string _referenceString =
            "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower();

        private bool _shouldUpdate = true;

        private TextureView _textureView;
        private TextView _textView;

        private Bitmap _updateBitmap;

        private readonly Timer _updateTimer = new Timer(500)
        {
            AutoReset = false
        };

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
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
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }

            _textureView.Rotation = 90.0f;
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

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
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

            _updateBitmap = _textureView.GetBitmap(_textureView.Bitmap.Width, _textureView.Bitmap.Height);

            _updateBitmap.GetPixels(_pixelArray, 0, _previewSize.Width, 0, 0, _previewSize.Width, _previewSize.Height);

            var updateBitmapHeight = _previewSize.Height;
            var updateBitmapWidth = _previewSize.Width;

            // List<Color> applicableColors = new List<Color>();

            GetBoundingBox(updateBitmapHeight, updateBitmapWidth);

            var overlayWasHidden = _overlayView.Rect == null;
            _overlayView.Rect = null;
            if (_maxX > _minX && _maxY > _minY)
            {
                var searchBuffer = 10;

                _overlayView.Rect = new Rect(_previewSize.Height - _maxY, _minX, _previewSize.Height - _minY, _maxX);

                var matrix = new Matrix();
                matrix.PostRotate(90);

                var width = _maxX - _minX;
                var height = _maxY - _minY;
                var cropped = Bitmap.CreateBitmap(
                    _updateBitmap,
                    _minX > searchBuffer ? _minX - searchBuffer : _minX,
                    _minY > searchBuffer ? _minY - searchBuffer : _minY,
                    _minX + width <= _previewSize.Width - searchBuffer ? _maxX - _minX + searchBuffer : _maxX - _minX,
                    _minY + height <= _previewSize.Height - searchBuffer ? _maxY - _minY + searchBuffer : _maxY - _minY,
                    matrix,
                    false);

                var image = FirebaseVisionImage.FromBitmap(cropped);


                if (_redrawCount > 5)
                {
                    var task = _firebaseVisionTextDetector.ProcessImage(image);
                    task.AddOnSuccessListener(_processImageListener);
                    task.AddOnCompleteListener(_processImageListener);
                    task.AddOnFailureListener(_processImageListener);

                    //ExportBitmapAsPNG(cropped);
                    _redrawCount = 0;
                }
            }

            if (overlayWasHidden && _overlayView.Rect == null) return;
            _overlayView.ForceLayout();
        }


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

            _updateTimer.Elapsed += (sender, e) => { _shouldUpdate = true; };

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

            _processImageListener = new ProcessImageListener();
            _processImageListener.TextRecognized += TextRecognized;

            var options = new FirebaseVisionCloudTextRecognizerOptions.Builder()
                .SetLanguageHints(new List<string> {"ru"})
                .SetModelType(FirebaseVisionCloudTextRecognizerOptions.DenseModel)
                .Build();

            _firebaseVisionTextDetector =
                FirebaseVision.GetInstance(_defaultFirebaseApp).GetCloudTextRecognizer(options);
        }

        private void TextRecognized(object sender, string text)
        {
            if (text.Length < 160)
            {
                text = text.Replace("\n", "").Replace(" ", "").ToLower();

                var diff = StringDistance.GetDamerauLevenshteinDistance(_referenceString, text);

                var quality = (double) (_referenceString.Length - diff) / _referenceString.Length * 100;

                _textView.Text = $"Качество: {quality}%";
            }
        }

        private void GetBoundingBox(int updateBitmapHeight, int updateBitmapWidth)
        {
            for (var j = 0; j < updateBitmapHeight; j++)
            for (var i = 0; i < updateBitmapWidth; i++)
            {
                var pixelColor = _pixelArray[j * updateBitmapWidth + i];

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