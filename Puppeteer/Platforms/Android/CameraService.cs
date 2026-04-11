using System.Security.Permissions;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.Runtime;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Image = global::Android.Media.Image;
using Android;
using Android.App;
using Android.OS;
using Windows.Foundation.Metadata;

namespace Puppeteer.Platforms.Android;

class SessionCallback : CameraCaptureSession.StateCallback
{
    private readonly Action<CameraCaptureSession> _onConfigured;

    public SessionCallback(Action<CameraCaptureSession> onConfigured)
    {
        _onConfigured = onConfigured;
    }

    public override void OnConfigured(CameraCaptureSession session)
        => _onConfigured(session);

    public override void OnConfigureFailed(CameraCaptureSession session) { }
}

class StateCallback : CameraDevice.StateCallback
{
    private readonly Action<CameraDevice> _onOpened;

    public StateCallback(Action<CameraDevice> onOpened)
    {
        _onOpened = onOpened;
    }

    public override void OnOpened(CameraDevice camera)
        => _onOpened(camera);

    public override void OnDisconnected(CameraDevice camera)
        => camera.Close();

    public override void OnError(CameraDevice camera, CameraError error)
        => camera.Close();
}

public class CameraService : ICameraService
{
    public event Action<byte[]> OnFrame;

    private CameraDevice _camera;
    private CameraCaptureSession _session;
    private ImageReader _reader;
    private readonly Context _context;

    public CameraService(Context context)
    {
        _context = context;
    }

    private byte[] ConvertYuvToRgb(Image image)
    {
        var width = image.Width;
        var height = image.Height;

        var yPlane = image.GetPlanes()[0].Buffer;
        var uPlane = image.GetPlanes()[1].Buffer;
        var vPlane = image.GetPlanes()[2].Buffer;

        var yBytes = new byte[yPlane.Remaining()];
        var uBytes = new byte[uPlane.Remaining()];
        var vBytes = new byte[vPlane.Remaining()];

        yPlane.Get(yBytes);
        uPlane.Get(uBytes);
        vPlane.Get(vBytes);

        var rgb = new byte[width * height * 3];

        int uvIndex = 0;

        for (int y = 0; y < height; y++)
        {
            int yRow = y * width;

            for (int x = 0; x < width; x++)
            {
                int yIndex = yRow + x;

                int Y = yBytes[yIndex] & 0xFF;

                int uvPos = (y / 2) * (width / 2) + (x / 2);

                int U = uBytes[uvPos] & 0xFF;
                int V = vBytes[uvPos] & 0xFF;

                // YUV → RGB (simple formula)
                int C = Y - 16;
                int D = U - 128;
                int E = V - 128;

                int R = (298 * C + 409 * E + 128) >> 8;
                int G = (298 * C - 100 * D - 208 * E + 128) >> 8;
                int B = (298 * C + 516 * D + 128) >> 8;

                int rgbIndex = yIndex * 3;

                rgb[rgbIndex + 0] = (byte)Math.Clamp(R, 0, 255);
                rgb[rgbIndex + 1] = (byte)Math.Clamp(G, 0, 255);
                rgb[rgbIndex + 2] = (byte)Math.Clamp(B, 0, 255);
            }
        }

        return rgb;
    }

    private static LensFacing GetLensFacing(CameraCharacteristics c)
    {
        var obj = c.Get(CameraCharacteristics.LensFacing);

        return (LensFacing)(int)(Java.Lang.Integer)obj;
    }

    private string GetBackCameraId(CameraManager manager)
    {
        foreach (var id in manager.GetCameraIdList())
        {
            var c = manager.GetCameraCharacteristics(id);

            var facing = GetLensFacing(c);

            if (facing == LensFacing.Back)
                return id;
        }

        return manager.GetCameraIdList()[0]; // fallback
    }

    public async Task StartAsync()
    {
        try
        {
            var thread = new HandlerThread("CameraThread");
            thread.Start();

            var handler = new Handler(thread.Looper);
            var context = Uno.UI.ContextHelper.Current;
            if (ContextCompat.CheckSelfPermission(context, Manifest.Permission.Camera) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions((Activity)context, new string[] { Manifest.Permission.Camera }, 100);
            }

            var manager = (CameraManager)_context.GetSystemService(Context.CameraService);
            var cameraId = GetBackCameraId(manager);

            _reader = ImageReader.NewInstance(
                640, 480,
                ImageFormatType.Jpeg,
                2);

            _reader.SetOnImageAvailableListener(new Listener(this), handler);

            var tcs = new TaskCompletionSource<bool>();

            manager.OpenCamera(cameraId, new StateCallback(cam =>
            {
                _camera = cam;
                tcs.SetResult(true);
            }), handler);

            await tcs.Task;

            var surface = _reader.Surface;

            var requestBuilder =
                _camera.CreateCaptureRequest(CameraTemplate.Preview);

            requestBuilder.AddTarget(surface);

            _camera.CreateCaptureSession(
                new List<Surface> { surface },
                new SessionCallback(session =>
                {
                    _session = session;
                    _session.SetRepeatingRequest(requestBuilder.Build(), null, null);
                }),
               handler);
            
        }
        catch(Exception ex)
        {
            int test = 30;
        }
        
    }

    public Task StopAsync()
    {
        _session?.Close();
        _camera?.Close();
        _reader?.Close();
        return Task.CompletedTask;
    }


    private void HandleImage(Image image)
    {
       
        var buffer = image.GetPlanes()[0].Buffer;
        image.Close();

        byte[] jpeg = new byte[buffer.Remaining()];
        buffer.Get(jpeg);

        OnFrame?.Invoke(jpeg);


    }

    private class Listener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        private readonly CameraService _parent;

        public Listener(CameraService parent)
        {
            _parent = parent;
        }

        public void OnImageAvailable(ImageReader reader)
        {
            var image = reader.AcquireLatestImage();
            if (image == null) return;

            _parent.HandleImage(image);
        }
    }
}
