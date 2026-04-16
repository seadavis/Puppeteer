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
                400, 400,
                ImageFormatType.Jpeg,
                4);

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

        byte[] jpeg;

        try
        {
            var buffer = image.GetPlanes()[0].Buffer;

            jpeg = new byte[buffer.Remaining()];
            buffer.Get(jpeg);
        }
        finally
        {
            image.Close();
        }

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
