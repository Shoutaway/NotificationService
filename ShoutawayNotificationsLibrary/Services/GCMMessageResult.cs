using System.Runtime.Serialization;

/// <summary>
/// Google Cloud Messaging Message Response
/// </summary>
[DataContract]
public class GCMMessageResult
{
    #region Properties
    [DataMember(Name = "error")]
    public string Error { get; set; }

    [DataMember(Name = "message_id")]
    public string MessageId { get; set; }

    [DataMember(Name = "registration_id")]
    public string RegistrationId { get; set; }
    #endregion
}