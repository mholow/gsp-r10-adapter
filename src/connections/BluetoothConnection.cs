using gspro_r10.OpenConnect;
using InTheHand.Bluetooth;
using LaunchMonitor.Proto;
using gspro_r10.bluetooth;
using Microsoft.Extensions.Configuration;

namespace gspro_r10
{
  public class BluetoothConnection
  {
    private static readonly double METERS_PER_S_TO_MILES_PER_HOUR = 2.2369;
    private static readonly float FEET_TO_METERS = 1 / 3.281f;
    public ConnectionManager ConnectionManager { get; }
    public IConfigurationSection Configuration { get; }
    public int ReconnectInterval { get; }
    public LaunchMonitorDevice? LaunchMonitor { get; private set; }
    public BluetoothDevice? Device { get; private set; }

    public BluetoothConnection(ConnectionManager connectionManager, IConfigurationSection configuration)
    {
      ConnectionManager = connectionManager;
      Configuration = configuration;
      ReconnectInterval = int.Parse(configuration["reconnectInterval"] ?? "5");
      Task.Run(ConnectToDevice);
    }

    private void ConnectToDevice()
    {
      string deviceName = Configuration["bluetoothDeviceName"] ?? "Approach R10";
      Device = FindDevice(deviceName);
      if (Device == null)
      {
        BluetoothLogger.Error($"Could not find '{deviceName}' in list of paired devices.");
        BluetoothLogger.Error("Device must be paired through computer bluetooth settings before running");
        BluetoothLogger.Error("If device is paired, make sure name matches exactly what is set in 'bluetoothDeviceName' in settings.json");
        return;
      }

      do 
      {
        BluetoothLogger.Info($"Connecting to {Device.Name}: {Device.Id}");
        Device.Gatt.ConnectAsync().Wait();

        if (!Device.Gatt.IsConnected)
        {
          BluetoothLogger.Info($"Could not connect to bluetooth device. Waiting {ReconnectInterval} seconds before trying again");
          Thread.Sleep(TimeSpan.FromSeconds(ReconnectInterval));
        }
      }
      while (!Device.Gatt.IsConnected);

      Device.Gatt.AutoConnect = true;

      BluetoothLogger.Info($"Connected to Launch Monitor");
      LaunchMonitor = SetupLaunchMonitor(Device);
      Device.GattServerDisconnected += OnDeviceDisconnected;
    }

    private void OnDeviceDisconnected(object? sender, EventArgs args)
    {
        BluetoothLogger.Error("Lost bluetooth connection");
        LaunchMonitor?.Dispose();
        if (Device != null)
          Device.GattServerDisconnected -= OnDeviceDisconnected;

        Task.Run(ConnectToDevice);
    }

    private LaunchMonitorDevice? SetupLaunchMonitor(BluetoothDevice device)
    {
      LaunchMonitorDevice lm = new LaunchMonitorDevice(device);
      lm.AutoWake = bool.Parse(Configuration["autoWake"] ?? "false");
      lm.CalibrateTiltOnConnect = bool.Parse(Configuration["calibrateTiltOnConnect"] ?? "false");

      lm.DebugLogging =  bool.Parse(Configuration["debugLogging"] ?? "false");

      lm.MessageRecieved += (o, e) => BluetoothLogger.Incoming(e.Message?.ToString() ?? string.Empty);
      lm.MessageSent += (o, e) => BluetoothLogger.Outgoing(e.Message?.ToString() ?? string.Empty);
      lm.BatteryLifeUpdated += (o, e) => BluetoothLogger.Info($"Battery Life Updated: {e.Battery}%");
      lm.Error += (o, e) => BluetoothLogger.Error($"{e.Severity}: {e.Message}");

      if (bool.Parse(Configuration["sendStatusChangesToGSP"] ?? "false"))
      {
        lm.ReadinessChanged += (o, e) => {
            ConnectionManager.SendLaunchMonitorReadyUpdate(e.Ready);
        };
      }

      lm.ShotMetrics += (o, e) => {
        ConnectionManager.SendShot(
          BallDataFromLaunchMonitorMetrics(e.Metrics?.BallMetrics),
          ClubDataFromLaunchMonitorMetrics(e.Metrics?.ClubMetrics)
        );
      };

      if (!lm.Setup())
      {
        BluetoothLogger.Error("Failed Device Setup");
        return null;
      }

      float temperature = float.Parse(Configuration["temperature"] ?? "60");
      float humidity = float.Parse(Configuration["humidity"] ?? "1");
      float altitude = float.Parse(Configuration["altitude"] ?? "0");
      float airDensity = float.Parse(Configuration["airDensity"] ?? "1");
      float teeDistanceInFeet = float.Parse(Configuration["teeDistanceInFeet"] ?? "7");;
      float teeRange = teeDistanceInFeet * FEET_TO_METERS;
      
      lm.ShotConfig(temperature, humidity, altitude, airDensity, teeRange);

      BluetoothLogger.Info($"Device Setup Complete: ");
      BluetoothLogger.Info($"   Model: {lm.Model}");
      BluetoothLogger.Info($"   Firmware: {lm.Firmware}");
      BluetoothLogger.Info($"   Bluetooth ID: {lm.Device.Id}");
      BluetoothLogger.Info($"   Battery: {lm.Battery}%");
      BluetoothLogger.Info($"   Current State: {lm.CurrentState}");
      BluetoothLogger.Info($"   Tilt: {lm.DeviceTilt}");

      return lm;
    }

    private BluetoothDevice? FindDevice(string deviceName)
    {
      foreach (BluetoothDevice pairedDev in Bluetooth.GetPairedDevicesAsync().Result)
        if (pairedDev.Name == deviceName)
          return pairedDev;
      return null;
    }

    public static BallData? BallDataFromLaunchMonitorMetrics(BallMetrics? ballMetrics)
    {
      if (ballMetrics == null) return null;
      return new BallData()
      {
        HLA = ballMetrics.LaunchDirection,
        VLA = ballMetrics.LaunchAngle,
        Speed = ballMetrics.BallSpeed * METERS_PER_S_TO_MILES_PER_HOUR,
        SpinAxis = ballMetrics.SpinAxis * -1,
        TotalSpin = ballMetrics.TotalSpin,
        SideSpin = ballMetrics.TotalSpin * Math.Sin(-1 * ballMetrics.SpinAxis * Math.PI/180),
        BackSpin = ballMetrics.TotalSpin * Math.Cos(-1 * ballMetrics.SpinAxis * Math.PI/180)
      };
    }

    public static ClubData? ClubDataFromLaunchMonitorMetrics(ClubMetrics? clubMetrics)
    {
      if (clubMetrics == null) return null;
      return new ClubData()
      {
        Speed = clubMetrics.ClubHeadSpeed * METERS_PER_S_TO_MILES_PER_HOUR,
        SpeedAtImpact = clubMetrics.ClubHeadSpeed,
        AngleOfAttack = clubMetrics.AttackAngle,
        FaceToTarget = clubMetrics.ClubAngleFace,
        Path = clubMetrics.ClubAnglePath
      };
    }
  }

  public static class BluetoothLogger
  {
      public static void Info(string message) => LogBluetoothMessage(message, LogMessageType.Informational);
      public static void Error(string message) => LogBluetoothMessage(message, LogMessageType.Error);
      public static void Outgoing(string message) => LogBluetoothMessage(message, LogMessageType.Outgoing);
      public static void Incoming(string message) => LogBluetoothMessage(message, LogMessageType.Incoming);
      public static void LogBluetoothMessage(string message, LogMessageType type) => BaseLogger.LogMessage(message, "R10-BT", type, ConsoleColor.Magenta);
  }
}