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

            var previewSize = _camera.GetParameters().PreviewSize;
            _textureView.LayoutParameters =
                new FrameLayout.LayoutParams(previewSize.Width, previewSize.Height, GravityFlags.Center);

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
            var bitmap = _textureView.GetBitmap(180, 100);
           
            _imageView.SetImageBitmap(bitmap);
        }
    }
}