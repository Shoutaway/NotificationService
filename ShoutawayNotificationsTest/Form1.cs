using ShoutawayNotificationsLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShoutawayNotificationsTest
{
    public partial class Form1 : Form
    {
        #region Variables
        private Library library = null;
        private System.Threading.Timer serviceTimer;
        private bool useThreads = false;
        #endregion

        #region Constructor
        public Form1()
        {
            InitializeComponent();

            // Initialize application
            init();
        }
        #endregion

        #region Service events
        private void serviceTimer_Tick(object state)
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;

            try
            {
                // Get settings from ini-file
                getSettings();

                if (shouldRun())
                {
                    // Run
                    run();

                    // Set last run
                    setLastRun();
                }
            }
            catch (Exception ex)
            {
                Helper.Log(className, method, "Error: " + ex.Message, false);
            }
            finally
            {
                // Reset service timer
                serviceTimer.Change(Convert.ToInt64(Helper.ServiceSettings["TimerInterval"]) * 1000, 0);
            }
        }
        #endregion

        #region Service methods
        private void createDefaultIni()
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;

            try
            {
                // Get ini-file path
                string iniFilePath = Helper.RootPath + Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location) + "_config.ini";

                // Create and fill ini file
                StreamWriter file = new StreamWriter(iniFilePath);
                file.WriteLine("[ConnectionString]");
                file.WriteLine("Notifications=Server=10.46.200.192;Database=Shoutaway;User=statUser;Password=guadeluPa;");
                file.WriteLine();
                file.WriteLine("[ServiceSettings]");
                file.WriteLine(";Debug=[0|1]");
                file.WriteLine("Debug=0");
                file.WriteLine(";TimerInterval=[INTERVAL IN SECONDS]");
                file.WriteLine("TimerInterval=30");
                file.WriteLine(";RunInterval=[INTERVAL IN SECONDS|EMPTY FOR RUNTIMES]");
                file.WriteLine("RunInterval=");
                file.WriteLine(";RunTimes=00:30,12:45");
                file.WriteLine("RunTimes=");
                file.WriteLine(";LastRun=[DATE|EMPTY FOR DIRECT RUN]");
                file.WriteLine("LastRun=");
                file.WriteLine("[ProjectSettings]");
                file.WriteLine(@"WebPath=\\10.46.200.192\Web\Shoutaway\ShoutawayAdmin\");
                file.Close();
            }
            catch (Exception ex)
            {
                Helper.Log(className, method, "Error: " + ex.Message, false);
            }
        }

        private void getSettings()
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;

            try
            {
                // Get ini-file path
                string iniFilePath = Helper.RootPath + Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location) + "_config.ini";

                // Create ini-file
                if (!File.Exists(iniFilePath))
                    createDefaultIni();

                if (File.Exists(iniFilePath))
                {
                    // Get settings from ini-file
                    IniFile ini = new IniFile(iniFilePath);
                    Dictionary<string, string> connectionStrings = new Dictionary<string, string>();
                    Dictionary<string, string> projectSettings = new Dictionary<string, string>();
                    Dictionary<string, string> serviceSettings = new Dictionary<string, string>();

                    connectionStrings.Add("Notifications", ini.IniReadValue("ConnectionString", "Notifications"));

                    serviceSettings.Add("Debug", ini.IniReadValue("ServiceSettings", "Debug"));
                    serviceSettings.Add("TimerInterval", ini.IniReadValue("ServiceSettings", "TimerInterval"));
                    serviceSettings.Add("RunInterval", ini.IniReadValue("ServiceSettings", "RunInterval"));
                    serviceSettings.Add("RunTimes", ini.IniReadValue("ServiceSettings", "RunTimes"));
                    serviceSettings.Add("LastRun", ini.IniReadValue("ServiceSettings", "LastRun"));

                    projectSettings.Add("WebPath", ini.IniReadValue("ProjectSettings", "WebPath"));

                    // Set global settings
                    Helper.ConnectionStrings = connectionStrings;
                    Helper.ProjectSettings = projectSettings;
                    Helper.ServiceSettings = serviceSettings;

                    // Configure service timer
                    if (serviceTimer == null)
                        serviceTimer = new System.Threading.Timer(serviceTimer_Tick, null, 0, 0);
                }
            }
            catch (Exception ex)
            {
                Helper.Log(className, method, "Error: " + ex.Message, false);
            }
        }

        private void init()
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;

            try
            {
                // Set root path
                string rootPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                rootPath = rootPath.Substring(0, rootPath.LastIndexOf(@"\") + 1);
                Helper.RootPath = rootPath;

                // Get settings from ini-file
                getSettings();
            }
            catch (Exception ex)
            {
                Helper.Log(className, method, "Error: " + ex.Message, false);
            }
        }

        private void setLastRun()
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;

            try
            {
                // Save last run
                string iniFilePath = Helper.RootPath + Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location) + "_config.ini";
                IniFile ini = new IniFile(iniFilePath);
                ini.IniWriteValue("ServiceSettings", "LastRun", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                ini = null;
            }
            catch (Exception ex)
            {
                Helper.Log(className, method, "Error: " + ex.Message, false);
            }
        }

        private bool shouldRun()
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
            bool run = false;

            try
            {
                if (string.IsNullOrEmpty(Helper.ServiceSettings["LastRun"]))
                {
                    // Empty last run
                    run = true;
                }
                else if (!string.IsNullOrEmpty(Helper.ServiceSettings["RunInterval"]))
                {
                    // Get last run
                    DateTime lastRun = Convert.ToDateTime(Helper.ServiceSettings["LastRun"]);

                    // Compare last run with interval
                    if (lastRun.AddSeconds(Convert.ToInt32(Helper.ServiceSettings["RunInterval"])) <= DateTime.Now)
                        run = true;
                }
                else if (!string.IsNullOrEmpty(Helper.ServiceSettings["RunTimes"]))
                {
                    // Get last run
                    DateTime lastRun = Convert.ToDateTime(Helper.ServiceSettings["LastRun"]);

                    // Get run times
                    List<DateTime> runTimes = new List<DateTime>();
                    foreach (string time in Helper.ServiceSettings["RunTimes"].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries))
                        runTimes.Add(Convert.ToDateTime(DateTime.Today.ToString("yyyy-MM-dd") + " " + time));

                    // Compare last run with run times
                    if (runTimes.Any(x => lastRun < x && x < DateTime.Now))
                        run = true;
                }
            }
            catch (Exception ex)
            {
                Helper.Log(className, method, "Error: " + ex.Message, false);
            }

            return run;
        }
        #endregion

        #region Project methods
        private void run()
        {
            string className = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.Name;
            string method = System.Reflection.MethodBase.GetCurrentMethod().Name;

            // Init library
            if (library == null)
                library = new Library();

            if (!library.IsCheckingNotifications)
            {
                // Check notifications
                if (useThreads)
                    new Task(() => { library.CheckNotifications(useThreads); }).Start();
                else
                    library.CheckNotifications(useThreads);
            }
            else
                Helper.Log(className, method, "Already checking notifications", true);
        }
        #endregion
    }
}