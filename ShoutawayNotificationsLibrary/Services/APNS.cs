using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;

/// <summary>
/// Apple Push Notification Service
/// </summary>
public class APNS
{
    #region Constants
    private const string hostFeedbackProduction = "feedback.push.apple.com";
    private const string hostFeedbackSandbox = "feedback.sandbox.push.apple.com";
    private const string hostPushProduction = "gateway.push.apple.com";
    private const string hostPushSandbox = "gateway.sandbox.push.apple.com";
    private const int timeoutMilli = 60000;
    #endregion

    #region Properties
    public X509Certificate2 Certificate { get; set; }
    public string FeedbackHost { get { return (Sandbox ? hostFeedbackSandbox : hostFeedbackProduction); } }
    public int FeedbackPort { get { return 2196; } }
    public string PushHost { get { return (Sandbox ? hostPushSandbox : hostPushProduction); } }
    public int PushPort { get { return 2195; } }
    public bool Sandbox { get; set; }
    #endregion

    #region Contructor
    public APNS() : this(true)
    {
    }

    public APNS(bool Sandbox) : this(Sandbox, null)
    {
    }

    public APNS(bool Sandbox, X509Certificate2 Certificate)
    {
        this.Certificate = Certificate;
        this.Sandbox = Sandbox;
    }

    public APNS(bool Sandbox, string CertificatePath, string CertificatePassword) : this(Sandbox, new X509Certificate2(CertificatePath, CertificatePassword))
    {
    }
    #endregion

    #region Public methods
    public List<APNSFeedback> Receive()
    {
        bool certificateExpired;
        return Receive(out certificateExpired);
    }

    public List<APNSFeedback> Receive(out bool CertificateExpired)
    {
        CertificateExpired = false;
        string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        List<APNSFeedback> list = new List<APNSFeedback>();
        TcpClient client = null;
        SslStream apnsStream = null;

        try
        {
            // Has certificate expired?
            CertificateExpired = (Certificate.NotAfter < DateTime.Now);

            if (!CertificateExpired)
            {
                // Load certificate
                X509CertificateCollection certificatesCollection = new X509CertificateCollection { Certificate };

                // Open TCP and SSL
                client = new TcpClient();
                client.ReceiveTimeout = timeoutMilli;
                client.SendTimeout = timeoutMilli;
                client.Connect(FeedbackHost, FeedbackPort);
                apnsStream = new SslStream(client.GetStream(), false, validateServerCertificate, null);
                apnsStream.ReadTimeout = timeoutMilli;
                apnsStream.WriteTimeout = timeoutMilli;

                bool authed = false;

                try
                {
                    // Authenticate
                    apnsStream.AuthenticateAsClient(FeedbackHost, certificatesCollection, SslProtocols.Tls, true);
                    authed = true;
                }
                catch (AuthenticationException ex)
                {
                    authed = false;
                }

                if (authed)
                {
                    // Get the first feedback
                    byte[] buffer = new byte[38];
                    int recd = apnsStream.Read(buffer, 0, buffer.Length);

                    while (recd > 0)
                    {
                        try
                        {
                            APNSFeedback feedback = new APNSFeedback();

                            // Get seconds since 1970
                            byte[] bSeconds = new byte[4];
                            Array.Copy(buffer, 0, bSeconds, 0, bSeconds.Length);
                            if (BitConverter.IsLittleEndian)
                                Array.Reverse(bSeconds);
                            int tSeconds = BitConverter.ToInt32(bSeconds, 0);

                            // Add seconds since 1970 to that date
                            feedback.TimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(tSeconds);

                            // Get device token
                            byte[] bDeviceToken = new byte[32];
                            Array.Copy(buffer, 6, bDeviceToken, 0, bDeviceToken.Length);
                            feedback.DeviceToken = BitConverter.ToString(bDeviceToken).Replace("-", "").ToLower().Trim();

                            // Add feedback to list
                            list.Add(feedback);
                        }
                        catch (Exception ex)
                        {
                        }

                        // Clear buffer
                        Array.Clear(buffer, 0, buffer.Length);

                        // Get next feedback
                        recd = apnsStream.Read(buffer, 0, buffer.Length);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Helper.Log(className, method, "Error: " + ex.Message, false);
        }
        finally
        {
            // Close connections
            if (apnsStream != null)
            {
                apnsStream.Close();
                apnsStream.Dispose();
            }

            if (client != null)
                client.Close();
        }


        return list;
    }

    public bool Send(APNSMessage Message)
    {
        bool certificateExpired;
        return Send(Message, out certificateExpired);
    }

    public bool Send(APNSMessage Message, out bool CertificateExpired)
    {
	   CertificateExpired = false;
        string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        bool sent = false;
        TcpClient client = null;
        SslStream apnsStream = null;

        try
        {
            // Has certificate expired?
            CertificateExpired = (Certificate.NotAfter < DateTime.Now);

            if (!CertificateExpired)
            {
                int maxLength = 255; // Max 256 characters minus first payload byte

                // Load certificate
                X509CertificateCollection certificatesCollection = new X509CertificateCollection { Certificate };

                // Open TCP and SSL
                client = new TcpClient();
                client.ReceiveTimeout = timeoutMilli;
                client.SendTimeout = timeoutMilli;
                client.Connect(PushHost, PushPort);
                apnsStream = new SslStream(client.GetStream(), false, validateServerCertificate, null);
                apnsStream.ReadTimeout = timeoutMilli;
                apnsStream.WriteTimeout = timeoutMilli;

                bool authed = false;

                try
                {
                    // Authenticate
                    apnsStream.AuthenticateAsClient(PushHost, certificatesCollection, System.Security.Authentication.SslProtocols.Tls, true);
                    authed = apnsStream.IsAuthenticated;
                }
                catch (AuthenticationException ex)
                {
                    authed = false;
				Helper.Log(className, method, "Error: " + ex.Message, false);
			 }

                if (authed)
                {
				// Encode a test message into a byte array.
				MemoryStream memoryStream = new MemoryStream();
                    BinaryWriter writer = new BinaryWriter(memoryStream);

                    writer.Write((byte)0);  //The command
                    writer.Write((byte)0);  //The first byte of the deviceId length (big-endian first byte)
                    writer.Write((byte)32); //The deviceId length (big-endian second byte)
                    writer.Write(toByteArray(Message.DeviceToken.ToUpper()));

                    // Handle special characters
                    string text = handleSpecialCharacters(Message.Alert);

                    // Get data
                    Dictionary<string, object> data = new Dictionary<string, object>();
                    data.Add("alert", text);
                    data.Add("badge", Message.Badge);
                    string soundFile = "notification.wav";

                    if (Message.Extras != null)
                    {
                        foreach (string extraKey in Message.Extras.Keys)
                        {
                            // Handle special characters
                            string extraText = handleSpecialCharacters(Message.Extras[extraKey]);
                            if (extraKey == "Id")
                            {
                                soundFile = getfileName(extraText);
                            }
                        }
                    }
                    data.Add("sound", soundFile);

                    // Get payload
                    string payload = getPayload(data);

                    if (Message.Extras != null)
                    {
                        foreach (string extraKey in Message.Extras.Keys)
                        {
                            // Handle special characters
                            string extraText = handleSpecialCharacters(Message.Extras[extraKey]);

                            // Add to data and get payload
                            data.Add(extraKey, extraText);
                            string tempload = getPayload(data);

                            // Set payload or remove from data
                            if (tempload.Length <= maxLength)
                                payload = tempload;
                            else
                                data.Remove(extraKey);
                        }
                    }
                    

                    if (payload.Length > maxLength)
                    {
                        // Trim text
                        int reduceBy = payload.Length - maxLength + 3;
                        text = Message.Alert.Trim().Substring(0, Message.Alert.Trim().Length - reduceBy) + "...";

                        // Handle special characters
                        text = handleSpecialCharacters(text);

                        // Set data
                        data["alert"] = text;

                        // Get payload
                        payload = getPayload(data);
                    }

                    writer.Write((byte)0); // First byte of payload length; (big-endian first byte)
                    writer.Write((byte)payload.Length); // Payload length (big-endian second byte)

                    byte[] b1 = System.Text.Encoding.UTF8.GetBytes(payload);
                    writer.Write(b1);
                    writer.Flush();

                    byte[] array = memoryStream.ToArray();
                    apnsStream.Write(array);
                    apnsStream.Flush();

				// Close the client connection.
				client.Close();

                    // Set sent
                    sent = true;
			 }
                else
                {
				Helper.Log(className, method, "Not authorized " + Message.DeviceToken, false);
			 }
            }
        }
        catch (Exception ex)
        {
            Helper.Log(className, method, "Error: " + ex.Message, false);
        }
        finally
        {
            // Close connections
            if (apnsStream != null)
            {
                apnsStream.Close();
                apnsStream.Dispose();
            }

            if (client != null)
                client.Close();
        }

	   return sent;
    }
    #endregion

    #region Private methods
    private string getfileName(string id)
    {
        string fileName = "notification.wav";
        try
        {
            int idNo = Convert.ToInt32(id);
            switch (idNo)
            {
                case 1001:                
                    fileName = "new_message.wav";
                    break;
                default:
                    fileName = "notification.wav";
                    break;
            }
        }
        catch
        {
        }
        return fileName;
    }
    private string getPayload(Dictionary<string, object> Data)
    {
        // Build up payload
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"aps\":{");

        foreach (string key in Data.Keys)
        {
            if (new List<Type> { typeof(Int16), typeof(Int32), typeof(Int64) }.Contains(Data[key].GetType()))
                sb.AppendFormat("\"{0}\":{1}", key, Data[key]);
            else if (Data[key].GetType() == typeof(String))
                sb.AppendFormat("\"{0}\":\"{1}\"", key, Data[key]);
            sb.Append(",");
        }

        if (Data.Count > 0)
            sb.Remove(sb.Length - 1, 1);
        sb.Append("}}");

        return sb.ToString();
    }

    private string handleSpecialCharacters(string Text)
    {
        // Handle special characters asc <= 32 and asc >=127 should be replaced with their unicode versions in format \uXXXX where XXXX is a four digit hex e.g. \u00C5
        StringBuilder encodedString = new StringBuilder();

        foreach (char c in Text)
        {
            if ((int)c < 32 || (int)c > 127)
                encodedString.Append("\\u" + String.Format("{0:x4}", Convert.ToUInt32(c)).ToUpper());
            else
                encodedString.Append(c);
        }

        return encodedString.ToString().Replace("\"", "'");
    }

    private byte[] toByteArray(string Hex)
    {
        return Enumerable.Range(0, Hex.Length).Where(x => 0 == x % 2).Select(x => Convert.ToByte(Hex.Substring(x, 2), 16)).ToArray();
    }

    private bool validateServerCertificate(object Sender, X509Certificate Certificate, X509Chain Chain, SslPolicyErrors SslPolicyErrors)
    {
	   if (SslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
	   {
		  return true;
	   }
	   else if (SslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch)
	   {
		  Zone z = Zone.CreateFromUrl(((HttpWebRequest)Sender).RequestUri.ToString());
		  if (z.SecurityZone == System.Security.SecurityZone.Intranet || z.SecurityZone == System.Security.SecurityZone.MyComputer)
			 return true;
		  Helper.Log("APNS", "validateServerCertificate", "Error: " + SslPolicyErrors.ToString(), false);
		  return false;
	   }

	   return true;
    }
    #endregion
}