namespace HydroSentinel.Services;

public interface IFormTestingMode
{
    bool Enabled { get; }
    string BannerText { get; }
}

public sealed class FormTestingMode(IWebHostEnvironment environment) : IFormTestingMode
{
    public bool Enabled => environment.IsDevelopment();
    public string BannerText => "DEVELOPMENT TEST MODE — Form submissions are temporary and are not saved.";
}
