namespace gspro_r10
{
  public enum LogMessageType
  {
    Incoming,
    Outgoing,
    Informational,
    Error
  }
  public static class BaseLogger
  {
    private static object lockObject = new Object();

    public static void LogDebug(string message) => LogMessage(message, "DEBUG", LogMessageType.Informational, ConsoleColor.Gray);
    public static void LogMessage(string message, string component, LogMessageType type, ConsoleColor color)
    {
      lock (lockObject)
      {
        Console.Write($"{DateTime.Now.ToString("HH:MM:ss.fff")} ");
        Console.ForegroundColor = color;
        Console.Write($"{component.PadLeft(6)} ");
        Console.ResetColor();

        switch (type)
        {
          case LogMessageType.Incoming:
            Console.Write(">> ");
            break;
          case LogMessageType.Outgoing:
            Console.Write("<< ");
            break;
          case LogMessageType.Informational:
            Console.Write("|| ");
            break;
          case LogMessageType.Error:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"XX {message}");
            Console.ResetColor();
            return;
        }
        Console.WriteLine(message);
      }
    }
  }
}