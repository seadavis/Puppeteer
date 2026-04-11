namespace Puppeteer.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///Assets/Scene.html"));

                var text = await FileIO.ReadTextAsync(file);
                SceneView.NavigateToString(text);
            }
            catch (Exception ex)
            {
                int test = 30;
            }

        });
    }

}
