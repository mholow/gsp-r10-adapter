namespace gspro_r10
{
  public static class COBS 
  {
    public static IEnumerable<byte> Encode(IEnumerable<byte> input) {
      List<byte> result = new List<byte>();
      int distanceIndex = 0;
      byte distance = 1;

      foreach (byte b in input)
      {
        if (b != 0 && distance < 255)
        {
          result.Add(b);
          distance++;
        }
        else
        {
          result.Insert(distanceIndex, (byte) distance);
          distanceIndex = result.Count;
          distance = 1;
        }
      }

      if(result.Count != 255 && result.Count > 0)
        result.Insert(distanceIndex, distance);

      return result;
    }

    public static IEnumerable<byte> Decode(IEnumerable<byte> input) {
      byte[] inputArray = input.ToArray();
      List<byte> result = new List<byte>();
      int distanceIndex = 0;
      byte distance = 1;

      while (distanceIndex < inputArray.Length)
      {
        distance = inputArray[distanceIndex];

        if (inputArray.Length < distanceIndex + distance || distance < 1)
          return new List<byte>();

        if (distance > 1)
          for (byte i = 1; i < distance; i++)
            result.Add(inputArray[distanceIndex + i]);

        distanceIndex += distance;

        if(distance < 0xFF && distanceIndex < inputArray.Length)
          result.Add(0);
      }

      return result;
    }
  }
}