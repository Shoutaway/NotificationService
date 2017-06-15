using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// Google Cloud Messaging
/// </summary>
public class GCM
{
    #region Constants
    private const int timeoutMilli = 60000;
    private const string urlSend = "https://android.googleapis.com/gcm/send";
    #endregion

    #region Properties
    public string AuthorizationKey { get; set; }
    #endregion

    #region Contructor
    public GCM() : this(string.Empty)
    {
    }

    public GCM(string AuthorizationKey)
    {
        this.AuthorizationKey = AuthorizationKey;
    }
    #endregion

    #region Public methods
    public GCMMessageResponse Send(GCMMessage Message)
    {
        string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        GCMMessageResponse response = null;

        try
        {
            // Set post data
            string json = JsonConvert.SerializeObject(Message, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            byte[] postData = Encoding.UTF8.GetBytes(json);

            // Create request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlSend);
            request.Headers["Authorization"] = string.Format("key={0}", AuthorizationKey);
            request.ContentLength = postData.Length;
            request.ContentType = "application/json";
            request.Method = "POST";
            request.ReadWriteTimeout = timeoutMilli;
            request.Timeout = timeoutMilli;

            // Send post data
            Stream stream = request.GetRequestStream();
            stream.Write(postData, 0, postData.Length);
            stream.Close();

            // Get response
            string responseBody = new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();
            response = (GCMMessageResponse)JsonConvert.DeserializeObject(responseBody, typeof(GCMMessageResponse));
        }
        catch (Exception ex)
        {
            Helper.Log(className, method, "Error: " + ex.Message, false);
        }

        return response;
    }
    #endregion
}