using System;
using Windows.Security.ExchangeActiveSyncProvisioning;

namespace GlobalLIB
{
  public class RaspberryPi
  {
    public static bool EstRaspberryPi2()
    {
      EasClientDeviceInformation deviceInfo = new EasClientDeviceInformation();
      return deviceInfo.SystemProductName.IndexOf("Raspberry Pi 2", StringComparison.OrdinalIgnoreCase) >= 0;
    }
  }
}
