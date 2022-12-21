using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using gspro_r10.OpenConnect;
using gspro_r10.R10;
using Microsoft.Extensions.Configuration;

namespace gspro_r10
{
  public class ConnectionManager
  {
    private R10ConnectionServer R10Server;
    private OpenConnectClient OpenConnectClient;

    private JsonSerializerOptions serializerSettings = new JsonSerializerOptions() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private int shotNumber = 0;
    public ConnectionManager(IConfigurationRoot configuration)
    {

      R10Server = new R10ConnectionServer(this, int.Parse(configuration["r10ServerPort"] ?? "2483"));
      R10Server.Start();
      OpenConnectClient = new OpenConnectClient(configuration["openConnectIP"] ?? "127.0.0.1", int.Parse(configuration["openConnectPort"] ?? "921"));
      OpenConnectClient.ConnectAsync();
    }


    internal void SendShot(R10Session session, R10.BallData? ballData, R10.ClubData? clubData)
    {
      string openConnectMessage = JsonSerializer.Serialize(OpenConnectApiMessage.CreateShotData(
        shotNumber++,
        OpenConnect.BallData.FromR10BallData(ballData),
        OpenConnect.ClubData.FromR10ClubData(clubData)
      ), serializerSettings);

      OpenConnectClient.SendAsync(openConnectMessage);

      session.CompleteShot();

    }
  }
}