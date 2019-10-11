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
        private Paint _strokePaint;
        private Paint _backgroundPaint;
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
            _strokePaint = new Paint();
            _strokePaint.SetStyle(Paint.Style.Stroke);
            _strokePaint.Color = Color.Yellow;
            _strokeWidth = 10;

            _backgroundPaint = new Paint();
            _backgroundPaint.SetStyle(Paint.Style.Fill);
            _backgroundPaint.Color = new Color(128, 128, 128, 128);
        }

        public Rect Rect { get; set; }

        protected override void OnDraw(Canvas canvas)
        {
            try
            {
                base.OnDraw(canvas);

                // Canvas canvas = _textureView.LockCanvas();
                if (canvas != null && Rect != null &&
                    !Rect.IsEmpty)
                {
                    _strokePaint.StrokeWidth = _strokeWidth;

                    //canvas.DrawColor( Rect.Left - _strokeWidth, Rect.Top - _strokeWidth, Rect.Right + _strokeWidth, Rect.Bottom + _strokeWidth, clear);

                    canvas.DrawRect(0,0,this.Width, Rect.Top - _strokeWidth, _backgroundPaint);
                    canvas.DrawRect(0, Rect.Bottom + _strokeWidth, this.Width, this.Height, _backgroundPaint);

                    canvas.DrawRect(0, Rect.Top - _strokeWidth, Rect.Left - _strokeWidth, Rect.Bottom + _strokeWidth, _backgroundPaint);

                    canvas.DrawRect(Rect.Right + _strokeWidth, Rect.Top - _strokeWidth, this.Width, Rect.Bottom + _strokeWidth, _backgroundPaint);

                    canvas.DrawRect(Rect.Left - _strokeWidth, Rect.Top - _strokeWidth, Rect.Right + _strokeWidth, Rect.Bottom + _strokeWidth, _strokePaint);
                 

                    //_textureView.UnlockCanvasAndPost(canvas);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
    }
}