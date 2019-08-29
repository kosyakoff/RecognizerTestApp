using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Recognizer.Core.Resources.Strings;
using RecognizerTestApp.Services;
using RecognizerTestApp.Settings;
using Camera = Android.Hardware.Camera;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace RecognizerTestApp
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, TextureView.ISurfaceTextureListener
    {
        private const int REQUEST_CAMERA_ID = 1001;
        private const int REQUEST_WRITE_ID = 1002;
        private const int REQUEST_INTERNET_ID = 1003;

        private Camera _camera;

        private OverlayView _overlayView;
        private Camera.Size _previewSize;
        private bool _rightCheckInProgress;

        private RecognizerService _recognizerService;

        private TextureView _textureView;
        private TextView _textView;
        private Toolbar _toolbar;
        private ImageView _imageView;
        private Button _rerunButton;
        private SurfaceTexture _surface;

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            if (Camera.NumberOfCameras == 0)
            {
                Toast.MakeText(this, CommonResources.no_camera, ToastLength.Long).Show();
                return;
            }

            _surface = surface;

            CheckRights();
        }

        private async Task MainInit(SurfaceTexture surface)
        {
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

            await _recognizerService.Init(ApplicationContext, new Size(_previewSize.Width, _previewSize.Height));
            _recognizerService.OverlayRectUpdated += RecognizerServiceOverlayRectUpdated;
            _recognizerService.CroppedImageUpdated += RecognizerServiceCroppedImageUpdated;
            _recognizerService.RecordWasFound += RecognizerServiceRecordWasFound;
        }

        private void RecognizerServiceRecordWasFound(object sender, string result)
        {
            RunOnUiThread(() =>
            {
                _rerunButton.Visibility = ViewStates.Visible;
                _overlayView.Rect = new Rect();
                _overlayView.ForceLayout();
                _textView.Text = result;
            });
            
        }

        private void RecognizerServiceCroppedImageUpdated(object sender, Bitmap bitmap)
        {
            RunOnUiThread(() => _imageView.SetImageBitmap(bitmap));
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
            if (_recognizerService.IsInitialized && !_recognizerService.SearchComplete && !_recognizerService.RecognizingTextInProgress)
            {
                _recognizerService.RecognizingTextInProgress = true;
                await Task.Factory.StartNew(RecognizeText);
            }
        }

        private async Task RecognizeText()
        {
            var updatedBitmap = _textureView.GetBitmap(_textureView.Bitmap.Width, _textureView.Bitmap.Height);

            var result = await _recognizerService.RecognizeText(updatedBitmap);

            _textView.Text = $"{CommonResources.common_quality}: {result.Quality}%";
            _overlayView.ForceLayout();
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
                    _recognizerService.SetRecognizingActor(RecognizingActor.Client);

                    break;
                case Resource.Id.menu_server:
                    SupportActionBar.Title = CommonResources.on_server;
                    Toast.MakeText(this, CommonResources.on_server, ToastLength.Short).Show();
                    _recognizerService.SetRecognizingActor(RecognizingActor.Server);
                    break;
            }

            return base.OnOptionsItemSelected(item);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            _recognizerService = new RecognizerService();

            // Set our view from the "main" layout resource

            SetContentView(Resource.Layout.activity_main);

            if (GeneralSettings.ShowCroppedImage)
            {
                _imageView = FindViewById<ImageView>(Resource.Id.image_view);
                _imageView.Visibility = ViewStates.Visible;
            }

            _textView = FindViewById<TextView>(Resource.Id.text_view);
            _rerunButton = FindViewById<Button>(Resource.Id.rerun_button);
            _rerunButton.Text = CommonResources.rerun;
            _rerunButton.Click += (obj, e) =>
            {
                _rerunButton.Visibility = ViewStates.Invisible;
                _recognizerService.SearchComplete = false;
                _textView.Text = string.Empty;
            };

            _toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(_toolbar);
            SupportActionBar.Title = CommonResources.on_client;

            _textureView = FindViewById<TextureView>(Resource.Id.surface_view);
            _overlayView = FindViewById<OverlayView>(Resource.Id.overlay_view);

            _textureView.SurfaceTextureListener = this;


        }

        private void CheckRights()
        {
            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.Camera) != Permission.Granted)
            {
                _rightCheckInProgress = true;
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Camera },
                    REQUEST_CAMERA_ID);
                return;
            }

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.WriteExternalStorage) !=
                Permission.Granted)
            {
                _rightCheckInProgress = true;
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.WriteExternalStorage },
                    REQUEST_WRITE_ID);
                return;
            }

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.Internet) !=
                Permission.Granted)
            {
                _rightCheckInProgress = true;
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Internet },
                    REQUEST_INTERNET_ID);
                return;
            }

            MainInit(_surface);
        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            switch (requestCode)
            {
                case REQUEST_CAMERA_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.camera_request_permission)
                            .Show(this.FragmentManager, "dialog");
                    }
                    break;

                case REQUEST_WRITE_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.write_request_permission)
                            .Show(this.FragmentManager, "dialog");
                    }
                    break;

                case REQUEST_INTERNET_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.internet_request_permission)
                            .Show(this.FragmentManager, "dialog");
                    }
                    break;
            }

            CheckRights();

        }

        private void RecognizerServiceOverlayRectUpdated(object sender, Rect rect)
        {
            _overlayView.Rect = rect;

        }
    }
}