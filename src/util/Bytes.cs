namespace gspro_r10
{
  public static class Bytes
  {
    public static byte[] ToByteArray(this string hexString)
    {
      byte[] retval = new byte[hexString.Length / 2];
      for (int i = 0; i < hexString.Length; i += 2)
      {
        retval[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
      }
      return retval;
    }

    public static string ToHexString(this byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);
    public static string ToHexString(this IEnumerable<byte> bytes) => ToHexString(bytes.ToArray());
    public static byte[] Checksum(this IEnumerable<byte> bytes) => Crc16.ComputeChecksum(bytes);
  }
}