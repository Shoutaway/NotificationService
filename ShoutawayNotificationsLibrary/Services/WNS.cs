using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

/// <summary>
/// Summary description for WNS
/// </summary>
public class WNS
{
    #region Constants
    private const int timeoutMilli = 60000;
    #endregion

    #region Contructor
    public WNS()
    {
    }
    #endregion

    #region Public methods
    public bool Send(WNSToast Message, out bool Active)
    {
        string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        bool sent = false;
        Active = true;

        try
        {
            // Create toast
            string toast = string.Format("<toast><visual><binding template=\"ToastText01\"><text id=\"1\">{0}</text></binding></visual></toast>", formatValue(Message.Text));

            // Send toast
            sent = send(Message.SID, Message.Secret, Message.SubscriptionUri, "wns/toast", toast, 0, out Active);
        }
        catch (Exception ex)
        {
            Helper.Log(className, method, "Error: " + ex.Message, false);
        }

        return sent;
    }
    #endregion

    #region Private methods
    private string formatValue(string value)
    {
        return value.Replace("<", "&lt;").Replace(">", "&gt;").Replace("&", "&amp;").Replace("‘", "&apos;").Replace("'", "&apos;").Replace("“", "&quot;").Replace("\"", "&quot;");
    }

    private string getAccessToken(string sid, string secret)
    {
        string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        string accessToken = string.Empty;

        try
        {
            // Set content
            string content = string.Format("grant_type=client_credentials&client_id={0}&client_secret={1}&scope=notify.windows.com", HttpUtility.UrlEncode(sid), HttpUtility.UrlEncode(secret));
            byte[] postData = Encoding.UTF8.GetBytes(content);

            // Create request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://login.live.com/accesstoken.srf");
            request.ContentLength = postData.Length;
            request.ContentType = "application/x-www-form-urlencoded";
            request.Method = "POST";
            request.ReadWriteTimeout = timeoutMilli;
            request.Timeout = timeoutMilli;

            // Send post data
            Stream stream = request.GetRequestStream();
            stream.Write(postData, 0, postData.Length);
            stream.Close();

            // Get response
            string response = new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();

            // Get access token
            JObject obj = JObject.Parse(response);
            accessToken = obj.Value<string>("access_token");
        }
        catch (Exception ex)
        {
            Helper.Log(className, method, "Error: " + ex.Message, false);
        }

        return accessToken;
    }

    private bool send(string sid, string secret, string url, string notificationType, string content, int tries, out bool active)
    {
        string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        bool sent = false;
        active = true;

        try
        {
            // Get access token
            string accessToken = getAccessToken(sid, secret);

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                // Set post data
                byte[] postData = Encoding.UTF8.GetBytes(content);

                // Create request
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.ContentLength = postData.Length;
                request.ContentType = "text/xml";
                request.Headers.Add("Authorization", string.Format("Bearer {0}", accessToken));
                request.Headers.Add("X-WNS-Type", notificationType);
                request.Method = "POST";
                request.ReadWriteTimeout = timeoutMilli;
                request.Timeout = timeoutMilli;

                // Send post data
                Stream stream = request.GetRequestStream();
                stream.Write(postData, 0, postData.Length);
                stream.Close();

                // Get response
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                // Set sent
                sent = (response.StatusCode == HttpStatusCode.OK);
            }
        }
        catch (WebException ex)
        {
            // Get HTTP status code
            HttpStatusCode status = ((HttpWebResponse)ex.Response).StatusCode;

            if (status == HttpStatusCode.Unauthorized)
            {
                // Retry
                if (tries < 5)
                    sent = send(sid, secret, url, notificationType, content, tries++, out active);
            }
            else if (status == HttpStatusCode.Gone || status == HttpStatusCode.NotFound)
            {
                // Set not active
                active = false;
            }

            Helper.Log(className, method, "Error: " + ex.Message, false);
        }
        catch (Exception ex)
        {
            Helper.Log(className, method, "Error: " + ex.Message, false);
        }

        return sent;
    }
    #endregion
}