using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml.Media.Imaging;

#if ANDROID
using Puppeteer.Platforms.Android;
#endif

namespace Puppeteer.Presentation;

public sealed partial class MainPage : Page
{
    private ICameraService _cameraService;
    private WriteableBitmap _bitmap;

    public MainPage()
    {
        try
        {
            this.InitializeComponent(); ;
            _bitmap = new WriteableBitmap(640, 480);
        }
        catch (Exception ex)
        {
            int test = 30;
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        _cameraService.OnFrame += _cameraService_OnFrame;
    }

    private void _cameraService_OnFrame(byte[] rgb)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            using (var stream = _bitmap.PixelBuffer.AsStream())
            {
                stream.Seek(0, SeekOrigin.Begin);

                // RGB from camera service (RGBRGBRGB...)
                stream.Write(rgb, 0, Math.Min(rgb.Length, 640 * 480 * 3));
            }

            _bitmap.Invalidate();
        });
    }

    private async Task StartAsyncSafe()
    {
        try
        {
#if ANDROID
            _cameraService = new CameraService(ContextHelper.Current);
#endif
            _cameraService.OnFrame += _cameraService_OnFrame;

            await _cameraService.StartAsync();

            var file = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///Assets/Scene.html"));

            var text = await FileIO.ReadTextAsync(file);

            DispatcherQueue.TryEnqueue(() =>
            {
                SceneView.NavigateToString(text);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
    

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        CameraImage.Source = _bitmap;

        _ = Task.Run(StartAsyncSafe);

    }

}
