using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace ShoutawayNotificationsLibrary
{
    public class Library
    {
        #region Variables
        private bool isCheckingNotifications = false;
        private object myLock = "lock";
        private List<int> notificationTags = new List<int>();
        #endregion

        #region Properties
        public bool IsCheckingNotifications
        {
            get
            {
                bool result;
                lock (this.myLock)
                {
                    result = this.isCheckingNotifications;
                }
                return result;
            }
            set
            {
                lock (this.myLock)
                {
                    this.isCheckingNotifications= value;
                }
            }
        }
        #endregion

        #region Public methods
        public void CheckNotifications(bool UseThreads)
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;

            if (!this.IsCheckingNotifications)
            {
                // Set checking
                this.IsCheckingNotifications = true;

                SqlConnection sqlConn = new SqlConnection(Helper.ConnectionStrings["Notifications"]);
                DateTime now = DateTime.Now;

                try
                {
                    // Open connection
                    sqlConn.Open();

                    // Get settings
                    string sql = "SELECT TOP 1 * FROM notificationsettings WHERE name='Shoutaway' ORDER BY id DESC";
                    Helper.Log(className, method, "SQL: " + sql, true);
                    SqlDataAdapter ad = new SqlDataAdapter(sql, sqlConn);
                    DataTable settings = new DataTable();
                    ad.Fill(settings);
                    Helper.Log(className, method, "SQL count: " + settings.Rows.Count, true);

                    if (settings.Rows.Count > 0)
                    {
                        // Get notifications
                        sql = "SELECT * FROM notifications WHERE status=0";
                        Helper.Log(className, method, "SQL: " + sql, true);
                        ad = new SqlDataAdapter(sql, sqlConn);
                        DataTable notifications = new DataTable();
                        ad.Fill(notifications);
                        Helper.Log(className, method, "SQL count: " + notifications.Rows.Count, true);

                        if (notifications.Rows.Count > 0)
                        {
                            // Group notifications
                            DataRow[][] grouped = notifications.AsEnumerable().GroupBy(x => x.Field<int>("tag")).Select(x => x.ToArray()).ToArray();

                            foreach (DataRow[] group in grouped)
                            {
                                // Check group
                                if (UseThreads)
                                    new Task(() =>
                                    {
                                        try
                                        {
                                            sendNotifications(settings.Rows[0], group);
                                        }
                                        catch (Exception ex)
                                        {
                                            Helper.Log(className, method, "Thread error: " + ex.Message, false);
                                        }
                                    }).Start();
                                else
                                    sendNotifications(settings.Rows[0], group);

                                // Sleep
                                System.Threading.Thread.Sleep(1);
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
                    // If open, close conn
                    if (sqlConn.State != ConnectionState.Closed)
                        sqlConn.Close();
                }

                // Log execution time
                TimeSpan timeSpan = new TimeSpan(DateTime.Now.Ticks - now.Ticks);
                Helper.Log(className, method, string.Format("Execution time: {0} min {1} sec", timeSpan.Minutes, timeSpan.Seconds), true);

                // Reset checking
                this.IsCheckingNotifications = false;
            }
            else
            {
                Helper.Log(className, method, "Already checking", true);
            }
        }
        #endregion

        #region Private methods
        private void sendNotifications(DataRow settings, DataRow[] notifications)
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
            int tag = Convert.ToInt32(notifications[0]["tag"]);

            if (!customerQueueContains(tag))
            {
                // Add to list
                customerQueueAdd(tag);

                SqlConnection sqlConn = new SqlConnection(Helper.ConnectionStrings["Notifications"]);
                SqlDataAdapter ad = null;
                SqlCommand cmd = null;
                string sql = string.Empty;
                int affectedRows = 0;
                DateTime now = DateTime.Now;

                try
                {
                    // Get certificate path
                    string certPath = Helper.ProjectSettings["WebPath"] + @"files\certificates\";

                    foreach (DataRow item in notifications)
                    {
                        int status = -1;
                        string itemId = item["id"].ToString().Trim();
                        int devicesExpected = 0;
                        int devicesSent = 0;

                        try
                        {
                            // Get params
                            int badge = Convert.ToInt32(item["badge"]);
                            Dictionary<string, string> extras = JsonConvert.DeserializeObject<Dictionary<string, string>>(item["extras"].ToString().Trim());
                            string message = item["message"].ToString().Trim();
                            long userId = Convert.ToInt64(item["userid"]);

                            // Open connection
                            if (sqlConn.State != ConnectionState.Open)
                                sqlConn.Open();

                            // Get user devices
                            sql = string.Format("SELECT id, service, deviceid FROM notificationdevices WHERE userid={0}", userId);
                            Helper.Log(className, method, string.Format("Item {0} - SQL: {1}", itemId, sql), true);
                            ad = new SqlDataAdapter(sql, sqlConn);
                            DataTable userDevices = new DataTable();
                            ad.Fill(userDevices);
                            Helper.Log(className, method, string.Format("Item {0} - SQL count: {1}", itemId, userDevices.Rows.Count), true);

                            if (userDevices != null && userDevices.Rows.Count > 0)
                            {
                                try
                                {
                                    if (Convert.ToBoolean(settings["apnsenabled"]))
                                    {
								// Get devices
								DataRow[] devices = userDevices.Select("service='apns'");

                                        if (devices.Length > 0)
                                        {
                                            // Sandbox?
                                            bool sandbox = Convert.ToBoolean(settings["apnssandbox"]);

                                            string certPath1 = certPath + string.Format("apns_{0}_{1}.p12", Convert.ToInt64(settings["id"]), (sandbox ? "dev" : "prod"));
                                            Helper.Log(className, method, certPath1, true);
                                            // APNS
                                            APNS apns = new APNS(sandbox, certPath + string.Format("apns_{0}_{1}.p12", Convert.ToInt64(settings["id"]), (sandbox ? "dev" : "prod")), settings[sandbox ? "apnscertificatedevpassword" : "apnscertificateprodpassword"].ToString());

                                            // Increase expected
                                            devicesExpected += devices.Length;

                                            foreach (DataRow device in devices)
                                            {
                                                // Send to APNS
                                                bool certificateExpired;
                                                bool sent = apns.Send(new APNSMessage
                                                {
                                                    Alert = message,
                                                    Badge = badge,
                                                    DeviceToken = device["deviceid"].ToString(),
                                                    Extras = extras
                                                }, out certificateExpired);

                                                if (certificateExpired)
                                                {
                                                    // Decrease expected
                                                    devicesExpected -= devices.Length;

                                                    // Update customer
                                                    sql = string.Format("UPDATE settings SET apnsenabled=0 WHERE id={0}", Convert.ToInt64(settings["id"]));
                                                    Helper.Log(className, method, "SQL: " + sql, true);
                                                    cmd = new SqlCommand(sql, sqlConn);
                                                    affectedRows = cmd.ExecuteNonQuery();
                                                    Helper.Log(className, method, "SQL affected rows: " + affectedRows, true);

                                                    break;
                                                }
                                                else if (sent)
                                                {
                                                    // Increase sent
                                                    devicesSent++;
                                                }

                                                // Sleep
                                                System.Threading.Thread.Sleep(1);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Helper.Log(className, method, string.Format("Item {0} - APNS error: {1}", itemId, ex.Message), false);
                                }

                                try
                                {
                                    if (Convert.ToBoolean(settings["gcmenabled"]))
                                    {
                                        // Get devices
                                        DataRow[] devices = userDevices.Select("service='gcm'");

                                        if (devices.Length > 0)
                                        {
                                            // Increase expected
                                            devicesExpected += devices.Length;

                                            // Get data
                                            Dictionary<string, string> data = new Dictionary<string, string>();
                                            data.Add("message", message);
                                            foreach (string extraKey in extras.Keys)
                                                data.Add(extraKey, extras[extraKey]);

                                            // Send to GCM
                                            GCM gcm = new GCM(settings["gcmauthkey"].ToString());

                                            foreach (DataRow device in devices)
                                            {
                                                GCMMessageResponse gcmResponse = gcm.Send(new GCMMessage
                                                {
                                                    Data = data,
                                                    RegistrationIds = new string[] { device["deviceid"].ToString() }
                                                });

                                                if (gcmResponse != null)
                                                {
                                                    if (gcmResponse.Success > 0)
                                                    {
                                                        // Increase sent
                                                        devicesSent++;
                                                    }
                                                    else if (gcmResponse.Results != null && gcmResponse.Results.Count > 0 && gcmResponse.Results[0].Error == "NotRegistered")
                                                    {
                                                        // Delete device
                                                        sql = string.Format("DELETE FROM notificationdevices WHERE service='gcm' AND deviceid='{0}'", device["deviceid"].ToString().Replace("'", "''"));
                                                        Helper.Log(className, method, string.Format("Item {0} - SQL: {1}", itemId, sql), true);
                                                        cmd = new SqlCommand(sql, sqlConn);
                                                        affectedRows = cmd.ExecuteNonQuery();
                                                        Helper.Log(className, method, string.Format("Item {0} - SQL affected rows: {1}", itemId, affectedRows), true);

                                                        // Decrease expected
                                                        devicesExpected--;
                                                    }
                                                    else
                                                    {
                                                        Helper.Log(className, method, string.Format("Item {0} - GCM response error: {1}", itemId, JsonConvert.SerializeObject(gcmResponse)), false);
                                                    }
                                                }

                                                // Sleep
                                                System.Threading.Thread.Sleep(1);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Helper.Log(className, method, string.Format("Item {0} - GCM error: {1}", itemId, ex.Message), false);
                                }

                                try
                                {
                                    if (Convert.ToBoolean(settings["mpnsenabled"]))
                                    {
                                        // Get devices
                                        DataRow[] devices = userDevices.Select("service='mpns'");

                                        if (devices.Length > 0)
                                        {
                                            // Increase expected
                                            devicesExpected += devices.Length;

                                            foreach (DataRow device in devices)
                                            {
                                                // MPNS
                                                MPNS mpns = new MPNS();

                                                // Send to MPNS
                                                bool active = true;
                                                bool sent = mpns.Send(new MPNSToast
                                                {
                                                    SubscriptionUri = device["deviceid"].ToString(),
                                                    SubTitle = message
                                                }, out active);

                                                if (sent)
                                                {
                                                    // Increase sent
                                                    devicesSent++;
                                                }
                                                else if (!active)
                                                {
                                                    // Delete device
                                                    sql = string.Format("DELETE FROM notificationdevices WHERE service='mpns' AND deviceid='{0}'", device["deviceid"].ToString().Replace("'", "''"));
                                                    Helper.Log(className, method, string.Format("Item {0} - SQL: {1}", itemId, sql), true);
                                                    cmd = new SqlCommand(sql, sqlConn);
                                                    affectedRows = cmd.ExecuteNonQuery();
                                                    Helper.Log(className, method, string.Format("Item {0} - SQL affected rows: {1}", itemId, affectedRows), true);

                                                    // Decrease expected
                                                    devicesExpected--;
                                                }

                                                // Sleep
                                                System.Threading.Thread.Sleep(1);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Helper.Log(className, method, string.Format("Item {0} - MPNS error: {1}", itemId, ex.Message), false);
                                }

                                try
                                {
                                    if (Convert.ToBoolean(settings["wnsenabled"]))
                                    {
                                        // Get devices
                                        DataRow[] devices = userDevices.Select("service='wns'");

                                        if (devices.Length > 0)
                                        {
                                            // Increase expected
                                            devicesExpected += devices.Length;

                                            foreach (DataRow device in devices)
                                            {
                                                // WNS
                                                WNS wns = new WNS();

                                                // Send to WNS
                                                bool active = true;
                                                bool sent = wns.Send(new WNSToast
                                                {
                                                    Secret = settings["wnssecret"].ToString(),
                                                    SID = settings["wnssid"].ToString(),
                                                    SubscriptionUri = device["deviceid"].ToString(),
                                                    Text = message
                                                }, out active);

                                                if (sent)
                                                {
                                                    // Increase sent
                                                    devicesSent++;
                                                }
                                                else if (!active)
                                                {
                                                    // Delete device
                                                    sql = string.Format("DELETE FROM notificationdevices WHERE service='wns' AND deviceid='{0}'", device["deviceid"].ToString().Replace("'", "''"));
                                                    Helper.Log(className, method, string.Format("Item {0} - SQL: {1}", itemId, sql), true);
                                                    cmd = new SqlCommand(sql, sqlConn);
                                                    affectedRows = cmd.ExecuteNonQuery();
                                                    Helper.Log(className, method, string.Format("Item {0} - SQL affected rows: {1}", itemId, affectedRows), true);

                                                    // Decrease expected
                                                    devicesExpected--;
                                                }

                                                // Sleep
                                                System.Threading.Thread.Sleep(1);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Helper.Log(className, method, string.Format("Item {0} - WNS error: {1}", itemId, ex.Message), false);
                                }
                            }

                            // Set status
                            status = 1;
                        }
                        catch (Exception ex)
                        {
                            Helper.Log(className, method, string.Format("Item {0} - Error: {1}", itemId, ex.Message), false);
                        }

                        // Update item
                        sql = string.Format("UPDATE notifications SET status={0}, statusdate=GETDATE(), devicesexpected={1}, devicessent={2} WHERE id='{3}'", status, devicesExpected, devicesSent, itemId.Replace("'", "''"));
                        Helper.Log(className, method, string.Format("Item {0} - SQL: {1}", itemId, sql), true);
                        cmd = new SqlCommand(sql, sqlConn);
                        affectedRows = cmd.ExecuteNonQuery();
                        Helper.Log(className, method, string.Format("Item {0} - SQL affected rows: {1}", itemId, affectedRows), true);

                        // Sleep
                        System.Threading.Thread.Sleep(1);
                    }

                    try
                    {
                        if (Convert.ToBoolean(settings["apnsenabled"]))
                        {
                            // Sandbox?
                            bool sandbox = Convert.ToBoolean(settings["apnssandbox"]);

                            // APNS
                            APNS apns = new APNS(sandbox, certPath + string.Format("apns_{0}_{1}.p12", Convert.ToInt64(settings["id"]), (sandbox ? "dev" : "prod")), settings[sandbox ? "apnscertificatedevpassword" : "apnscertificateprodpassword"].ToString());

                            // Get feedback
                            bool certificateExpired;
                            List<APNSFeedback> feedbacks = apns.Receive(out certificateExpired);

                            if (certificateExpired)
                            {
                                // Update customer
                                sql = string.Format("UPDATE notificationsettings SET apnsenabled=0 WHERE id={0}", Convert.ToInt64(settings["id"]));
                                Helper.Log(className, method, "SQL: {1}" + sql, true);
                                cmd = new SqlCommand(sql, sqlConn);
                                affectedRows = cmd.ExecuteNonQuery();
                                Helper.Log(className, method, "SQL affected rows: " + affectedRows, true);
                            }
                            else
                            {
                                foreach (APNSFeedback feedback in feedbacks)
                                {
                                    // Delete devices
                                    sql = string.Format("DELETE FROM notificationdevices WHERE service='apns' AND deviceid='{0}'", feedback.DeviceToken.Replace("'", "''"));
                                    Helper.Log(className, method, "SQL: " + sql, true);
                                    cmd = new SqlCommand(sql, sqlConn);
                                    affectedRows = cmd.ExecuteNonQuery();
                                    Helper.Log(className, method, "SQL affected rows: " + affectedRows, true);

                                    // Sleep
                                    System.Threading.Thread.Sleep(1);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.Log(className, method, "APNS error: " + ex.Message, false);
                    }
                }
                catch (Exception ex)
                {
                    Helper.Log(className, method, "Error: " + ex.Message, false);
                }
                finally
                {
                    // If open, close conn
                    if (sqlConn.State != ConnectionState.Closed)
                        sqlConn.Close();
                }

                // Log execution time
                TimeSpan timeSpan = new TimeSpan(DateTime.Now.Ticks - now.Ticks);
                Helper.Log(className, method, string.Format("Execution time: {0} min {1} sec", timeSpan.Minutes, timeSpan.Seconds), true);

                // Remove from list
                customerQueueRemove(tag);
            }
            else
            {
                Helper.Log(className, method, "Already checking", true);
            }
        }

        private void customerQueueAdd(int customerId)
        {
            lock (myLock)
                notificationTags.Add(customerId);
        }

        private bool customerQueueContains(int customerId)
        {
            lock (myLock)
                return notificationTags.Contains(customerId);
        }

        private int[] customerQueueGet()
        {
            lock (myLock)
                return notificationTags.Select(x => x).ToArray();
        }

        private void customerQueueRemove(int customerId)
        {
            lock (myLock)
                notificationTags.Remove(customerId);
        }
        #endregion
    }
}