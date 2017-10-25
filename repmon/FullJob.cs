using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace repmon
{
    class FullJob
    {
        public static int period = 300; //seconds
        public static int skipMax = 5;
        public static JArray tableScopes;
        public static string username;    //get from server.xml file
        public static string password;
        public static string pathDualShield;
        public static dynamic smtp;
        public static int timeout = 10;

        public static void Init()
        {
            Log.Information("Create Log");

            pathDualShield = getDaulShieldBinFolder("MySQL(DUAL)");
            Log.Information("DualShield folder is {Folder}", pathDualShield);

            getMySQLCredential(pathDualShield + "tomcat\\conf\\server.xml");
            Log.Information("MySQL username is {Username}", username);

            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
            loadSettings("settings.json");
            Log.Information("Job is executed per {Period} seconds", period);

        }
        public static Tuple<string, string> getMySQLCredential(string serverXML)
        {
            XmlDocument xmlDoc = new XmlDocument(); // Create an XML document object
            xmlDoc.Load(serverXML); // Load the XML document from the specified file

            XmlNodeList xnList = xmlDoc.SelectNodes("//GlobalNamingResources/Resource[@driverClassName='com.mysql.jdbc.Driver']");
            username = (string) xnList[0].Attributes["username"].Value;
            password = (string)xnList[0].Attributes["password"].Value;
            return Tuple.Create(username, password);
        }


        public static void loadSettings(string jsonFile)
        {
            //load settings from a json file
            try
            {
                dynamic settings = JObject.Parse(File.ReadAllText(jsonFile));    //must dynamic
                period = settings.period;
                skipMax = settings.error_1032.skips;
                tableScopes = settings.error_1032.tables;
                smtp = settings.smtp;
                timeout = settings.timeout;
            }
            catch (Exception ex)
            {
                Log.Error("Exception caught in loadSettings(): {0}", ex.ToString());
            }
        }

        public static string getDaulShieldBinFolder(string serviceName)
        {
            string pathMySQL = "";   //MySQL(DUAL)

            try
            {
                RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\services\\" + serviceName);
                pathMySQL = (string)registryKey.GetValue("ImagePath");
                //            pathMySQL = "\"C:\\Program Files\\Deepnet DualShield\\mysql\\bin\\mysqld\"";
                int index = pathMySQL.IndexOf("\\mysql");

                pathMySQL = pathMySQL.Substring(1, index);
            }
            catch (Exception ex)
            {
                Log.Error("Exception caught in getDaulShieldBinFolder(): {0}", ex.ToString());
            }




            return pathMySQL;
        }

        public static string getSlaveStatus()
        {
            string repOut = "";
            repOut = runSQLCommand("-u " + username + " -p" + password + " -Bse \"show slave status\\G\"");
            return repOut;
        }

        public static void fix_error_1205()
        {
            //run mysql command

            runSQLCommand("-u " + username + " -p" + password + " -Bse \"stop slave; start slave;\"");
        }

        public static void fix_error_1032()
        {
            //run mysql command

            runSQLCommand("-u " + username + " -p" + password + " -Bse \"stop slave; SET GLOBAL SQL_SLAVE_SKIP_COUNTER = 1; START SLAVE;\"");
        }

        private static string runSQLCommand(string strCommandParameters)
        {
            string strCommand = pathDualShield + "mysql\\bin\\mysql.exe";
//            Log.Information("SQL command {Command} with parameters {Parameters}", strCommand, strCommandParameters);

            string strOutput = "";
            //Create process
            System.Diagnostics.Process pProcess = new System.Diagnostics.Process();

            //strCommand is path and file name of command to run
            pProcess.StartInfo.FileName = strCommand;

            //strCommandParameters are parameters to pass to program
            pProcess.StartInfo.Arguments = strCommandParameters;

            pProcess.StartInfo.UseShellExecute = false;

            //Set output of program to be written to process output stream
            pProcess.StartInfo.RedirectStandardOutput = true;

            //Optional
            pProcess.StartInfo.WorkingDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            //hide window
            pProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            pProcess.StartInfo.CreateNoWindow = true;

            //Start the process
            pProcess.Start();

            //Get program output
            strOutput = pProcess.StandardOutput.ReadToEnd();

            //Wait for process to finish
            pProcess.WaitForExit(timeout*1000);    //wait for 10 seconds
            return strOutput;
        }

        private static bool isIOError(string repOut)
        {
            if (repOut.Contains("Slave_IO_Running: No"))
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        public static int findErrorCode(string repOut)
        {
            if (repOut.Contains("Slave_SQL_Running: No"))
            {
                int index = repOut.IndexOf("Last_Errno:");
                if (index < 0)
                    return 0;

                string errCode = repOut.Substring(index + 12, 4);
                int nCode = Int32.Parse(errCode);
                return nCode;
            }
            else
            {
                return 0;
            }

        }

        private static bool isInScope(string repOut)
        {
            string tableName;   //dualshield.log; dualshield.log_field
            for (int i = 0; i < FullJob.tableScopes.Count; i++)
            {
                tableName = (string)FullJob.tableScopes[i];
                if (repOut.Contains("on table " + tableName))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Bob()
        {
            string repOut;
            //check replication status

            repOut = FullJob.getSlaveStatus();
            if (repOut.Length < 10)
            {
                Log.Information("getSlaveStatus probably timeout or no replication configured");
                return;
            }

            if(isIOError(repOut))
            {
                Log.Error("IO error, unfortunately it can't be auto-fixed");
                FullJob.notifyDBA(false);
                return; //we can not fix it
            }

            int nError = findErrorCode(repOut);
            if (nError == 0)
            {
                Log.Information("Replication is healthy");
                return;
            }

            bool bAutoFixed = false;

            int nTry = 0;

            if (nError == 1205)
            {
                FullJob.fix_error_1205();
                nTry = 1;
                bAutoFixed = true;
            }
            else if (nError == 1032)
            {
                if (isInScope(repOut))
                {
                    int i = 0;
                    do
                    {
                        nTry++;
                        FullJob.fix_error_1032();
                        string repOut2 = FullJob.getSlaveStatus();
                        if (findErrorCode(repOut2) == 0)
                            bAutoFixed = true;
                        i++;
                    }
                    while ((i < FullJob.skipMax) && (bAutoFixed == false));

                }
            }

            Log.Information("The actual slave status: {STATUE}", repOut);
            //write log, and notify DBA
            if(bAutoFixed)
                Log.Error("Replication error {Error} fixed with {Try} Tries", nError, nTry);
            else
                Log.Error("Tried {Try} times, Counld not fix the replication error {Error} ", nTry, nError);

            FullJob.notifyDBA(bAutoFixed);
        }

        public static void notifyDBA(bool bFixed)
        {
            if (!smtp.enabled.Value)
            {
                Log.Information("SMTP is not enabled in settings.json");
                return;
            }

            string mailServer = smtp.server;
            int mailPort = smtp.port;
            SmtpClient smtpClient = new SmtpClient(mailServer, mailPort);

            if(smtp.auth.Value)
            {
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new System.Net.NetworkCredential(smtp.username.Value, smtp.password.Value);
            }
            else
            {
                smtpClient.UseDefaultCredentials = true;
            }



            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpClient.EnableSsl = smtp.ssl.Value;
            MailMessage mail = new MailMessage();

            //Setting From , To and CC
            mail.From = new MailAddress(smtp.username.Value);

            for (int i = 0; i < smtp.to.Count; i++)
            {
                mail.To.Add(new MailAddress(smtp.to[i].Value));
            }

            if (bFixed)
                mail.Subject = smtp.customize.subject1.Value;
            else
                mail.Subject = smtp.customize.subject2.Value;

            mail.Body = string.Format(smtp.customize.body.Value, System.AppDomain.CurrentDomain.BaseDirectory+"logs");

            try
            {
                smtpClient.Send(mail);
                Log.Information("DBA is notified");
            }
            catch (Exception ex)
            {
                Log.Error("Exception caught in notifyDBA(): {0}",  ex.ToString());
            }


        }
    }
}
