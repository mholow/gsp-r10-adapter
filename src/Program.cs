using Microsoft.Extensions.Configuration;

namespace gspro_r10
{
  class Program
  {
    public static void Main()
    {
      
      IConfigurationBuilder builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory());

      if (File.Exists(Path.Join(Directory.GetCurrentDirectory(), "settings.json")))
      {
        builder.AddJsonFile("settings.json");
      }
      else
      {
        BaseLogger.LogMessage($"settings.json file not found or could not be opened in {Directory.GetCurrentDirectory()}", "Main", LogMessageType.Error);
      }

      IConfigurationRoot configuration = builder.Build();
      
      Console.Title = "GSP-R10 Connect";
      BaseLogger.LogMessage("GSP - R10 Bridge starting. Press enter key to close", "Main");
      ConnectionManager manager = new ConnectionManager(configuration);
      Console.ReadLine();
      BaseLogger.LogMessage("Shutting down...", "Main");
      manager.Dispose();
      BaseLogger.LogMessage("Exiting...", "Main");
    }
  }
}
