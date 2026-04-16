using Windows.Foundation;
using Uno.WinUI.Graphics2DSK;
using SkiaSharp;

namespace Puppeteer.Presentation;

public partial class SKCanvasElementImpl : SKCanvasElement
{
    SKBitmap bitmap;
    int renderCount = 0;

    public byte[] JpegImageBytes 
    { 
        get => field; 
        set
        {
            field = value;
            DecodeJpeg();
        }
    }
    
    private void DecodeJpeg()
    {
        Task.Run(() => {
            bitmap = SKBitmap.Decode(JpegImageBytes);
            Invalidate();
        });
    }

    /* 
     * Small example on how to re render
     * private void SampleChanged(int newIndex)
    {
        Sample = Math.Min(Math.Max(0, newIndex), SampleCount - 1);
        Invalidate();
    } */

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {


        var minDim = Math.Min(area.Width, area.Height);

        SKBitmap nextBitMap;


        // rescale to fit the given area, assuming each drawing is 260x260
        canvas.Scale((float)(minDim / 260), (float)(minDim / 260));



        if (bitmap == null)
        {
            nextBitMap = new SKBitmap((int)area.Width, (int)area.Height);
            nextBitMap.Erase(SKColors.Purple);
            canvas.RotateDegrees(90);
        }
        else 
        {
            nextBitMap = RotateOnce(bitmap, 90);
            renderCount++;
        }
    
        canvas.DrawBitmap(nextBitMap, 0, 0);
    }

    SKBitmap RotateOnce(SKBitmap source, float degrees)
    {
        var rotated = new SKBitmap(source.Height, source.Width);

        using var canvas = new SKCanvas(rotated);

        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);

        canvas.DrawBitmap(source,
            -source.Width / 2f,
            -source.Height / 2f);

        return rotated;
    }

}
