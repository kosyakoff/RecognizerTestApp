using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;


namespace RecognizerTestApp
{

    [Register("com.OverlayView")]
    public class OverlayView : View
    {

        private int _statusBarHeight = 0;
        private Paint _paint;
        private int _strokeWidth;

        public OverlayView(Context context, IAttributeSet attrs) :
            base(context, attrs)
        {
            Initialize();
        }

        public OverlayView(Context context, IAttributeSet attrs, int defStyle) :
            base(context, attrs, defStyle)
        {
            Initialize();
        }


        private void Initialize()
        {
            _paint = new Paint();
            _paint.SetStyle(Paint.Style.Stroke);
            _paint.Color = Color.Yellow;
            _strokeWidth = 10;
        }

        public Rect Rect { get; set; }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

           // Canvas canvas = _textureView.LockCanvas();
            if (canvas != null && Rect != null &&
                !Rect.IsEmpty)
            {
                _paint.StrokeWidth = _strokeWidth;

                canvas.DrawRect(Rect.Left - _strokeWidth, Rect.Top - _strokeWidth, Rect.Right + _strokeWidth, Rect.Bottom + _strokeWidth, _paint);

                //_textureView.UnlockCanvasAndPost(canvas);
            }

        }
    }
}