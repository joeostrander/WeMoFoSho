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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace WeMoFoSho
{
    //public class StateObject
    //{
    //    public Socket workSocket = null;
    //    public const int bufferSize = 1024;
    //    public byte[] buffer = new byte[bufferSize];
    //    public StringBuilder sb = new StringBuilder(); 
    // }

    public partial class RegisterNew : Form
    {

        private const int portMulti = 1900;
        //private UdpClient udpServer;
        private bool done_udp = false;
        private bool done_http = false;
        private String strWeMoIPAddress = "";
        private String strWeMoGuid;  //anything
        private String strWeMoSerialNumber;                //anything
        private String strWeMoName;
        private bool boolRespond = true;
        private int portWeb;
        private IPAddress localIPAddr;
        private bool boolRegisterSuccess;
        private ManualResetEvent mreHttpReceiveDone = new ManualResetEvent(false);
        private ManualResetEvent mreUdpReceiveDone = new ManualResetEvent(false);
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        HttpListener httpListener;


        public RegisterNew()
        {
            InitializeComponent();
        }

        //private bool IsNumeric(this String s) => s.All(Char.IsDigit);
        private bool IsNumeric(String s) {
            return s.All(Char.IsDigit);
        }
     

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if (textBoxFriendlyName.Text=="")
            {
                MessageBox.Show("Enter a friendly name!","Friendly Name", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (textBoxPort.Text == "" || (!IsNumeric(textBoxPort.Text)) )
            {
                MessageBox.Show("Enter a valid port number!", "Port Number", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Int32.TryParse(textBoxPort.Text, out portWeb);

            strWeMoGuid = Guid.NewGuid().ToString();
            strWeMoSerialNumber = strWeMoGuid.Replace("-", "");
            localIPAddr = GetLocalIP();

            /*
            1) disable controls 
            2) Tell user to say "Alexa, discover devices"
            3) Listen on for multicast upnp broadcast, port 1900
            4) timeout?  cancel?  success? fail?
            */
            enableControls(false);
            boolRegisterSuccess = false;

            strWeMoName = textBoxFriendlyName.Text;

            toolStripStatusLabel1.Text = "Say:  Alexa, discover devices";

            done_http = false;
            done_udp = false;
            enableControls(false);
            
            timer1.Start();

            StartWebServer(portWeb);
            StartUdpServer();
                       

            //MessageBox.Show("done???");
            //           labelStatus.Text = "";
            //enableControls(true);
        }

        private void enableControls(bool boolEnable)
        {
            textBoxFriendlyName.Enabled = boolEnable;
            textBoxPort.Enabled = boolEnable;
            buttonStart.Enabled = boolEnable;
            buttonExit.Enabled = boolEnable;
            buttonCancel.Enabled = !boolEnable;
            this.CancelButton = boolEnable ? buttonExit : buttonCancel;
        }

        private void RegisterNew_Load(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "";
        }

        private void buttonExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        private String getWeMoString()
        {

            //strWeMoSerialNumber = strWeMoSerialNumber.Substring(strWeMoSerialNumber.Length - 14);

            String strWeMo =
              "HTTP/1.1 200 OK\r\n" +
              "CACHE-CONTROL: max-age=86400\r\n" +
              "DATE: " + getDateString() + "\r\n" +
              "EXT:\r\n" +
              "LOCATION: http://" + strWeMoIPAddress + ":" + portWeb.ToString() + "/setup.xml\r\n" +
              "OPT: \"http://schemas.upnp.org/upnp/1/0/\"); ns=01\r\n" +
              "01-NLS: " + strWeMoGuid + "\r\n" +
              "SERVER: Unspecified, UPnP/1.0, Unspecified\r\n" +
              "X-User-Agent: redsonic\r\n" +
              "ST: ::upnp:rootdevice\r\n" +
              "USN: uuid:Socket-1_0-" + strWeMoSerialNumber + "::upnp:rootdevice\r\n\r\n";

            return strWeMo;
        }

        private String getDateString()
        {
            DateTime dt = DateTime.Now;
            String strDate = dt.ToUniversalTime().ToString("R");  //'R' = GMT Format... Thu, 03 Nov 2016 13:55:46 GMT
            return strDate;
        }

        //CallBack
        //private void recv(IAsyncResult res)
        //{
        //    //StateObject so = (StateObject)res.AsyncState;
        //    //Socket s = so.workSocket;



        //    Console.WriteLine("recv...");
        //    //UdpClient udpSrv = res.AsyncState as UdpClient;

        //    //IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, portMulti);
        //    IPEndPoint groupEP = new IPEndPoint(localIPAddr, portMulti);
        //    //IPEndPoint groupEP = (IPEndPoint)s.LocalEndPoint;


        //    if (done_udp)
        //    {
        //        Console.WriteLine("done_udp");
        //        mreUdpReceiveDone.Set();
        //        return;
        //    }

        //    //byte[] received;
        //    string strReceived = "";
        //    try
        //    {
        //        //received = udpSrv.EndReceive(res, ref groupEP);
        //        //Socket socket1 = res.AsyncState as Socket;
        //        int read = s.EndReceive(res);
        //        if (read > 0)
        //        {
        //            string data = Encoding.ASCII.GetString(so.buffer, 0, read);
        //            so.sb.Append(data);
        //            strReceived = so.sb.ToString();
        //            //if (strReceived.Contains("M-SEARCH") && strReceived.Contains("urn:Belkin:device:"))
        //            if (strReceived.Contains("M-SEARCH") && strReceived.Contains("upnp:rootdevice"))
        //            {
        //                Debug.WriteLine("***ECHO IS SEARCHING!!!***");


        //                //who sent it?  IP?  port?
        //                Debug.WriteLine("Remote Address:\t{0}:{1}", groupEP.Address.ToString(), groupEP.Port.ToString());
        //                Debug.WriteLine(strReceived);
        //                Debug.WriteLine("");


        //                //Respond to search
        //                if (boolRespond)
        //                {
        //                    responseToSearchUdp(groupEP.Address, groupEP.Port);
        //                    done_udp = true;
        //                    //udpSrv.Close();
        //                    s.Close();
        //                    mreUdpReceiveDone.Set();
        //                }
        //            }
        //            else
        //            {
        //                Debug.WriteLine("??? UNKNOWN UDP BROADCAST ???");
        //                Debug.WriteLine(strReceived);
        //            }


        //            if (!done_udp)
        //            {
        //                //udpSrv.BeginReceive(new AsyncCallback(recv), udpSrv);
        //                s.BeginReceive(so.buffer, 0, StateObject.bufferSize, 0, new AsyncCallback(recv), so);
        //            }


        //            //s.BeginReceive(so.buffer, 0, StateObject.bufferSize, 0, new AsyncCallback(recv), so);
        //            //Console.WriteLine("data:  {0}", blah);
        //            return;
        //        }
        //        else
        //        {
        //            if (so.sb.Length > 1)
        //            {
        //                //all data has been read

        //                strReceived = so.sb.ToString();
        //                Console.WriteLine(String.Format("Read {0} byte from socket data = {1} ",  strReceived.Length, strReceived));
        //                //s.Close();
        //            }
        //        }

        //        //received = socket1.EndReceive(res, ref groupEP);
        //        //socket1.ReceiveFrom(res, SocketFlags.None, ref groupEP);
        //        //var stringResponse = Encoding.UTF8.GetString(received).Trim().TrimEnd('\0');
        //        //Console.WriteLine(string.Format("Received:  {0}", stringResponse));
        //    }
        //    catch (ObjectDisposedException ode)
        //    {
        //        Console.WriteLine(ode.Message);
        //        return;
        //    }

        //    //String strReceived = Encoding.UTF8.GetString(received);
        //    //strReceived = so.sb.ToString();


        //}

        private void recv(IAsyncResult res)
        {
         
            Console.WriteLine("recv...");
            UdpClient udpSrv = res.AsyncState as UdpClient;

            IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, portMulti);
            //IPEndPoint groupEP = new IPEndPoint(localIPAddr, portMulti);
            //IPEndPoint groupEP = (IPEndPoint)s.LocalEndPoint;


            if (done_udp)
            {
                Console.WriteLine("done_udp");
                mreUdpReceiveDone.Set();
                return;
            }

            byte[] received;
            try
            {
                received = udpSrv.EndReceive(res, ref groupEP);
                }
            catch (ObjectDisposedException ode)
            {
                Console.WriteLine(ode.Message);
                return;
            }

            String strReceived = Encoding.UTF8.GetString(received);
            

            if (strReceived.Contains("M-SEARCH") && strReceived.Contains("upnp:rootdevice"))
            {
                Console.WriteLine("***ECHO IS SEARCHING!!!***");

                //who sent it?  IP?  port?
                Console.WriteLine("Remote Address:\t{0}:{1}", groupEP.Address.ToString(), groupEP.Port.ToString());
                Console.WriteLine(strReceived);

                //respond to search
                if (boolRespond)
                {
                    responseToSearchUdp(groupEP.Address, groupEP.Port);
                    done_udp = true;
                    udpSrv.Close();
                    mreUdpReceiveDone.Set();
                }
            }
            else
            {
                Console.WriteLine("??? UNKNOWN UDP BROADCAST ???");
                Console.WriteLine(strReceived);
            }

            if (!done_udp)
            {
                udpSrv.BeginReceive(new AsyncCallback(recv), udpSrv);
            }
        }

        private void responseToSearchUdp(IPAddress senderIP, int senderPort)
        {

            Socket sOut = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ep = new IPEndPoint(senderIP, senderPort);
            //s.SendTo(sendbuf, ep);
            sOut.Connect(ep);
            //strWeMoIPAddress = ((IPEndPoint)sOut.LocalEndPoint).Address.ToString();
            strWeMoIPAddress = ((IPEndPoint)sOut.LocalEndPoint).Address.ToString();

            string sendStr = getWeMoString();
            Console.WriteLine("Sending:  {0}", sendStr);
            byte[] sendbuf = Encoding.ASCII.GetBytes(sendStr);
            sOut.Send(sendbuf);
            sOut.Close();
            Console.WriteLine("response sent?");
        }

        private void StartUdpServer() {
            Thread t = new Thread(new ThreadStart(UdpServer));
            t.Start();
        }

        //private void UdpServer()
        //{
        //    //I was playing with sockets here...

        //    mreUdpReceiveDone.Reset();
        //    //UdpClient udpServer = new UdpClient(portMulti);

        //    try
        //    {

        //        IPAddress multicastAddress = IPAddress.Parse("239.255.255.250");
                
        //        Console.WriteLine("IP:  {0}", localIPAddr.ToString());

                
        //            Console.WriteLine("Calling socket.Bind()");
        //            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        //            //Bind to local IP address
        //            IPEndPoint localEP = new IPEndPoint(localIPAddr, 1900);

        //            socket.Bind(localEP);

        //            //This is required to see the multicast messages
        //            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastAddress, localIPAddr));

        //            //Receive broadcasts from any remote endpoint
        //            EndPoint remoteEP = (EndPoint)new IPEndPoint(IPAddress.Any, 0);

        //            //var response = new byte[0x8000];

        //            //while (true)
        //            //{
        //            //socket.BeginReceiveFrom()
        //            StateObject state = new StateObject();
        //            state.workSocket = socket;

        //        //IAsyncResult result = 
        //        //socket.BeginReceiveFrom(state.buffer, 0, StateObject.bufferSize, SocketFlags.None, ref remoteEP, new AsyncCallback(recv), state);
        //        socket.BeginReceive(state.buffer, 0, StateObject.bufferSize, SocketFlags.None, new AsyncCallback(recv), state);
        //        ////socket.ReceiveFrom(response, SocketFlags.None, ref remoteEP);
        //            ////var stringResponse = Encoding.UTF8.GetString(response).Trim().TrimEnd('\0');
        //            ////Console.WriteLine(string.Format("Received:  {0}", stringResponse));
        //            //}

        //            //IAsyncResult result = socket.BeginReceive(new AsyncCallback(recv), socket);
        //            //socket.BeginReceive()

                
                    

                
        //        //ASYNCHRONOUS...
        //        Debug.WriteLine("Waiting for broadcast");
                

        //        mreUdpReceiveDone.WaitOne();
                

        //    }
        //    catch (Exception e)
        //    {
        //        Debug.WriteLine(e.ToString());
        //    }
        //    finally
        //    {
        //        //udpServer.Close();
        //        done_udp = true;
        //    }
        //}

        private IPAddress GetLocalIP()
        {
            IPAddress ip = null;
            string localName = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(localName);
            foreach (IPAddress address in hostEntry.AddressList)
            {
                //UPnP only supports IPv4
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ip = address;
                    break;
                }
            }
            return ip;
        }

        private void UdpServer()
        {
            mreUdpReceiveDone.Reset();
            //UdpClient udpServer = new UdpClient(portMulti);
            UdpClient udpServer = new UdpClient();
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, portMulti));

            try
            {

                IPAddress multicastaddress = IPAddress.Parse("239.255.255.250");
                //IPAddress localIPAddr = GetLocalIP();

                //if (localIPAddr == null)
                //{
                //    Console.WriteLine("Failed to get ip address");
                //    return;
                //}
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, portMulti);
                //IPEndPoint localEndPoint = new IPEndPoint(localIPAddr, 1900);

                udpServer.Client.Bind(localEndPoint);

                udpServer.JoinMulticastGroup(multicastaddress);
                udpServer.MulticastLoopback = true;

                //ASYNCHRONOUS...
                Debug.WriteLine("Waiting for broadcast");
                IAsyncResult result = udpServer.BeginReceive(new AsyncCallback(recv), udpServer);


                mreUdpReceiveDone.WaitOne();


            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
            finally
            {
                udpServer.Close();
                done_udp = true;
            }
        }

        private void StartWebServer(int port) {
            Thread t = new Thread(new ParameterizedThreadStart(WebServer));
            t.Start(port);
            
        }



        
        private void WebServer(object oPort)
        {
            int port = (int)oPort;
            httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://+:" + port.ToString() + "/");  //must run elevated to do this... otherwise use localhost
            httpListener.Start();

            Debug.WriteLine("Listening on port " + port.ToString() + "...");

            //receive(ref httpListener);


            while (!done_http)
            {
                IAsyncResult result = httpListener.BeginGetContext(new AsyncCallback(ListenerCallback), httpListener);
                //result.AsyncWaitHandle.WaitOne();
                WaitHandle[] handles = new WaitHandle[] { mreHttpReceiveDone, result.AsyncWaitHandle };
                WaitHandle.WaitAny(handles);
            }
            if (httpListener.IsListening)
            {
                try
                {
                    httpListener.Stop();
                    httpListener.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            

            Debug.WriteLine("DONE?");

        }

        private void receiveHttp(ref HttpListener listener)
        {
            IAsyncResult result = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
        }

        private void ListenerCallback(IAsyncResult result)
        {
            
            HttpListener listener = result.AsyncState as HttpListener;


            
            if (listener.IsListening)
            {
                if (done_http)
                {
                    mreHttpReceiveDone.Set();
                    return;
                }

                HttpListenerContext context = listener.EndGetContext(result);
                HttpListenerRequest request = context.Request;
                string responseString = "";

                var body = new StreamReader(request.InputStream).ReadToEnd();
                Console.WriteLine("BODY\r\n{0}", body);
                Console.WriteLine("RAW URL:  {0}", request.RawUrl);

                switch (request.RawUrl)
                {
                    case "/":

                        Debug.WriteLine("empty request on port:  " + request.Url.Port.ToString());
                        //Debug.WriteLine(request.RawUrl);
                        break;
                    case "/setup.xml":
                        Debug.WriteLine("SETUP.XML request on port:  " + request.Url.Port.ToString());
                        //Debug.WriteLine(request.RawUrl);
                        responseString = getSetupXML();

                        //TO DO:  re-enable!
                        //done_http = true;
                        //boolRegisterSuccess = true;

                        break;
                    case "/eventservice.xml":
                        responseString = getEventServiceXML();
                        
                        break;
                    case "/upnp/control/basicevent1":
                        Debug.WriteLine("***ECHO COMMAND on port:  " + request.Url.Port.ToString());

                        //Console.WriteLine("BODY {0}", body);
                        
                        if (true || body.Contains("GetBinaryState"))
                        {
                            Console.WriteLine("Request for binary state of device...SENDING FAKE RESULT");
                            responseString = $@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/""><s:Body>
<u:GetBinaryStateResponse xmlns:u=""urn:Belkin:service:basicevent:1"">
<BinaryState>1</BinaryState>
</u:GetBinaryStateResponse>
</s:Body> </s:Envelope>
";

                            done_http = true;
                            boolRegisterSuccess = true;


                        }

                        break;
                }


                HttpListenerResponse response = context.Response;
                /*
                    
                    // SOAPACTION: "urn:Belkin:service:basicevent:1#GetBinaryState"
                }
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                //System.IO.Stream output = response.OutputStream;

                
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close(buffer, true);
                 */
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;

                ////TO DO.... probably don't need this!
                response.ContentType = "text/xml";
                response.ContentEncoding = Encoding.UTF8;
                //response.Headers.Remove("Date");
                //response.Headers.Add("Server", "");
                //response.Headers.Add("Date", "");
                //response.KeepAlive = false;
                
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close(buffer, true);



                //System.IO.Stream output = response.OutputStream;
                //output.Write(buffer, 0, buffer.Length);
                //output.Close();

                if (!done_http)
                {
                    receiveHttp(ref listener);
                }
                else
                {
                    Debug.WriteLine("Setup complete for " + strWeMoName);
                    listener.Stop();
                    listener.Close();

                    mreHttpReceiveDone.Set();
                }

            }
            else
            {
                Console.WriteLine("Listener isn't listening!");
            }
        }

        private string getEventServiceXML()
        {
            String eventservice_xml = "<scpd xmlns=\"urn:Belkin:service-1-0\">\r\n" +
                    "<actionList>\r\n" +
                      "<action>\r\n" +
                        "<name>SetBinaryState</name>\r\n" +
                        "<argumentList>\r\n" +
                          "<argument>\r\n" +
                            "<retval/>\r\n" +
                            "<name>BinaryState</name>\r\n" +
                            "<relatedStateVariable>BinaryState</relatedStateVariable>\r\n" +
                            "<direction>in</direction>\r\n" +
                            "</argument>\r\n" +
                        "</argumentList>\r\n" +
                      "</action>\r\n" +
                      "<action>\r\n" +
                        "<name>GetBinaryState</name>\r\n" +
                        "<argumentList>\r\n" +
                          "<argument>\r\n" +
                            "<retval/>\r\n" +
                            "<name>BinaryState</name>\r\n" +
                            "<relatedStateVariable>BinaryState</relatedStateVariable>\r\n" +
                            "<direction>out</direction>\r\n" +
                            "</argument>\r\n" +
                        "</argumentList>\r\n" +
                      "</action>\r\n" +
                  "</actionList>\r\n" +
                    "<serviceStateTable>\r\n" +
                      "<stateVariable sendEvents=\"yes\">\r\n" +
                        "<name>BinaryState</name>\r\n" +
                        "<dataType>Boolean</dataType>\r\n" +
                        "<defaultValue>0</defaultValue>\r\n" +
                       "</stateVariable>\r\n" +
                       "<stateVariable sendEvents=\"yes\">\r\n" +
                          "<name>level</name>\r\n" +
                          "<dataType>string</dataType>\r\n" +
                          "<defaultValue>0</defaultValue>\r\n" +
                       "</stateVariable>\r\n" +
                    "</serviceStateTable>\r\n" +
                    "</scpd>\r\n\r\n";

            return eventservice_xml;
        }

        private String getSetupXML()
        {


            string setup_xml = "<?xml version=\"1.0\"?>\r\n" +
                                "<root>\r\n" +
                                 "<device>\r\n" +
                                    "<deviceType>urn:Belkin:device:controllee:1</deviceType>\r\n" +
                                    "<friendlyName>" + strWeMoName + "</friendlyName>\r\n" +
                                    "<manufacturer>Belkin International Inc.</manufacturer>\r\n" +
                                    "<modelName>Socket</modelName>\r\n" +
                                    "<modelNumber>3.1415</modelNumber>\r\n" +
                                    "<modelDescription>Belkin Plugin Socket 1.0</modelDescription>\r\n" +
                                    "<UDN>uuid:Socket-1_0-" + strWeMoSerialNumber + "</UDN>\r\n" +
                                    "<serialNumber>221517K0101769</serialNumber>\r\n" +
                                    "<binaryState>0</binaryState>\r\n" +
                                    "<serviceList>\r\n" +
                                      "<service>\r\n" +
                                          "<serviceType>urn:Belkin:service:basicevent:1</serviceType>\r\n" +
                                          "<serviceId>urn:Belkin:serviceId:basicevent1</serviceId>\r\n" +
                                          "<controlURL>/upnp/control/basicevent1</controlURL>\r\n" +
                                          "<eventSubURL>/upnp/event/basicevent1</eventSubURL>\r\n" +
                                          "<SCPDURL>/eventservice.xml</SCPDURL>\r\n" +
                                      "</service>\r\n" +
                                  "</serviceList>\r\n" +
                                  "</device>\r\n" +
                                "</root>\r\n\r\n";


            return setup_xml;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            done_http = true;
            done_udp = true;
            mreHttpReceiveDone.Set();
            mreUdpReceiveDone.Set();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (done_http && done_udp)
            {

                if (httpListener.IsListening)
                {
                    httpListener.Stop();
                    httpListener.Close();
                }
                enableControls(true);
                timer1.Stop();
                if (boolRegisterSuccess)
                {
                    toolStripStatusLabel1.Text = "Successfully registered:  "+ strWeMoName;
                } else
                {
                    toolStripStatusLabel1.Text = "";
                }

            }
        }
    }
}
