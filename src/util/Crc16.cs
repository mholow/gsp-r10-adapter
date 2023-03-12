namespace gspro_r10
{
  public static class Crc16
  {
    const ushort polynomial = 0xA001;
    static readonly ushort[] table = new ushort[256];

    public static byte[] ComputeChecksum(IEnumerable<byte> bytes)
    {
      ushort crc = 0;
      foreach (byte b in bytes) crc = (ushort)((crc >> 8) ^ table[(byte)(crc ^ b)]);
      return BitConverter.GetBytes(crc);
    }

    static Crc16()
    {
      ushort value;
      ushort temp;
      for (ushort i = 0; i < table.Length; ++i)
      {
        value = 0;
        temp = i;
        for (byte j = 0; j < 8; ++j)
        {
          if (((value ^ temp) & 0x0001) != 0)
            value = (ushort)((value >> 1) ^ polynomial);
          else
            value >>= 1;
          temp >>= 1;
        }
        table[i] = value;
      }
    }
  }
}