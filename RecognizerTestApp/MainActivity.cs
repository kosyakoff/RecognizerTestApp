﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Java.Util;
using Recognizer.Core.Resources.Strings;
using RecognizerTestApp.Helpers;
using RecognizerTestApp.Services;
using RecognizerTestApp.Settings;
using Camera = Android.Hardware.Camera;
using Exception = System.Exception;
using Math = System.Math;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace RecognizerTestApp
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, TextureView.ISurfaceTextureListener
    {
        private const int REQUEST_CAMERA_ID = 1001;
        private const int REQUEST_WRITE_ID = 1002;
        private const int REQUEST_INTERNET_ID = 1003;

        private static readonly string TAG = "Recognizer";
        // Max preview width that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_WIDTH = 1920;

        // Max preview height that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_HEIGHT = 1080;

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
        private Size _realSurfaceSize = new Size(0,0);
        private TextView _serviceText;

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

        private void SetTextureViewSize()
        {
            var previewWidth = _previewSize.Height;
            var previewHeight = _previewSize.Width;

            double wh = (double)previewWidth / previewHeight;
            double hw = (double)previewHeight / previewWidth;

            if (previewHeight < _textureView.Height && previewWidth < _textureView.Width)
            {
                if ((previewHeight / _textureView.Height) >= (previewWidth / _textureView.Width))
                {
                    _realSurfaceSize = new Size((int)(_textureView.Height / hw), _textureView.Height);
                }
                else
                {
                    _realSurfaceSize = new Size(_textureView.Width, (int)(_textureView.Width / wh));
                }
            }
            else
            {
                if (( _textureView.Height / previewHeight) >= (_textureView.Width / previewWidth))
                {
                    _realSurfaceSize = new Size((int)(_textureView.Height / hw), _textureView.Height);
                }
                else
                {
                    _realSurfaceSize = new Size(_textureView.Width, (int)(_textureView.Width / wh));
                }
            }

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

            SetTextureViewSize();

            _camera.SetPreviewTexture(surface);
            _camera.StartPreview();

            _textureView.Rotation = 90.0f;
            _overlayView.Rotation = 90.0f;

            RunOnUiThread(() =>
            {
                _textureView.LayoutParameters =
                    new FrameLayout.LayoutParams(_realSurfaceSize.Height, _realSurfaceSize.Width,
                        GravityFlags.CenterVertical | GravityFlags.CenterHorizontal);

                _overlayView.LayoutParameters =
                    new FrameLayout.LayoutParams(_realSurfaceSize.Height, _realSurfaceSize.Width,
                        GravityFlags.CenterVertical | GravityFlags.CenterHorizontal);
            });


            await _recognizerService.Init(ApplicationContext, new Size(_realSurfaceSize.Height , _realSurfaceSize.Width));
            _recognizerService.OverlayRectUpdated += RecognizerServiceOverlayRectUpdated;
            _recognizerService.CroppedImageUpdated += RecognizerServiceCroppedImageUpdated;
            _recognizerService.RecordWasFound += RecognizerServiceRecordWasFound;
            _recognizerService.SomeIncorrectTextWasFound += RecognizerServiceSomeIncorrectTextWasFound;
            _recognizerService.NoTextWasFound += RecognizerServiceNoTextWasFound; 
        }

        private void RecognizerServiceNoTextWasFound(object sender, EventArgs e)
        {
            try
            {
                RunOnUiThread(() => { _serviceText.Text = CommonResources.place_cursor_on_text; });
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        private void RecognizerServiceSomeIncorrectTextWasFound(object sender, EventArgs e)
        {
            try
            {
                RunOnUiThread(() =>
                {
                    Toast.MakeText(this, CommonResources.some_wrong_text_found, ToastLength.Short).Show();
                    _serviceText.Text = string.Empty;
                });
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        private void RecognizerServiceRecordWasFound(object sender, string result)
        {
            try
            {
                RunOnUiThread(() =>
                {
                    _rerunButton.Visibility = ViewStates.Visible;
                    _overlayView.Rect = new Rect();
                    _overlayView.ForceLayout();
                    _textView.Text = result;
                    _serviceText.Text = string.Empty;
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
            if (_recognizerService.GetRecognizingActor() == RecognizingActor.Server &&
                !_recognizerService.CanBeRecognizedFromServer())
            {
                return;
            }

            if (_recognizerService.IsInitialized && !_recognizerService.SearchComplete && !_recognizerService.RecognizingTextInProgress)
            {
                _recognizerService.RecognizingTextInProgress = true;
                await Task.Factory.StartNew(RecognizeText);
            }
        }

        private async Task RecognizeText()
        {
            try
            {
                var updatedBitmap = _textureView.GetBitmap(_textureView.Bitmap.Width, _textureView.Bitmap.Height);

                var result = await _recognizerService.RecognizeText(updatedBitmap);

                _textView.Text = $"{CommonResources.common_quality}: {result.Quality}%";
                _overlayView.ForceLayout();
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
            }
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
                    _recognizerService.SetRecognizingActor(RecognizingActor.Server);

                    if (_recognizerService.CanBeRecognizedFromServer())
                    {
                        Toast.MakeText(this, CommonResources.on_server, ToastLength.Short).Show();
                    }
                    else
                    {
                        Toast.MakeText(this, CommonResources.cant_be_recognized_from_server, ToastLength.Long).Show();
                    }
                    break;
            }

            return base.OnOptionsItemSelected(item);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            RequestedOrientation = ScreenOrientation.Portrait;

            _recognizerService = new RecognizerService();

            // Set our view from the "main" layout resource

            SetContentView(Resource.Layout.activity_main);

            if (GeneralSettings.ShowCroppedImage)
            {
                _imageView = FindViewById<ImageView>(Resource.Id.image_view);
                _imageView.Visibility = ViewStates.Visible;
            }

            _serviceText = FindViewById<TextView>(Resource.Id.service_text);
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
            _overlayView.BringToFront();

            _textureView.SurfaceTextureListener = this;

        }

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var newExc = new Exception("TaskSchedulerOnUnobservedTaskException", e.Exception);
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var newExc = new Exception("CurrentDomainOnUnhandledException", e.ExceptionObject as Exception);
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

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            switch (requestCode)
            {
                case REQUEST_CAMERA_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.camera_request_permission)
                            .Show(this.FragmentManager, "dialog");
                        this.FinishAffinity();
                    }
                    break;

                case REQUEST_WRITE_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.write_request_permission)
                            .Show(this.FragmentManager, "dialog");
                        this.FinishAffinity();
                    }
                    break;

                case REQUEST_INTERNET_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.internet_request_permission)
                            .Show(this.FragmentManager, "dialog");
                        this.FinishAffinity();
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