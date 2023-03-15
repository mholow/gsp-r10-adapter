using System.Net;
using System.Text.Json;
using gspro_r10.api;
using gspro_r10.OpenConnect;
using Microsoft.Extensions.Configuration;
using NetCoreServer;

namespace gspro_r10
{
  class PuttingConnectionServer
  {
    private ConnectionManager _connectionManager;
    private HttpListener _httpListener;
    private bool _alive;

    public PuttingConnectionServer(ConnectionManager connectionManager, IConfigurationSection configuration)
    {
      _connectionManager = connectionManager;
      _httpListener = new HttpListener();
      int port = int.Parse(configuration["port"] ?? "8888");
      _httpListener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", port));
    }

    public void Start()
    {
      PuttingLogger.Info($"Putting HTTP Server starting");

      try
      {
        _httpListener.Start();
        _alive = true;

        while (_alive)
        {
          // Attempt to service the next message from the putting server
          HttpListenerContext context = _httpListener.GetContext();
          HttpListenerRequest req = context.Request;

          // Read in the putting metrics
          PuttingMessage puttingMessage;

          using (StreamReader reader = new StreamReader(req.InputStream))
          {
            puttingMessage = JsonSerializer.Deserialize<PuttingMessage>(reader.ReadToEnd()) ?? new PuttingMessage();
          }

          // Relay information about the putting message
          PuttingLogger.Incoming("Ball Speed: " + puttingMessage.ballData.BallSpeed);
          PuttingLogger.Incoming("Total Spin: " + puttingMessage.ballData.TotalSpin);
          PuttingLogger.Incoming("Launch Direction: " + puttingMessage.ballData.LaunchDirection);

          // Send the putt to GSPro
          _connectionManager.SendShot(BallDataFromPuttingMetrics(puttingMessage.ballData), null);

          // Respond with okay and successful result for now
          using HttpListenerResponse resp = context.Response;
          resp.StatusCode = (int)HttpStatusCode.OK;
          resp.StatusDescription = "Status OK";
          resp.Headers.Set("Content-Type", "application/json");

          PuttingResponse response = new PuttingResponse();
          response.result = true;

          byte[] buffer = JsonSerializer.SerializeToUtf8Bytes(response);
          resp.ContentLength64 = buffer.Length;
          using (Stream writer = resp.OutputStream)
          {
            writer.Write(buffer, 0, buffer.Length);
          }
        }

      }
      catch(Exception e)
      {
        PuttingLogger.Error(e.Message);
      }

      _httpListener.Stop();
    }

    public void Stop()
    {
      _alive = false;
    }

    private BallData? BallDataFromPuttingMetrics(PuttingBallData? puttingMetrics)
    {
      if (puttingMetrics == null) return null;
      return new BallData()
      {
        HLA = puttingMetrics.LaunchDirection,
        VLA = 0,
        Speed = puttingMetrics.BallSpeed,
        SpinAxis = 0,
        TotalSpin = puttingMetrics.TotalSpin,
        SideSpin = 0,
        BackSpin = 0
      };
    }
  }

  public static class PuttingLogger
  {
      public static void Info(string message) => LogPuttingMessage(message, LogMessageType.Informational);
      public static void Error(string message) => LogPuttingMessage(message, LogMessageType.Error);
      public static void Outgoing(string message) => LogPuttingMessage(message, LogMessageType.Outgoing);
      public static void Incoming(string message) => LogPuttingMessage(message, LogMessageType.Incoming);
      public static void LogPuttingMessage(string message, LogMessageType type) => BaseLogger.LogMessage(message, "PUTT", type, ConsoleColor.Yellow);
  }
}