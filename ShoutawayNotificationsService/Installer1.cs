using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace ShoutawayNotificationsService
{
    [RunInstaller(true)]
    public partial class Installer1 : System.Configuration.Install.Installer
    {
        #region Variables
        private ServiceInstaller serviceInstaller;
        private ServiceProcessInstaller processInstaller;
        #endregion

        #region Constructor
        public Installer1()
        {
            InitializeComponent();

            // Instantiate and configure installer
            processInstaller = new ServiceProcessInstaller();
            processInstaller.Account = ServiceAccount.LocalSystem;
            Installers.Add(processInstaller);

            // Instantiate and configure installer
            serviceInstaller = new ServiceInstaller();
            serviceInstaller.Description = "TXL";
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().GetName().Name);
            Installers.Add(serviceInstaller);
        }
        #endregion

        #region Events
        private void Installer1_AfterInstall(object sender, InstallEventArgs e)
        {
            ServiceController service = new ServiceController(this.serviceInstaller.ServiceName);
            service.Start();
        }

        private void Installer1_BeforeUninstall(object sender, InstallEventArgs e)
        {
            ServiceController service = new ServiceController(this.serviceInstaller.ServiceName);
            service.Stop();
        }
        #endregion
    }
}