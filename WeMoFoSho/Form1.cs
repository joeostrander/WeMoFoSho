using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;
using System.Net;
using System.IO;
using Microsoft.Win32;
using System.Security.Principal;

namespace WeMoFoSho
{
    public partial class Form1 : Form
    {

        private bool boolStartup = false;
        private bool boolBalloonNotification = false;
        private bool boolLogging = false;

        private const String DATATABLE = "devices";
        private DataSet dsListeners = new DataSet(DATATABLE);
        private String strXmlFile = Application.StartupPath + "\\" + Application.ProductName + ".xml";
        private String strLogFile = Application.StartupPath + "\\" + Application.ProductName + ".log";
        private bool boolListening = false;
        private bool done = false;

        private const String COLUMN_ID = "ID";
        private const String COLUMN_NAME = "Name";
        private const String COLUMN_PORT = "Port";
        private const String COLUMN_ACTION_ON = "Acton ON";
        private const String COLUMN_ACTION_OFF = "Action OFF";
        private const String COLUMN_STATE_URL = "State URL";

        private bool isElevated;
        private int intLastStateRequest = 0;

        List<HttpListener> listOfListeners;


        private struct device
        {
            public String name;
            public int port;
            public String action_on;
            public String action_off;
            public String state_url;
        }

        private Dictionary<int, device> devices;


        public Form1()
        {
            InitializeComponent();
            this.Text = Application.ProductName;
        }

        private void buttonRegisterNew_Click(object sender, EventArgs e)
        {
            RegisterNew x = new RegisterNew();
            x.ShowDialog();
            
           
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Opacity = 0;

            //notifyIcon1.Icon = new Icon(this.Icon, 40, 40);
            notifyIcon1.Text = Application.ProductName;
            
            toolStripStatusLabel1.Text = "";

            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            if (!isElevated)
            {
                //MessageBox.Show("You may need to run this app elevated (as administrator)!", "Run this as Administrator", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            LoadSettings();

            LoadData();




            //Start server if there is data...
            if (dataGridView1.RowCount > 1)
            {
                StartWebServer();
            }


            
        }

        private void createLogFile()
        {
            if (!System.IO.File.Exists(strLogFile))
            {
                try
                {
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter(strLogFile))
                    {
                        file.WriteLine("\"Date\",\"Device\",\"Mode\"");
                        file.Close();
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show("Could not create log file!\n\n" + ex.Message + "\n\n" + strLogFile, "Create Log File", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
        }

        private void LoadData()
        {
            if (!System.IO.File.Exists(strXmlFile))
            {
                MessageBox.Show("Could not find file:\r\n\r\n" + strXmlFile + "\r\n\r\n" + "A new file will be created.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                DataTable dt = dsListeners.Tables.Add(DATATABLE);
                dt.Columns.Add(COLUMN_ID);
                dt.Columns.Add(COLUMN_NAME);
                dt.Columns.Add(COLUMN_PORT);
                dt.Columns.Add(COLUMN_ACTION_ON);
                dt.Columns.Add(COLUMN_ACTION_OFF);
                dt.Columns.Add(COLUMN_STATE_URL);
                                
                dt.Columns[COLUMN_ID].DataType = typeof(Int32);
                dt.Columns[COLUMN_ID].AutoIncrement = true;

                dt.Columns[COLUMN_PORT].DataType = typeof(Int32);

                dsListeners.Tables[DATATABLE].AcceptChanges();

                bindingSource1.DataSource = dsListeners.Tables[DATATABLE];
                dataGridView1.DataSource = bindingSource1;

                saveXML();
            } else
            {
                //dataGridView1.DataSource = null;
                dsListeners = new DataSet();
                dsListeners.ReadXmlSchema(strXmlFile);
                if (dsListeners.Tables.Count == 0) {
                    dsListeners.Tables.Add(DATATABLE);
                    dsListeners.Tables[DATATABLE].Columns.Add(COLUMN_ID);
                    dsListeners.Tables[DATATABLE].Columns.Add(COLUMN_NAME);
                    dsListeners.Tables[DATATABLE].Columns.Add(COLUMN_PORT);
                    dsListeners.Tables[DATATABLE].Columns.Add(COLUMN_ACTION_ON);
                    dsListeners.Tables[DATATABLE].Columns.Add(COLUMN_ACTION_OFF);
                    dsListeners.Tables[DATATABLE].Columns.Add(COLUMN_STATE_URL);

                }

                dsListeners.Tables[DATATABLE].Columns[COLUMN_ID].DataType = typeof(Int32);
                dsListeners.Tables[DATATABLE].Columns[COLUMN_ID].AutoIncrement = true;
                dsListeners.Tables[DATATABLE].Columns[COLUMN_ID].ReadOnly = true;

                dsListeners.ReadXml(strXmlFile);

                dsListeners.Tables[DATATABLE].AcceptChanges();
                bindingSource1.DataSource = dsListeners.Tables[DATATABLE];
                dataGridView1.DataSource = bindingSource1;

            }
        }

        private void saveXML()
        {
            DataTable dt = new DataTable();
            bindingSource1.EndEdit();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            XmlWriter writer = XmlWriter.Create(strXmlFile, settings);
            dsListeners.WriteXml(writer);
            writer.Close();
            // boolChangesMade = false;
            
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 ab = new AboutBox1();
            ab.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveXML();
            MessageBox.Show("Data saved.", "Save Data",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadData();
        }

        private void buttonStartServer_Click(object sender, EventArgs e)
        {
            if (buttonStartServer.Text.Contains("Start"))
            {
                StartWebServer();
            }
            else
            {
                //Stop listening
                StopWebServer();
            }
        }

        private void StopWebServer()
        {
            boolListening = false;

        }

        private void StartWebServer()
        {
           

            int count_running = 0;
            //Start listening
            boolListening = true;
            timer1.Start();
            dataGridView1.Enabled = false;
            buttonRegisterDevice.Enabled = false;
            buttonStartServer.Text = "&Stop Server";
            toolStripStatusLabel1.Text = "Listening...";
            done = false;

            devices = new Dictionary<int, device>();
            listOfListeners = new List<HttpListener>();

            foreach ( DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["ID"].Value != null) {

                    String strName = row.Cells[COLUMN_NAME].Value.ToString();
                    Int32 intPort;
                    Int32.TryParse(row.Cells[COLUMN_PORT].Value.ToString(),out intPort);
                    String strActionOn = row.Cells[COLUMN_ACTION_ON].Value.ToString();
                    String strActionOff = row.Cells[COLUMN_ACTION_OFF].Value.ToString();
                    String strStateURL = row.Cells[COLUMN_STATE_URL].Value.ToString();

                    if (intPort>0 && !String.IsNullOrEmpty(strName) && !String.IsNullOrEmpty(strActionOff) && !String.IsNullOrEmpty(strActionOn))
                    {
                        device dev = new device();
                        dev.name = strName;
                        dev.port = intPort;
                        dev.action_on = strActionOn;
                        dev.action_off = strActionOff;
                        dev.state_url = strStateURL;

                        if (!devices.ContainsKey(intPort))
                        {
                            devices.Add(intPort, dev);
                            //start a listener on the port
                            Debug.WriteLine(devices[intPort].action_off);
                            WebServer(intPort);
                            count_running++;
                        }                         


                    }


                }
            }

            if (count_running==0)
            {
                StopWebServer();
            }
        }

        private void WebServer(int port)
        {
            
            try
            {
                HttpListener httpListener = new HttpListener();
                httpListener.Prefixes.Add("http://+:" + port.ToString() + "/");  //must run elevated to do this... otherwise use localhost
                httpListener.Start();

                Debug.WriteLine(devices[port].name + " listening on port " + port.ToString() + "...");

                listOfListeners.Add(httpListener);

                receive(ref httpListener);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to start httplistener on port "+port.ToString()+"\n\n"+ex.Message, "Failed!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                if (!isElevated)
                {

                    if (MessageBox.Show("Click OK to run:\n\nnetsh http add urlacl url=http://+:"+port.ToString()+"/ user=Everyone", "Try netsh...", MessageBoxButtons.OKCancel, MessageBoxIcon.Information)==DialogResult.OK)
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo("c:\\windows\\system32\\netsh.exe");
                        startInfo.Arguments = "http add urlacl url=http://+:" + port.ToString() + "/ user=Everyone";
                        startInfo.Verb = "runas";
                        System.Diagnostics.Process.Start(startInfo);
                        System.Threading.Thread.Sleep(5000);
                        WebServer(port);
                    }


                }
            }


        }


        private void receive(ref HttpListener listener)
        {
            IAsyncResult result = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
            
        }


        private void ListenerCallback(IAsyncResult result)
        {

            HttpListener listener = result.AsyncState as HttpListener;

            if (listener.IsListening)
            {
                HttpListenerContext context = null;
                try
                {
                    context = listener.EndGetContext(result);

                }
                catch (HttpListenerException hle)
                {
                    Console.WriteLine(hle.Message);
                    return;
                } 



                HttpListenerRequest request = context.Request;
                
                string responseString = "";
                device dev = devices[request.Url.Port];

                var body = new StreamReader(request.InputStream).ReadToEnd();

                Console.WriteLine("BODY\r\n{0}", body);
                Console.WriteLine("RAW URL:  {0}", request.RawUrl);
                switch (request.RawUrl)
                {
                    case "/":

                        
                        Console.WriteLine(request.RawUrl);
                        Console.WriteLine("unknown request on port:  " + request.Url.Port.ToString());
                        
                        break;
                    case "/upnp/control/basicevent1":
                        Debug.WriteLine("***ECHO COMMAND on port:  " + request.Url.Port.ToString());

                        Console.WriteLine("BODY {0}", body);
                        if (body.Contains("SetBinaryState"))
                        {
                            if (body.Contains("<BinaryState>1</BinaryState>"))
                            {
                                intLastStateRequest = 1;
                                Debug.Write(dev.name + ":  ON...");
                                if (boolBalloonNotification)
                                    notifyIcon1.ShowBalloonTip(3000, dev.name, "Turning " + dev.name + " ON", ToolTipIcon.Info);

                                logData(DateTime.Now.ToString(), dev.name, "ON");

                                Task.Run(() => {
                                    PerformAction(dev.action_on);
                                });

                            }
                            else if (body.Contains("<BinaryState>0</BinaryState>")) 
                            {
                                intLastStateRequest = 0;
                                Debug.Write(dev.name + ":  OFF...");
                                if (boolBalloonNotification)
                                    notifyIcon1.ShowBalloonTip(3000, dev.name, "Turning " + dev.name + " OFF", ToolTipIcon.Info);

                                logData(DateTime.Now.ToString(), dev.name, "OFF");

                                Task.Run(() =>
                                {
                                    PerformAction(dev.action_off);
                                });
                            }
                        }

                        if (body.Contains("GetBinaryState"))
                        {
                            Console.WriteLine("Request for binary state of device:  " + dev.name);

                            string deviceState = "";
                            
                            int intDeviceState = intLastStateRequest;  //for devices that toggle, just send whatever the last requested state
                            
                            // get the URL from the table... query for state.... reply
                            if (!string.IsNullOrEmpty(dev.state_url))
                            {
                                deviceState = PerformAction(dev.state_url); //main thread?
                                intDeviceState = ((deviceState.ToLower().Contains("on") || deviceState.ToLower().Contains("open")) ? 1 : 0);
                            }
                            responseString = $@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/""><s:Body>
<u:GetBinaryStateResponse xmlns:u=""urn:Belkin:service:basicevent:1"">
<BinaryState>{intDeviceState.ToString()}</BinaryState>
</u:GetBinaryStateResponse>
</s:Body> </s:Envelope>
";

                        }

                        break;
                    default:
                        break;
                } 

                
                HttpListenerResponse response = context.Response;
                Console.WriteLine("headers:  {0}",request.Headers.ToString());
                if (responseString.Contains("BinaryState"))
                {
                    Console.WriteLine("Sending as text/xml");
                    response.ContentType = "text/xml";
                    response.ContentEncoding = Encoding.UTF8;
                    //response.Headers.Remove("Date");
                    
                    //response.Headers.Add("Server", "");
                    //response.Headers.Add("Date", "");
                    //response.KeepAlive = false;
                    // SOAPACTION: "urn:Belkin:service:basicevent:1#GetBinaryState"
                }
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                //System.IO.Stream output = response.OutputStream;

                
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close(buffer, true);

                //response.OutputStream.Write(buffer, 0, buffer.Length);
                //response.OutputStream.Flush();
                //response.OutputStream.Close();
                

                Console.WriteLine("Sending response:\r\n{0}", responseString);
                Console.WriteLine(new string('*', 30));

                if (!done)
                {
                    receive(ref listener);
                }
                
            }
            else
            {
                Console.WriteLine("listener isn't listening??");
            }
            
        }

        private void logData(string myDate, string myDevice, string myMode)
        {
            if (!boolLogging)
                return;

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(strLogFile, true))  //true = append
            {
                file.Write("\"");
                file.Write(myDate);
                file.Write("\",\"");
                file.Write(myDevice);
                file.Write("\",\"");
                file.Write(myMode);
                file.WriteLine("\"");
                file.Close();
            }

        }

        private string PerformAction(String action)
        {
            if (action.StartsWith("http"))
            {
                WebRequest req = WebRequest.Create(action);
                req.Timeout = 10000;
                try
                {
                    WebResponse resp = req.GetResponse();
                    Debug.WriteLine(((HttpWebResponse)resp).StatusDescription);
                    StreamReader reader = new StreamReader(resp.GetResponseStream());
                    // Read the content.
                    string responseFromServer = reader.ReadToEnd();
                    Debug.WriteLine(responseFromServer);
                    return responseFromServer;
                }
                catch {
                    //do nothing
                }
                
            }
            else  //run it like a command
            {
                Debug.WriteLine("/s /c \"start " + action + "\"");
                ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/s /c \"start \"\" " + action + "\"");
                procStartInfo.UseShellExecute = false;
                Process p = new Process();
                p.StartInfo = procStartInfo;
                p.Start();
                
            }

            return "";
            
        }

        private void KillListeners()
        {
            Console.WriteLine("KillsListeners()");
            try
            {
                foreach (HttpListener ls in listOfListeners)
                {
                    ls.Stop();
                    ls.Close();
                    while (ls.IsListening)
                    {
                        Console.WriteLine("killing listener...");
                        System.Threading.Thread.Sleep(500);
                    }
                    Console.WriteLine("Killed 1");
                }
                listOfListeners.Clear();

            }
            catch (Exception ex)
            {
                Console.WriteLine("KillListeners:  {0}", ex.Message);
            }


        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!boolListening)
            {
                //stop listening
                dataGridView1.Enabled = true;
                buttonRegisterDevice.Enabled = true;
                buttonStartServer.Text = "&Start Server";
                toolStripStatusLabel1.Text = "";

                //kill all listeners
                done = true;

                timer1.Stop();

                KillListeners();
            }
        }

        private void Form1_Move(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                notifyIcon1.ShowBalloonTip(600, Application.ProductName, Application.ProductName + " running.", ToolTipIcon.Info);
                this.Hide();
            }
            else
            {
                this.Show();
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            this.Hide();
            this.Opacity = 100;
        }

        private void exitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }





         private void LoadSettings()
         {
        

        RegistryKey regKey;
        regKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);

        RegistryKey appRegKey;
        appRegKey = Registry.CurrentUser.OpenSubKey("Software\\" + Application.ProductName, true);

        if (appRegKey == null) {
            //Create the key
            Registry.CurrentUser.CreateSubKey("Software\\" + Application.ProductName);
            appRegKey = Registry.CurrentUser.OpenSubKey("Software\\" + Application.ProductName, true);
            if (appRegKey==null) 
            {
                regKey.Close();
                return;
            }

            //Ask user if they want it to launch auto...
            if (MessageBox.Show("Launch " + Application.ProductName + " at Startup?",Application.ProductName,MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes) 
            {
                boolStartup = true;
            } else {
                boolStartup = false;
            }

        } 
        else
        {
                //Get Current Value and set it in the interface
                boolStartup = Convert.ToBoolean(appRegKey.GetValue("RunAtStartup", false));
                boolBalloonNotification = Convert.ToBoolean(appRegKey.GetValue("BalloonNotification", false));
                boolLogging = Convert.ToBoolean(appRegKey.GetValue("LoggingEnabled", false));
        }
            
        regKey.Close();
        appRegKey.Close();

        SaveRegistrySettings();

        }

        private void SaveRegistrySettings()
        {
            RegistryKey regKey;
            regKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            RegistryKey appRegKey;
            appRegKey = Registry.CurrentUser.OpenSubKey("Software\\" + Application.ProductName, true);

            appRegKey.SetValue("BalloonNotification",boolBalloonNotification, RegistryValueKind.DWord);
            appRegKey.SetValue("LoggingEnabled", boolLogging, RegistryValueKind.DWord);
            appRegKey.SetValue("RunAtStartup", boolStartup, RegistryValueKind.DWord);

            //If boolStartup==true, set to run at startup
            if (boolStartup )
            {
                regKey.SetValue(Application.ProductName, Application.ExecutablePath);
            }
            else
            {
                if (regKey.GetValue(Application.ProductName)!=null) {
                    regKey.DeleteValue(Application.ProductName);
                }
            }



            runAtStartupToolStripMenuItem.Checked = boolStartup;
            balloonNotificationToolStripMenuItem.Checked = boolBalloonNotification;
            loggingToolStripMenuItem.Checked = boolLogging;

            regKey.Close();
            appRegKey.Close();

            //Create log file if not found
            if (boolLogging)
            {
                createLogFile();
            }
        }



        private void runAtStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunAtStartupSet();
        }

        private void RunAtStartupSet()
        {
            boolStartup = runAtStartupToolStripMenuItem.Checked;
            SaveRegistrySettings();
        }

        private void notifyIcon1_BalloonTipClicked(object sender, EventArgs e)
        {
            ShowMe();
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            ShowMe();
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowMe();
        }

        private void ShowMe()
        {
            this.Show();
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
        }

        private void balloonNotificationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BalloonNotificationSet();
        }

        private void BalloonNotificationSet()
        {
            boolBalloonNotification = balloonNotificationToolStripMenuItem.Checked;
            SaveRegistrySettings();
        }

        private void loggingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loggingSet();
        }

        private void loggingSet()
        {
            boolLogging = loggingToolStripMenuItem.Checked;
            SaveRegistrySettings();
        }
    }




}
