using System;
using System.IO;
using System.Net;
using System.Text;

/// <summary>
/// Summary description for MPNS
/// </summary>
public class MPNS
{
    #region Constants
    private const int timeoutMilli = 60000;
    #endregion

    #region Contructor
    public MPNS()
    {
    }
    #endregion

    #region Public methods
    public bool Send(MPNSToast Message, out bool Active)
    {
        string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        bool sent = false;
        Active = true;

        try
        {
            // Create toast
            string toast = "<?xml version=\"1.0\" encoding=\"utf-8\"?><wp:Notification xmlns:wp=\"WPNotification\"><wp:Toast>";
            if (!string.IsNullOrWhiteSpace(Message.Title))
                toast += string.Format("<wp:Text1>{0}</wp:Text1>", formatValue(Message.Title));
            if (!string.IsNullOrWhiteSpace(Message.SubTitle))
                toast += string.Format("<wp:Text2>{0}</wp:Text2>", formatValue(Message.SubTitle));
            if (!string.IsNullOrWhiteSpace(Message.Param))
                toast += string.Format("<wp:Param>{0}</wp:Param>", formatValue(Message.Param));
            toast += "</wp:Toast></wp:Notification>";

            // Send toast
            sent = send(Message.SubscriptionUri, "toast", 2, toast, out Active);
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

    private bool send(string url, string target, int notificationClass, string content, out bool active)
    {
        string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        bool sent = false;
        active = true;

        try
        {
            // Set post data
            byte[] postData = Encoding.UTF8.GetBytes(content);

            // Create request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentLength = postData.Length;
            request.ContentType = "text/xml";
            if (!string.IsNullOrWhiteSpace(target))
                request.Headers.Add("X-WindowsPhone-Target", target);
            request.Headers.Add("X-NotificationClass", notificationClass.ToString());
            request.Method = "POST";
            request.ReadWriteTimeout = timeoutMilli;
            request.Timeout = timeoutMilli;

            // Send post data
            Stream stream = request.GetRequestStream();
            stream.Write(postData, 0, postData.Length);
            stream.Close();

            // Get response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            string notificationStatus = response.Headers["X-NotificationStatus"];
            string subscriptionStatus = response.Headers["X-SubscriptionStatus"];
            string deviceConnectionStatus = response.Headers["X-DeviceConnectionStatus"];

            // Set sent
            sent = (!string.IsNullOrWhiteSpace(notificationStatus) && notificationStatus.ToLower() == "received");

            // Set active
            active = (!string.IsNullOrWhiteSpace(subscriptionStatus) && subscriptionStatus.ToLower() == "active");
        }
        catch (WebException ex)
        {
            // Get response
            string notificationStatus = ex.Response.Headers["X-NotificationStatus"];
            string subscriptionStatus = ex.Response.Headers["X-SubscriptionStatus"];
            string deviceConnectionStatus = ex.Response.Headers["X-DeviceConnectionStatus"];

            // Set active
            active = (!string.IsNullOrWhiteSpace(subscriptionStatus) && subscriptionStatus.ToLower() == "active");

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