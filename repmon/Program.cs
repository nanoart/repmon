using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace repmon
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 
        private static bool IsInstalled()
        {
            using (ServiceController controller =
                new ServiceController("MySQL Replication Monitor"))
            {
                try
                {
                    ServiceControllerStatus status = controller.Status;
                }
                catch
                {
                    return false;
                }
                return true;
            }
        }

        private static bool IsRunning()
        {
            using (ServiceController controller =
                new ServiceController("MySQL Replication Monitor"))
            {
                if (!IsInstalled()) return false;
                return (controller.Status == ServiceControllerStatus.Running);
            }
        }

        private static AssemblyInstaller GetInstaller()
        {
            AssemblyInstaller installer = new AssemblyInstaller(
                typeof(ProjectInstaller).Assembly, null);
            installer.UseNewContext = true;
            return installer;
        }
        private static void InstallService()
        {
            if (IsInstalled()) return;

            try
            {
                using (AssemblyInstaller installer = GetInstaller())
                {
                    IDictionary state = new Hashtable();
                    try
                    {
                        installer.Install(state);
                        installer.Commit(state);
                    }
                    catch
                    {
                        try
                        {
                            installer.Rollback(state);
                        }
                        catch { }
                        throw;
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private static void UninstallService()
        {
            if (!IsInstalled()) return;
            try
            {
                using (AssemblyInstaller installer = GetInstaller())
                {
                    IDictionary state = new Hashtable();
                    try
                    {
                        installer.Uninstall(state);
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
            catch
            {
                throw;
            }
        }


        private static void StartService()
        {
            if (!IsInstalled()) return;

            using (ServiceController controller =
                new ServiceController("MySQL Replication Monitor"))
            {
                try
                {
                    if (controller.Status != ServiceControllerStatus.Running)
                    {
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running,
                            TimeSpan.FromSeconds(10));
                    }
                }
                catch
                {
                    throw;
                }
            }
        }



        private static void StopService()
        {
            if (!IsInstalled()) return;
            using (ServiceController controller =
                new ServiceController("MySQL Replication Monitor"))
            {
                try
                {
                    if (controller.Status != ServiceControllerStatus.Stopped)
                    {
                        controller.Stop();
                        controller.WaitForStatus(ServiceControllerStatus.Stopped,
                             TimeSpan.FromSeconds(10));
                    }
                }
                catch
                {
                    throw;
                }
            }
        }
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Debug()
                            //                            .WriteTo.LiterateConsole()
                            .WriteTo.RollingFile(System.AppDomain.CurrentDomain.BaseDirectory+"logs\\repmon-{Date}.txt")
                            .CreateLogger();

            if (args.Length == 0)
            {
//                File.AppendAllText("c:\\temp\\mylog.txt", "Run it as a service");
                // Run your service normally.
                Log.Information("Run it as a service");
                ServiceBase[] ServicesToRun = new ServiceBase[] { new SmartFixService() };
                ServiceBase.Run(ServicesToRun);
            }
            else if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "-install":
                        InstallService();
                        StartService();
                        break;
                    case "-uninstall":
                        StopService();
                        UninstallService();
                        break;
                    case "-normal":
                        FullJob.Init();
                        FullJob.Bob();
                        break;
                    case "-testmail":
                        System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
                        FullJob.loadSettings("settings.json");
                        FullJob.notifyDBA(true);
                        Console.WriteLine("Done! check your email inbox or the log file");
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }



            //start test

            /*
                        System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);



                        FullJob.Init();
                        FullJob.Bob();
                        var credential = FullJob.getMySQLCredential("server.xml");
                        FullJob.loadSettings("settings.json");
                        FullJob.getDaulShieldBinFolder("ALG");
                        string contents = File.ReadAllText("exout.txt");
                        FullJob.findErrorCode(contents);
                                    Console.WriteLine();
            */

            //end test 
            /*
                        ServiceBase[] ServicesToRun;
                        ServicesToRun = new ServiceBase[]
                        {
                            new SmartFixService()
                        };
                        ServiceBase.Run(ServicesToRun);

            */


        }
    }
}
