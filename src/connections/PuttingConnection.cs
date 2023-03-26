using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using gspro_r10.Putting;
using Microsoft.Extensions.Configuration;
using NetCoreServer;


namespace gspro_r10
{

  class HttpPuttingSession : HttpSession
  {
    public ConnectionManager ConnectionManager;

    public HttpPuttingServer PuttingServer { get; private set; }

    public HttpPuttingSession(HttpPuttingServer server, ConnectionManager connectionManager) : base(server)
    {
      ConnectionManager = connectionManager;
      PuttingServer = server;
    }

    protected override void OnReceivedRequest(HttpRequest request)
    {
      try
      {
        if ((request.Method == "POST") || (request.Method == "PUT"))
        {
          string key = request.Url;
          string value = request.Body;
          PuttingDataMessage? message = JsonSerializer.Deserialize<PuttingDataMessage>(value);
          PuttingLogger.LogPuttIncoming(request.Body);

          if (message != null)
          {
            if (PuttingServer.PuttingEnabled)
              ConnectionManager.SendShot(BallDataFromPuttingBallData(message?.ballData), null);
            else
              PuttingLogger.LogPuttInfo("Not sending Putt because selected club is not putter");
          }

        }
        SendResponseAsync(Response.MakeOkResponse());

      }
      catch
      {
        SendResponseAsync(Response.MakeErrorResponse());
      }

    }

    protected override void OnReceivedRequestError(HttpRequest request, string error)
    {
      PuttingLogger.LogPuttError($"Request error: {error}");
    }

    protected override void OnError(SocketError error)
    {
      PuttingLogger.LogPuttError($"HTTP session caught an error: {error}");
    }

    public static OpenConnect.BallData? BallDataFromPuttingBallData(Putting.BallData? puttBallData)
    {
      if (puttBallData == null) return null;
      return new OpenConnect.BallData()
      {
        Speed = puttBallData.BallSpeed,
        //SpinAxis = -1 * (puttBallData.SpinAxis < 90 ? r10BallData.SpinAxis : r10BallData.SpinAxis - 360),
        TotalSpin = puttBallData.TotalSpin,
        HLA = puttBallData.LaunchDirection,
        VLA = 0,
      };
    }
  }

  class HttpPuttingServer : NetCoreServer.HttpServer
  {
    [DllImport("User32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("User32.dll")]
    private static extern bool BringWindowToTop(IntPtr handle);
    [DllImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const UInt32 SWP_NOSIZE = 0x0001;
    private const UInt32 SWP_NOMOVE = 0x0002;
    private const UInt32 TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;
    public ConnectionManager ConnectionManager;
    public IConfigurationSection Configuration;
    public bool PuttingEnabled = false;
    private bool mDisposing;

    public Process? PuttingProcess { get; private set; }
    public bool OnlyLaunchWhenPutting { get; }
    public bool KeepPuttingCamOnTop { get; }
    public bool LaunchBallTracker { get; }
    public int WebcamIndex { get; }
    public string BallColor { get; }
    public int CamPreviewWidth { get; }
    public string ExecutablePath { get; }
    public string ExecutableName { get; }
    public string AdditionalExeArgs { get; }
    public bool HideExeLogs { get; }
    protected override TcpSession CreateSession() => new HttpPuttingSession(this, ConnectionManager);
    protected override void OnError(SocketError error) => PuttingLogger.LogPuttError($"HTTP session caught an error: {error}");

    public HttpPuttingServer(ConnectionManager connectionManager, IConfigurationSection configuration)
      : base(IPAddress.Any, int.Parse(configuration["port"] ?? "8888"))
    {
      ConnectionManager = connectionManager;
      Configuration = configuration;
      PuttingLogger.LogPuttInfo($"Starting putting receiver on port {Port}");

      OnlyLaunchWhenPutting = bool.Parse(Configuration["onlyLaunchWhenPutting"] ?? "true");
      KeepPuttingCamOnTop = bool.Parse(Configuration["keepPuttingCamOnTop"] ?? "false");
      LaunchBallTracker = bool.Parse(Configuration["launchBallTracker"] ?? "false");
      WebcamIndex = int.Parse(Configuration["webcamIndex"] ?? "0");
      BallColor = Configuration["ballColor"] ?? "white";
      CamPreviewWidth = int.Parse(Configuration["camPreviewWidth"] ?? "640");
      ExecutablePath = Configuration["exePath"] ?? "./ball_tracking/ball_tracking.exe";
      ExecutableName = Path.GetFileName(ExecutablePath);
      AdditionalExeArgs = Configuration["additionalExeArgs"] ?? string.Empty;
      HideExeLogs = bool.Parse(Configuration["hideExeLogs"] ?? "false");

      if (LaunchBallTracker && CheckBallTrackingExists() && !OnlyLaunchWhenPutting)
      {
        LaunchProcess();
        if (KeepPuttingCamOnTop)
          FocusProcess();
      }

      ConnectionManager.ClubChanged += (o, e) =>
      {
        if (e.Club == OpenConnect.Club.PT)
        {
          StartPutting();
        }
        else
        {
          if (PuttingEnabled)
            StopPutting();
        }
      };
    }

    private void StartPutting()
    {
      PuttingEnabled = true;
      if (LaunchBallTracker && PuttingProcess == null)
      {
        LaunchProcess();
      }

      if (KeepPuttingCamOnTop)
        FocusProcess();
    }
    private void StopPutting(Boolean force = false)
    {
      PuttingEnabled = false;
      if ((LaunchBallTracker && OnlyLaunchWhenPutting) || force)
        KillProcess();
    }


    private bool CheckBallTrackingExists()
    {
        if (!File.Exists(ExecutablePath))
        {
          PuttingLogger.LogPuttError($"{ExecutablePath} file not found.");
          PuttingLogger.LogPuttError("Download latest release of ball_tracking program from https://github.com/alleexx/cam-putting-py/releases and unzip to same folder as this program");
          return false;
        }
        return true;
    }

    private void LaunchProcess()
    {
        if (CheckBallTrackingExists())
        {
          ProcessStartInfo startInfo = new ProcessStartInfo(ExecutablePath);
          startInfo.Arguments = $"-w {WebcamIndex} -c {BallColor} -r {CamPreviewWidth}";
          startInfo.WorkingDirectory = Path.GetDirectoryName(ExecutablePath);
          startInfo.WindowStyle = ProcessWindowStyle.Normal;
          startInfo.CreateNoWindow = false;
          startInfo.UseShellExecute = false;
          startInfo.RedirectStandardOutput = true;
          startInfo.RedirectStandardError = true;
          startInfo.RedirectStandardInput = true;
          startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "TRUE";
          startInfo.Environment["PYTHONUNBUFFERED"] = "TRUE";

          if (!string.IsNullOrWhiteSpace(AdditionalExeArgs))
          {
            startInfo.Arguments = $"{startInfo.Arguments} {AdditionalExeArgs}";
          }

          PuttingLogger.LogPuttInfo($"Starting putting camera: '{startInfo.FileName} {startInfo.Arguments}' ");
          PuttingProcess = Process.Start(startInfo);

          if (PuttingProcess == null)
          {
            PuttingLogger.LogPuttError("Error opening putting process");
            return;
          }
          Console.WriteLine(PuttingProcess.BasePriority);

          PuttingProcess.EnableRaisingEvents = true;
          PuttingProcess.OutputDataReceived += OnBallTrackerLogs;
          PuttingProcess.ErrorDataReceived += OnBallTrackerErrors;
          PuttingProcess.BeginOutputReadLine();
          PuttingProcess.BeginErrorReadLine();

          PuttingProcess.Exited += OnPuttingProcessClosed;

          int attempts = 0;
          while (((int)PuttingProcess.MainWindowHandle) == 0)
          {
            if (attempts % 5 == 0)
            {
              PuttingLogger.LogPuttInfo("Waiting for main window to launch...");
            }
            Thread.Sleep(1000);
            attempts += 1;
          }
          PuttingLogger.LogPuttInfo("Main Window launched");
        }
    }

    private void OnBallTrackerLogs(object _, DataReceivedEventArgs args)
    {
      if (!string.IsNullOrWhiteSpace(args.Data) && !HideExeLogs)
        PuttingLogger.LogPuttInfo($"[{ExecutableName}] {args.Data}");
    }

    private void OnBallTrackerErrors(object _, DataReceivedEventArgs args)
    {
      if (!string.IsNullOrWhiteSpace(args.Data) && !HideExeLogs)
        PuttingLogger.LogPuttError($"[{ExecutableName}] {args.Data}");
    }
    private void OnPuttingProcessClosed(object? _, EventArgs? args)
    {
      PuttingProcess = null;
      if ((PuttingEnabled || (!OnlyLaunchWhenPutting)) && !mDisposing)
      {
        PuttingLogger.LogPuttError($"{ExecutableName} closed unexpectedly. Reopening...");
        StartPutting();
      }
      else
      {
        PuttingLogger.LogPuttInfo($"{ExecutableName} closed");
      }
    }

    private void FocusProcess()
    {
      if (PuttingProcess != null)
      {
        IntPtr handle = PuttingProcess.MainWindowHandle;
        if (handle != IntPtr.Zero)
        {
          SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
          BringWindowToTop(handle);
          SetForegroundWindow(handle);
        }
      }
    }
    
    private void KillProcess()
    {
      PuttingLogger.LogPuttInfo("Shutting down putting camera");
      PuttingProcess?.Kill();
      PuttingProcess = null;
    }


    protected override void Dispose(bool disposing)
    {
      if (!IsDisposed)
      {
        if (disposing)
        {
          mDisposing = true;
          StopPutting(force: true);
          base.Dispose(disposing);
        }
      }
    }
  }

  public static class PuttingLogger
  {
    public static void LogPuttInfo(string message) => LogPuttMessage(message, LogMessageType.Informational);
    public static void LogPuttError(string message) => LogPuttMessage(message, LogMessageType.Error);
    public static void LogPuttOutgoing(string message) => LogPuttMessage(message, LogMessageType.Outgoing);
    public static void LogPuttIncoming(string message) => LogPuttMessage(message, LogMessageType.Incoming);
    public static void LogPuttMessage(string message, LogMessageType type) => BaseLogger.LogMessage(message, "Putt", type, ConsoleColor.Yellow);

  }
}