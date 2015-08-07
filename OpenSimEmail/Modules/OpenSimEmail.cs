/*
 * Copyright (c) Quill Littlefeather, http://qlittlefeather.com
 * 
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

/*
 * Credits
 * Fly-Man - https://github.com/OS-Development/OpenSim.Email
 * Code updated and heavlily moddified from Fly-Man OpenEmail Module
 * */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Text.RegularExpressions;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using DotNetOpenMail;
using DotNetOpenMail.SmtpAuth;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Communications;

[assembly: Addin("OpenEmailModule", "0.1")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
namespace OpenSimEmail.Modules.OpenEmail
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EmailModule")]
    public class OpenEmailModule : IEmailModule, ISharedRegionModule
    {
        //
        // Log module
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private IConfigSource m_Config;
        private string m_EmailServer = "";
        private string m_HostName = string.Empty;
        private string SMTP_SERVER_HOSTNAME = string.Empty;
        private int SMTP_SERVER_PORT = 25;
        private string SMTP_SERVER_LOGIN = string.Empty;
        private string SMTP_SERVER_PASSWORD = string.Empty;
        private string m_InterObjectHostname = string.Empty;
        private string value = string.Empty;
        private bool m_Enabled = true;

        // Scenes by Region Handle
        private Dictionary<ulong, Scene> m_Scenes =
            new Dictionary<ulong, Scene>();

        // Queue settings
        private int m_MaxQueueSize = 50; // maximum size of an object mail queue
        private Dictionary<UUID, List<Email>> m_MailQueues = new Dictionary<UUID, List<Email>>();
        private Dictionary<UUID, DateTime> m_LastGetEmailCall = new Dictionary<UUID, DateTime>();
        private TimeSpan m_QueueTimeout = new TimeSpan(2, 0, 0); // 2 hours without llGetNextEmail drops the queue

        public void InsertEmail(UUID to, Email email)
        {
            // It's tempting to create the queue here.  Don't; objects which have
            // not yet called GetNextEmail should have no queue, and emails to them
            // should be silently dropped.

            lock (m_MailQueues)
            {
                if (m_MailQueues.ContainsKey(to))
                {
                    if (m_MailQueues[to].Count >= m_MaxQueueSize)
                    {
                        // fail silently
                        return;
                    }

                    lock (m_MailQueues[to])
                    {
                        m_MailQueues[to].Add(email);
                    }
                }
            }
        }

        public void Initialise(IConfigSource m_Config)
        {
            IConfig startupConfig = m_Config.Configs["Startup"];

            m_Enabled = (startupConfig.GetString("emailmodule", "OpenEmailModule") == "OpenEmailModule");

            if (!m_Enabled)
            {
                m_log.Error("[OpenSimEmail] Module is not loaded in OpenSim.ini");
                return;
            }

            IConfig emailConfig = m_Config.Configs["Email"];
            //IConfig SMTPConfig = m_Config.Configs["SMTP"];//Not working
            //Load SMTP MODULE config
            try
            {
                if (emailConfig == null)
                {
                    m_log.Info("[OpenSimEmail] Not configured, disabling");
                    m_Enabled = false;
                    return;
                }

                //m_HostName = emailConfig.GetString("host_domain_header_from", "");

                m_EmailServer = emailConfig.GetString("EmailURL", "");
                m_HostName = emailConfig.GetString("host_domain_header_from", m_HostName);
                m_InterObjectHostname = emailConfig.GetString("SMTP_internal_object_host", m_InterObjectHostname);
                SMTP_SERVER_HOSTNAME = emailConfig.GetString("SMTP_SERVER_HOSTNAME", SMTP_SERVER_HOSTNAME);
                SMTP_SERVER_PORT = emailConfig.GetInt("SMTP_SERVER_PORT", SMTP_SERVER_PORT);
                SMTP_SERVER_LOGIN = emailConfig.GetString("SMTP_SERVER_LOGIN", SMTP_SERVER_LOGIN);
                SMTP_SERVER_PASSWORD = emailConfig.GetString("SMTP_SERVER_PASSWORD", SMTP_SERVER_PASSWORD);
                //m_MaxEmailSize = SMTPConfig.GetInt("email_max_size", m_MaxEmailSize);
                if (m_EmailServer == "")
                {
                    m_log.Error("[OpenSimEmail] No email dispatcher, disabling email");
                    m_Enabled = false;
                    return;
                }
                else
                {
                    m_log.Info("[OpenSimEmail] OpenSimEmail module is activated");
                    m_Enabled = true;
                }

            }
            catch (Exception e)
            {
                m_log.Error("[EMAIL] OpenSimEmail module not configured: " + e.Message);
                m_Enabled = false;
                return;
            }

            // It's a go!

        }
        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                lock (m_Scenes)
                {
                    // Claim the interface slot
                    scene.RegisterModuleInterface<IEmailModule>(this);

                    // Add to scene list
                    if (m_Scenes.ContainsKey(scene.RegionInfo.RegionHandle))
                    {
                        m_Scenes[scene.RegionInfo.RegionHandle] = scene;
                    }
                    else
                    {
                        m_Scenes.Add(scene.RegionInfo.RegionHandle, scene);
                    }
                }

                m_log.Info("[OpenSimEmail] Activated OpenSimEmail module");
            }
        }
        public void PostInitialise()
        {
            if (!m_Enabled)
                return;
        }

        public void Close()
        {
        }
        public void RegionLoaded(Scene scene)
        {
        }
        public void RemoveRegion(Scene scene)
        {
        }


        public Type ReplaceableInterface
        {
            //get { return typeof(IMoneyModule); }
            get { return null; }
        }

        public string Name
        {
            get { return "OpenEmailModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        /// New Client Event Handler
        private void OnNewClient(IClientAPI client)
        {
            return;
        }

        // Functions needed inside

        private void DelayInSeconds(int delay)
        {
            delay = (int)((float)delay * 1000);
            if (delay == 0)
                return;
            System.Threading.Thread.Sleep(delay);
        }

        static DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return origin.AddSeconds(timestamp);
        }


        static double ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date - origin;
            return Math.Floor(diff.TotalSeconds);
        }

        private SceneObjectPart findPrim(UUID objectID, out string ObjectRegionName)
        {
            lock (m_Scenes)
            {
                foreach (Scene s in m_Scenes.Values)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(objectID);
                    if (part != null)
                    {
                        ObjectRegionName = s.RegionInfo.RegionName;
                        uint localX = (s.RegionInfo.RegionLocX * 256);
                        uint localY = (s.RegionInfo.RegionLocY * 256);
                        ObjectRegionName = ObjectRegionName + " (" + localX + ", " + localY + ")";
                        return part;
                    }
                }
            }
            ObjectRegionName = string.Empty;
            return null;
        }
        public string GetEmailHost()
        {

            string hostn = m_InterObjectHostname;
            return hostn;


        }
        private void resolveNamePositionRegionName(UUID objectID, out string ObjectName, out string ObjectAbsolutePosition, out string ObjectRegionName)
        {
            string m_ObjectRegionName;
            int objectLocX;
            int objectLocY;
            int objectLocZ;
            SceneObjectPart part = findPrim(objectID, out m_ObjectRegionName);
            if (part != null)
            {
                objectLocX = (int)part.AbsolutePosition.X;
                objectLocY = (int)part.AbsolutePosition.Y;
                objectLocZ = (int)part.AbsolutePosition.Z;
                ObjectAbsolutePosition = "(" + objectLocX + ", " + objectLocY + ", " + objectLocZ + ")";
                ObjectName = part.Name;
                ObjectRegionName = m_ObjectRegionName;
                return;
            }
            objectLocX = (int)part.AbsolutePosition.X;
            objectLocY = (int)part.AbsolutePosition.Y;
            objectLocZ = (int)part.AbsolutePosition.Z;
            ObjectAbsolutePosition = "(" + objectLocX + ", " + objectLocY + ", " + objectLocZ + ")";
            ObjectName = part.Name;
            ObjectRegionName = m_ObjectRegionName;
            return;
        }

        //
        // Make external XMLRPC request
        //
        private Hashtable GenericXMLRPCRequest(Hashtable ReqParams, string method)
        {
            ArrayList SendParams = new ArrayList();
            SendParams.Add(ReqParams);

            // Send Request
            XmlRpcResponse Resp;
            try
            {
                XmlRpcRequest Req = new XmlRpcRequest(method, SendParams);
                Resp = Req.Send(m_EmailServer, 30000);
            }
            catch (WebException ex)
            {
                m_log.ErrorFormat("[OpenSimEmail]: Unable to connect to Email " +
                        "Server {0}.  Exception {1}", m_EmailServer, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to send email at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            catch (SocketException ex)
            {
                m_log.ErrorFormat(
                        "[OpenSimEmail]: Unable to connect to Email Server {0}. " +
                        "Exception {1}", m_EmailServer, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to send email at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            catch (XmlException ex)
            {
                m_log.ErrorFormat(
                        "[OpenSimEmail]: Unable to connect to Email Server {0}. " +
                        "Exception {1}", m_EmailServer, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to send email at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            if (Resp.IsFault)
            {
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to send email at this time. ";
                ErrorHash["errorURI"] = "";
                return ErrorHash;
            }
            Hashtable RespData = (Hashtable)Resp.Value;

            return RespData;
        }


        public void SendEmail(UUID objectID, string address, string subject, string body)
        {
            //Check if address is empty
            if (address == string.Empty)
                return;
            string[] host = address.Split('@');
            string hostcheck = host[1];
            WebClient client = new WebClient();
            value = client.DownloadString("http://osxchange.org/router.php?grid=" + hostcheck);
            // string routeraddress = address.en
            //FIXED:Check the email is correct form in REGEX
            string EMailpatternStrict = @"^(([^<>()[\]\\.,;:\s@\""]+"
                + @"(\.[^<>()[\]\\.,;:\s@\""]+)*)|(\"".+\""))@"
                + @"((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}"
                + @"\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+"
                + @"[a-zA-Z]{2,}))$";
            Regex EMailreStrict = new Regex(EMailpatternStrict);
            bool isEMailStrictMatch = EMailreStrict.IsMatch(address);
            if (!isEMailStrictMatch)
            {
                m_log.Error("[OpenSimEmail] REGEX Problem in EMail Address: " + address);
                return;
            }
            //FIXME:Check if subject + body = 4096 Byte
            if ((subject.Length + body.Length) > 1024)
            {
                m_log.Error("[OpenSimEmail] subject + body > 1024 Byte");
                return;
            }

            string LastObjectName = string.Empty;
            string LastObjectPosition = string.Empty;
            string LastObjectRegionName = string.Empty;

            resolveNamePositionRegionName(objectID, out LastObjectName, out LastObjectPosition, out LastObjectRegionName);

            if (!address.EndsWith(m_InterObjectHostname))
            {
                // regular email, send it out

                //Creation EmailMessage
                EmailMessage emailMessage = new EmailMessage();
                //From
                emailMessage.FromAddress = new EmailAddress(objectID.ToString() + "@" + m_HostName);
                //To - Only One
                emailMessage.AddToAddress(new EmailAddress(address));
                //Subject
                emailMessage.Subject = subject;
                //TEXT Body
                resolveNamePositionRegionName(objectID, out LastObjectName, out LastObjectPosition, out LastObjectRegionName);
                emailMessage.BodyText = "Object-Name: " + LastObjectName +
                          "\nRegion: " + LastObjectRegionName + "\nLocal-Position: " +
                          LastObjectPosition + "\n\n" + body;

                //Config SMTP Server
                //Set SMTP SERVER config
                SmtpServer smtpServer = new SmtpServer(SMTP_SERVER_HOSTNAME, SMTP_SERVER_PORT);
                // Add authentication only when requested
                //
                if (SMTP_SERVER_LOGIN != String.Empty && SMTP_SERVER_PASSWORD != String.Empty)
                {
                    //Authentication
                    smtpServer.SmtpAuthToken = new SmtpAuthToken(SMTP_SERVER_LOGIN, SMTP_SERVER_PASSWORD);
                }
                //Send Email Message
                emailMessage.Send(smtpServer);

                //Log
                m_log.Info("[EMAIL] EMail sent to: " + address + " from object: " + objectID.ToString() + "@" + m_HostName);


            }
            else if (address.EndsWith(m_InterObjectHostname))
            {
                Hashtable ReqHash = new Hashtable();
                ReqHash["fromaddress"] = objectID.ToString() + "@" + m_HostName;
                ReqHash["toaddress"] = address.ToString();
                ReqHash["timestamp"] = ConvertToUnixTimestamp(DateTime.UtcNow);
                ReqHash["subject"] = subject.ToString();
                ReqHash["objectname"] = LastObjectName;
                ReqHash["position"] = LastObjectPosition;
                ReqHash["region"] = LastObjectRegionName;
                ReqHash["messagebody"] = body.ToString();
                m_log.Error("Address is internal" + address);
                Hashtable result = GenericXMLRPCRequest(ReqHash,
                        "send_email");

                if (!Convert.ToBoolean(result["success"]))
                {
                    return;
                }
                DelayInSeconds(20);
            }
        }
        public Email GetNextEmail(UUID objectID, string sender, string subject)
        {

            string m_Object;
            string num_emails = "";

            m_Object = objectID + "@" + m_HostName;

            Hashtable ReqHash = new Hashtable();
            ReqHash["objectid"] = m_Object;

            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    "check_email");

            if (!Convert.ToBoolean(result["success"]))
            {
                return null;
            }

            ArrayList dataArray = (ArrayList)result["data"];

            foreach (Object o in dataArray)
            {
                Hashtable d = (Hashtable)o;

                num_emails = d["num_emails"].ToString();
            }

            //m_log.Error("[OpenSimEmail] " + num_emails);

            DelayInSeconds(2);

            if (num_emails != "0")
            {
                // Get the info from the database and Queue it up
                Hashtable GetHash = new Hashtable();
                GetHash["objectid"] = m_Object;
                GetHash["number"] = num_emails;

                //m_log.Debug("[OpenSimEmail] I have " + num_emails + " waiting on dataserver");

                Hashtable results = GenericXMLRPCRequest(GetHash,
                        "retrieve_email");

                if (!Convert.ToBoolean(results["success"]))
                {
                    return null;
                }

                ArrayList mailArray = (ArrayList)results["data"];

                foreach (Object ob in mailArray)
                {
                    Hashtable d = (Hashtable)ob;

                    Email email = new Email();


                    email.time = d["timestamp"].ToString();
                    email.subject = d["subject"].ToString();
                    email.sender = d["sender"].ToString();
                    email.message = "Object-Name: " + d["objectname"].ToString() +
                                  "\nRegion: " + d["region"].ToString() + "\nLocal-Position: " +
                                  d["objectpos"].ToString() + "\n\n" + d["message"].ToString();

                    string guid = m_Object.Substring(0, m_Object.IndexOf("@"));
                    UUID toID = new UUID(guid);

                    InsertEmail(toID, email);
                }
            }

            // And let's start with readin the Queue here
            List<Email> queue = null;

            lock (m_LastGetEmailCall)
            {
                if (m_LastGetEmailCall.ContainsKey(objectID))
                {
                    m_LastGetEmailCall.Remove(objectID);
                }

                m_LastGetEmailCall.Add(objectID, DateTime.Now);

                // Hopefully this isn't too time consuming.  If it is, we can always push it into a worker thread.
                DateTime now = DateTime.Now;
                List<UUID> removal = new List<UUID>();
                foreach (UUID uuid in m_LastGetEmailCall.Keys)
                {
                    if ((now - m_LastGetEmailCall[uuid]) > m_QueueTimeout)
                    {
                        removal.Add(uuid);
                    }
                }

                foreach (UUID remove in removal)
                {
                    m_LastGetEmailCall.Remove(remove);
                    lock (m_MailQueues)
                    {
                        m_MailQueues.Remove(remove);
                    }
                }
            }

            lock (m_MailQueues)
            {
                if (m_MailQueues.ContainsKey(objectID))
                {
                    queue = m_MailQueues[objectID];
                }
            }

            if (queue != null)
            {
                lock (queue)
                {
                    if (queue.Count > 0)
                    {
                        int i;

                        for (i = 0; i < queue.Count; i++)
                        {
                            if ((sender == null || sender.Equals("") || sender.Equals(queue[i].sender)) &&
                                (subject == null || subject.Equals("") || subject.Equals(queue[i].subject)))
                            {
                                break;
                            }
                        }

                        if (i != queue.Count)
                        {
                            Email ret = queue[i];
                            queue.Remove(ret);
                            ret.numLeft = queue.Count;
                            return ret;
                        }
                    }
                }
            }
            else
            {
                lock (m_MailQueues)
                {
                    m_MailQueues.Add(objectID, new List<Email>());
                }
            }

            return null;
        }
    }
}
