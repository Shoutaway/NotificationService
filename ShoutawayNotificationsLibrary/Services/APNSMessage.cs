using System.Collections.Generic;

/// <summary>
/// Apple Push Notification Service Message
/// </summary>
public class APNSMessage
{
    #region Properties
    public string Alert { get; set; }
    public int Badge { get; set; }
    public string DeviceToken { get; set; }
    public string Sound { get; set; }
    public Dictionary<string, string> Extras { get; set; }
    #endregion
}