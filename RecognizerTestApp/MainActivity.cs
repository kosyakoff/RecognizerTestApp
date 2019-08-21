using System;
using System.Text;
using Android;
using Android.App;
using Android.Content.PM;
using Android.Gms.Vision;
using Android.Gms.Vision.Texts;
using Android.Graphics;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Camera = Android.Hardware.Camera;

namespace RecognizerTestApp
{
    [Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/Theme.AppCompat.Light.NoActionBar")]
    public class MainActivity : AppCompatActivity, TextureView.ISurfaceTextureListener
    {
        private TextureView _textureView;
        private TextView _textView;
        private const int REQUEST_CAMERA_ID = 1001;
        Camera _camera;
        private ImageView _imageView;
        private Camera.Size _previewSize;
        int _minX = int.MaxValue;
        int _maxX = int.MinValue;
        int _minY = int.MaxValue;
        int _maxY = int.MinValue;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource

            // _textView = FindViewById<TextView>(Resource.Id.text_view);
            SetContentView(Resource.Layout.activity_main);

            _textureView = FindViewById<TextureView>(Resource.Id.surface_view);
            _imageView = FindViewById<ImageView>(Resource.Id.image_view);

            _textureView.SurfaceTextureListener = this;
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

            _previewSize = _camera.GetParameters().PreviewSize;
            _textureView.LayoutParameters =
                new FrameLayout.LayoutParams(_previewSize.Width, _previewSize.Height, GravityFlags.Center);


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
            _imageView.Rotation = 90.0f;
            //_textureView.Alpha = 0.5f;
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
            _minX = int.MaxValue;
            _maxX = int.MinValue;
            _minY = int.MaxValue;
            _maxY = int.MinValue;

            var bitmap = _textureView.GetBitmap(180, 100);

            var def = 50;
            int referenceColorR = 126;
            int referenceColorG = 193;
            int referenceColorB = 208;

            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    var pixelColor = bitmap.GetPixel(i, j);

                    int red = Color.GetRedComponent(pixelColor);
                    int green = Color.GetGreenComponent(pixelColor);
                    int blue = Color.GetBlueComponent(pixelColor);

                    var dbl_test_red = Math.Pow(((double)referenceColorR - red), 2.0);
                    var dbl_test_green = Math.Pow(((double)referenceColorG - green), 2.0);
                    var dbl_test_blue = Math.Pow(((double)referenceColorB - blue), 2.0);

                    var distance = Math.Sqrt(dbl_test_blue + dbl_test_green + dbl_test_red);

                    if (distance < def)
                    {
                        UpdateBoundingBox(i, j);
                        bitmap.SetPixel(i,j,Color.Blue);
                        //_myBitmap2.SetPixel(i, j, System.Drawing.Color.Blue);
                    }
                    else
                    {
                        bitmap.SetPixel(i, j, Color.Yellow);
                        //_myBitmap2.SetPixel(i, j, System.Drawing.Color.Yellow);
                    }
                }
            }

            if (_maxX <= _minX || _maxY <= _minY)
            {
                _minX = 0;
                _minY = 0;
                _maxX = _previewSize.Width;
                _maxY = _previewSize.Height;
            }
            else
            {
                Canvas canvas = _textureView.LockCanvas();
                if (canvas != null)
                {
                    Paint myPaint = new Paint();
                    myPaint.Color = Color.Yellow;
                    myPaint.StrokeWidth = 10;
                    canvas.DrawRect(_minX, _minY, _maxX, _maxY, myPaint);
                    _textureView.UnlockCanvasAndPost(canvas);
                }
            }

            _imageView.SetImageBitmap(bitmap);
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
    }
}