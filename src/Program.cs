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

      IConfigurationRoot configuration = builder.Build();
      
      Console.Title = "GSP-R10 Connect";
      Console.WriteLine("GSP - R10 Bridge starting. Press enter key to close");
      new ConnectionManager(configuration);
      Console.ReadLine();

      Console.WriteLine("Exiting...");
    }
  }
}
