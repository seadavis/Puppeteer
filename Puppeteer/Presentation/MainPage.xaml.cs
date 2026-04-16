using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using Google.Android.Material.Slider;
using Uno.WinUI.Graphics2DSK;



#if ANDROID
using Puppeteer.Platforms.Android;
#endif

namespace Puppeteer.Presentation;

public sealed partial class MainPage : Page
{
    private ICameraService _cameraService;
    private SKCanvasElementImpl _canvas;

    public MainPage()
    {
        try
        {
            this.InitializeComponent();
            if(SKCanvasElement.IsSupportedOnCurrentPlatform())
            {
                _canvas = new SKCanvasElementImpl();
                ImageBorder.Child = _canvas;
            }
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
        _canvas.JpegImageBytes = rgb;
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
        _ = Task.Run(StartAsyncSafe);

    }

}
