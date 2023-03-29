using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using InTheHand.Bluetooth;
using LaunchMonitor.Proto;
using static LaunchMonitor.Proto.State.Types;
using static LaunchMonitor.Proto.SubscribeResponse.Types;
using static LaunchMonitor.Proto.WakeUpResponse.Types;

namespace gspro_r10.bluetooth
{
  public class LaunchMonitorDevice : BaseDevice
  {
    internal static Guid MEASUREMENT_SERVICE_UUID = Guid.Parse("6A4E3400-667B-11E3-949A-0800200C9A66");
    internal static Guid MEASUREMENT_CHARACTERISTIC_UUID = Guid.Parse("6A4E3401-667B-11E3-949A-0800200C9A66");
    internal static Guid CONTROL_POINT_CHARACTERISTIC_UUID = Guid.Parse("6A4E3402-667B-11E3-949A-0800200C9A66");
    internal static Guid STATUS_CHARACTERISTIC_UUID = Guid.Parse("6A4E3403-667B-11E3-949A-0800200C9A66");

    private StateType _currentState;
    public StateType CurrentState { 
      get { return _currentState; } 
      private set {
        _currentState = value;
        Ready = value == StateType.Waiting;
      }
    }

    public Tilt? DeviceTilt { get; private set; }

    private bool _ready = false;
    public bool Ready { 
      get {return _ready; } 
      private set {
        bool changed = _ready != value;
        _ready = value;
        if (changed)
          ReadinessChanged?.Invoke(this, new ReadinessChangedEventArgs(){ Ready = value });
      }
    }

    public bool AutoWake { get; set; } = true;
    public bool CalibrateTiltOnConnect { get; set; } = true;

    public event ReadinessChangedEventHandler? ReadinessChanged;
    public delegate void ReadinessChangedEventHandler(object sender, ReadinessChangedEventArgs e);
    public class ReadinessChangedEventArgs: EventArgs
    {
      public bool Ready { get; set; }
    }

    public event ErrorEventHandler? Error;
    public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);
    public class ErrorEventArgs: EventArgs
    {
      public string? Message { get; set; }
      public Error.Types.Severity Severity { get; set; }
    }

    public event MetricsEventHandler? ShotMetrics;
    public delegate void MetricsEventHandler(object sender, MetricsEventArgs e);
    public class MetricsEventArgs: EventArgs
    {
      public Metrics? Metrics { get; set; }
    }

    public LaunchMonitorDevice(BluetoothDevice device) : base(device)
    {

    }

    public override bool Setup()
    {
      if (DebugLogging)
        BaseLogger.LogDebug("Subscribing to measurement service");
      GattService measService = Device.Gatt.GetPrimaryServiceAsync(MEASUREMENT_SERVICE_UUID).WaitAsync(TimeSpan.FromSeconds(5)).Result;
      GattCharacteristic measCharacteristic = measService.GetCharacteristicAsync(MEASUREMENT_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(5)).Result;
      if (!measCharacteristic.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(5)))
      {
        BluetoothLogger.Error("Error subscribing to measurement characteristic");
      }

      // Bytes that come after each shot. No idea how to parse these
      measCharacteristic.CharacteristicValueChanged += (o, e) => {};
      if (DebugLogging)
        BaseLogger.LogDebug("Subscribing to control service");
      GattCharacteristic controlPoint = measService.GetCharacteristicAsync(CONTROL_POINT_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(5)).Result;
      if (!controlPoint.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(5)))
      {
        BluetoothLogger.Error("Error subscribing to the control characteristic");
      }
      // Response to waiting device through controlPointInterface. Unused for now
      controlPoint.CharacteristicValueChanged += (o, e) => { };

      if (DebugLogging)
        BaseLogger.LogDebug("Subscribing to status service");
      GattCharacteristic statusCharacteristic = measService.GetCharacteristicAsync(STATUS_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(5)).Result;
      if (!statusCharacteristic.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(5)))
      {
        BluetoothLogger.Error("Error subscribing to the status characteristic");
      }
      statusCharacteristic.CharacteristicValueChanged += (o, e) =>
      {
        bool isAwake = e.Value[1] == (byte)0;
        bool isReady = e.Value[2] == (byte)0;

        // the following is unused in favor of the status change notifications and wake control provided by the protobuf service
        // if (!isAwake)
        // {
        //   controlPoint.WriteValueWithResponseAsync(new byte[] { 0x00 }).Wait();
        // }
      };


      bool baseSetupSuccess = base.Setup();
      if (!baseSetupSuccess)
      {
        BluetoothLogger.Error("Error during base device setup");
        return false;
      }


      WakeDevice();
      CurrentState = StatusRequest() ?? StateType.Error;
      DeviceTilt = GetDeviceTilt();
      SubscribeToAlerts().First();

      if (CalibrateTiltOnConnect)
        StartTiltCalibration();

      return true;
    }

    public override void HandleProtobufRequest(IMessage request)
    {
      if (request is WrapperProto WrapperProtoRequest)
      {
        AlertDetails notification = WrapperProtoRequest.Event.Notification.AlertNotification_;
        if (notification.State != null)
        {
          CurrentState = notification.State.State_;
          if (notification.State.State_ == StateType.Standby)
          {
            if (AutoWake)
            {
              BluetoothLogger.Info("Device asleep. Sending wakeup call");
              WakeDevice();
            }
            else
            {
              BluetoothLogger.Error("Device asleep. Wake device using button (or enable autowake in settings)");
            }
          }
        }
        if (notification.Error != null && notification.Error.HasCode)
        {
          Error?.Invoke(this, new ErrorEventArgs() { Message = $"{notification.Error.Code.ToString()} {notification.Error.DeviceTilt}", Severity = notification.Error.Severity });
        }
        if (notification.Metrics != null)
        {
          ShotMetrics?.Invoke(this, new MetricsEventArgs() { Metrics = notification.Metrics });
        }
        if (notification.TiltCalibration != null)
        {
          DeviceTilt = GetDeviceTilt();
        }
      }
    }

    public Tilt? GetDeviceTilt()
    {
      IMessage? resp = SendProtobufRequest(
        new WrapperProto() { Service = new LaunchMonitorService() { TiltRequest = new TiltRequest() } }
      );

      if (resp is WrapperProto WrapperProtoResponse)
        return WrapperProtoResponse.Service.TiltResponse.Tilt;
      
      return null;
    }

    public ResponseStatus? WakeDevice()
    {
      IMessage? resp = SendProtobufRequest(
        new WrapperProto() { Service = new LaunchMonitorService() { WakeUpRequest = new WakeUpRequest() } }
      );

      if (resp is WrapperProto WrapperProtoResponse)
        return WrapperProtoResponse.Service.WakeUpResponse.Status;

      return null;
    }

    public StateType? StatusRequest()
    {
      IMessage? resp = SendProtobufRequest(
        new WrapperProto() { Service = new LaunchMonitorService() { StatusRequest = new StatusRequest() } }
      );

      if (resp is WrapperProto WrapperProtoResponse)
        return WrapperProtoResponse.Service.StatusResponse.State.State_;

      return null;
    }

    public List<AlertStatusMessage> SubscribeToAlerts()
    {
      IMessage? resp = SendProtobufRequest(
        new WrapperProto()
        {
          Event = new EventSharing()
          {
            SubscribeRequest = new SubscribeRequest()
            {
              Alerts = { new List<AlertMessage>() { new AlertMessage() { Type = AlertNotification.Types.AlertType.LaunchMonitor } } }
            }
          }
        }
      );

      if (resp is WrapperProto WrapperProtoResponse)
        return WrapperProtoResponse.Event.SubscribeRespose.AlertStatus.ToList();

      return new List<AlertStatusMessage>();

    }

    public bool ShotConfig(float temperature, float humidity, float altitude, float airDensity, float teeRange)
    {
      IMessage? resp = SendProtobufRequest(new WrapperProto()
      {
        Service = new LaunchMonitorService()
        {
          ShotConfigRequest = new ShotConfigRequest()
          {
            Temperature = temperature,
            Humidity = humidity,
            Altitude = altitude,
            AirDensity = airDensity,
            TeeRange = teeRange
          }
        }
      });

      if (resp is WrapperProto WrapperProtoResponse)
        return WrapperProtoResponse.Service.ShotConfigResponse.Success;

      return false;
    }

    public ResetTiltCalibrationResponse.Types.Status? ResetTiltCalibrartion(bool shouldReset = true)
    {
      IMessage? resp = SendProtobufRequest(
        new WrapperProto() { Service = new LaunchMonitorService() { ResetTiltCalRequest = new ResetTiltCalibrationRequest() { ShouldReset = shouldReset } } }
      );

      if (resp is WrapperProto WrapperProtoResponse)
        return WrapperProtoResponse.Service.ResetTiltCalResponse.Status;

      return null;
    }

    public StartTiltCalibrationResponse.Types.CalibrationStatus? StartTiltCalibration(bool shouldReset = true)
    {
      IMessage? resp = SendProtobufRequest(
        new WrapperProto() { Service = new LaunchMonitorService() { StartTiltCalRequest = new StartTiltCalibrationRequest() } }
      );

      if (resp is WrapperProto WrapperProtoResponse)
        return WrapperProtoResponse.Service.StartTiltCalResponse.Status;

      return null;
    }

    protected override void Dispose(bool disposing)
    {
      foreach (var d in ReadinessChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
        ReadinessChanged -= (d as ReadinessChangedEventHandler);

      foreach (var d in Error?.GetInvocationList() ?? Array.Empty<Delegate>())
        Error -= (d as ErrorEventHandler);

      foreach (var d in ShotMetrics?.GetInvocationList() ?? Array.Empty<Delegate>())
        ShotMetrics -= (d as MetricsEventHandler);

      base.Dispose(disposing);
    }
  }
}