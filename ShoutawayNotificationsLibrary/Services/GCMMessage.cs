using System.Collections.Generic;
using System.Runtime.Serialization;

/// <summary>
/// Google Cloud Messaging Message
/// </summary>
[DataContract]
public class GCMMessage
{
    #region Properties
    [DataMember(Name = "collapse_key")]
    public string CollapsableKey { get; set; }

    [DataMember(Name = "data")]
    public Dictionary<string, string> Data { get; set; }

    [DataMember(Name = "delay_while_idle")]
    public bool DelayWhileIdle { get; set; }

    [DataMember(Name = "registration_ids")]
    public string[] RegistrationIds { get; set; }

    [DataMember(Name = "time_to_live")]
    public int? TimeToLive { get; set; }
    #endregion
}