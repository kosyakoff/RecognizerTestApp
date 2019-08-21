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

namespace RecognizerTestApp
{
    [Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/Theme.AppCompat.Light.NoActionBar")]
    public class MainActivity : AppCompatActivity, ISurfaceHolderCallback, Detector.IProcessor
    {
        private SurfaceView _cameraView;
        private TextView _textView;
        private CameraSource _cameraSource;
        private const int REQUEST_CAMERA_ID = 1001;

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            switch (requestCode)
            {
                case REQUEST_CAMERA_ID:
                    if (grantResults[0] == Permission.Granted)
                    {
                        _cameraSource.Start(_cameraView.Holder);
                    }
                    break;
            }

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            _cameraView = FindViewById<SurfaceView>(Resource.Id.surface_view);
            _textView = FindViewById<TextView>(Resource.Id.text_view);

            TextRecognizer textRecognizer = new TextRecognizer.Builder(ApplicationContext).Build();
            if (!textRecognizer.IsOperational)
            {
                Log.Error("Main Activity", "Detector deps are not yet available");
            }
            else
            {
                _cameraSource = new CameraSource.Builder(ApplicationContext,  textRecognizer)
                    .SetFacing(CameraFacing.Back)
                    .SetRequestedPreviewSize(1280,1024)
                    .SetRequestedFps(2.0f)
                    .SetAutoFocusEnabled(true)
                    .Build();
                _cameraView.Holder.AddCallback(this);
                textRecognizer.SetProcessor(this);

            }
        }

        public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
        {
            Console.WriteLine("1");
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            if (ActivityCompat.CheckSelfPermission(ApplicationContext,Manifest.Permission.Camera) != Android.Content.PM.Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new string[]
                {
                    Android.Manifest.Permission.Camera
                }, REQUEST_CAMERA_ID);

                return;
            }

            _cameraSource.Start(_cameraView.Holder);
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            _cameraSource.Stop();
        }

        public void ReceiveDetections(Detector.Detections detections)
        {
            SparseArray items = detections.DetectedItems;

            if (items.Size() != 0)
            {
                _textView.Post(() =>
                {
                    StringBuilder stringBuilder = new StringBuilder();

                    for (int i = 0; i < items.Size(); ++i)
                    {
                        stringBuilder.Append(((TextBlock)items.ValueAt(i)).Value);
                        stringBuilder.Append("\n");
                    }

                    _textView.Text = stringBuilder.ToString();
                });
            }
        }

        public void Release()
        {
          
        }
    }
}