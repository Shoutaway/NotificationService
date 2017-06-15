using System.Collections.Generic;
using System.Runtime.Serialization;

/// <summary>
/// Google Cloud Messaging Message Response
/// </summary>
[DataContract]
public class GCMMessageResponse
{
    #region Properties
    [DataMember(Name = "canonical_ids")]
    public int CanonicalIds { get; set; }

    [DataMember(Name = "failure")]
    public int Failure { get; set; }

    [DataMember(Name = "multicast_id")]
    public long MulticastId { get; set; }

    [DataMember(Name = "results")]
    public List<GCMMessageResult> Results { get; set; }

    [DataMember(Name = "success")]
    public int Success { get; set; }
    #endregion
}