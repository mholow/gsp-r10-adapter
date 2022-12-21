namespace gspro_r10
{
  public enum LogMessageType
  {
    Incoming,
    Outgoing,
    Informational,
    Error
  }
  public static class SimpleLogger
  {
    public static void LogR10Info(string message) => LogR10Message(message, LogMessageType.Informational);
    public static void LogR10Error(string message) => LogR10Message(message, LogMessageType.Error);
    public static void LogR10Outgoing(string message) => LogR10Message(message, LogMessageType.Outgoing);
    public static void LogR10Incoming(string message) => LogR10Message(message, LogMessageType.Incoming);
    public static void LogR10Message(string message, LogMessageType type) => LogMessage(message, "R10", type);

    public static void LogGSPInfo(string message) => LogGSPMessage(message, LogMessageType.Informational);
    public static void LogGSPError(string message) => LogGSPMessage(message, LogMessageType.Error);
    public static void LogGSPOutgoing(string message) => LogGSPMessage(message, LogMessageType.Outgoing);
    public static void LogGSPIncoming(string message) => LogGSPMessage(message, LogMessageType.Incoming);
    public static void LogGSPMessage(string message, LogMessageType type) => LogMessage(message, "GSP", type);


    public static void LogMessage(string message, string component, LogMessageType type)
    {
      Console.Write($"{DateTime.Now.ToString("HH:MM:ss.fff")} ");
      switch(component)
      {
        case "R10":
          Console.ForegroundColor = type == LogMessageType.Incoming ? ConsoleColor.Cyan : ConsoleColor.Blue;
          break;
        case "GSP":
          Console.ForegroundColor = type == LogMessageType.Incoming ? ConsoleColor.Green : ConsoleColor.DarkGreen;
          break;
      }
      Console.Write($"{component} ");
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
          return;
      }
      Console.WriteLine(message);
    }
  }
}