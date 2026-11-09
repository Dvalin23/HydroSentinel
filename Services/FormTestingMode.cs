namespace HydroSentinel.Services;

public interface IFormTestingMode
{
    bool Enabled { get; }
    string BannerText { get; }
}

public sealed class FormTestingMode(IWebHostEnvironment environment) : IFormTestingMode
{
    public bool Enabled => environment.IsDevelopment();
    public string BannerText => "DEVELOPMENT ENVIRONMENT — Use test accounts only. Account changes may be saved to the local development database.";
}
