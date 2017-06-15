using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public class Helper
{
    #region Variables
    private readonly static string className = "Helper";
    private static object myLock = "lock";
    #endregion

    #region Properties
    private static Dictionary<string, string> connectionStrings = new Dictionary<string, string>();
    public static Dictionary<string, string> ConnectionStrings
    {
        get
        {
            lock (myLock)
                return connectionStrings;
        }
        set
        {
            lock (myLock)
                connectionStrings = value;
        }
    }

    private static Dictionary<string, string> projectSettings = new Dictionary<string, string>();
    public static Dictionary<string, string> ProjectSettings
    {
        get
        {
            lock (myLock)
                return projectSettings;
        }
        set
        {
            lock (myLock)
                projectSettings = value;
        }
    }

    private static string rootPath = string.Empty;
    public static string RootPath
    {
        get
        {
            lock (myLock)
                return rootPath;
        }
        set
        {
            lock (myLock)
                rootPath = value;
        }
    }

    private static Dictionary<string, string> serviceSettings = new Dictionary<string, string>();
    public static Dictionary<string, string> ServiceSettings
    {
        get
        {
            lock (myLock)
                return serviceSettings;
        }
        set
        {
            lock (myLock)
                serviceSettings = value;
        }
    }
    #endregion

    #region Public methods
    public static void Log(string Class, string Method, string Message, bool Debug)
    {
        lock (myLock)
        {
            StreamWriter sw = null;

            try
            {
                if (((Debug && ServiceSettings["Debug"].ToString() == "1") || !Debug) && Message != "")
                {
                    // Get log-file path
                    string logPath = RootPath + "logs/";
                    if (!Directory.Exists(logPath))
                        Directory.CreateDirectory(logPath);
                    string filePath = logPath + DateTime.Today.ToString("yyyyMMdd") + ".log";

                    // Log message
                    sw = new StreamWriter(filePath, true);
                    if (Debug && ServiceSettings["Debug"].ToString() == "1")
                        sw.WriteLine("DEBUG > " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " - " + Class + "." + Method + "() - " + Message);
                    else if (!Debug)
                        sw.WriteLine("LOG > " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " - " + Class + "." + Method + "() - " + Message);

                    foreach (string file in Directory.GetFiles(logPath))
                    {
                        DateTime date;
                        if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyyMMdd", null, DateTimeStyles.None, out date))
                        {
                            // Delete file
                            if (DateTime.Today.AddDays(-30) > date)
                                File.Delete(file);
                        }
                        else
                        {
                            // Delete file
                            File.Delete(file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (sw != null)
                {
                    // Close file
                    sw.Close();
                    sw.Dispose();
                }
            }
        }
    }
    #endregion
}