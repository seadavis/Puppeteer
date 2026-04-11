namespace Puppeteer.Models
{
    public interface ICameraService
    {
        event Action<byte[]> OnFrame;
        Task StartAsync();
        Task StopAsync();
    }
}
