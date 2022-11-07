using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/*
    个人微信：a7761075
    联系邮箱：yinxurong@darsh.cn
    创建时间：2022/11/7 8:57:33
    主要用途：opc类
    更改记录：
                  时间：            更改记录：
*/

namespace OPCManager
{
    public class OPCClient
    {
        public static OPCClient Instance = new Lazy<OPCClient>(() => { return new OPCClient(); }).Value;
        private readonly UAClientHelperAPI myClientHelperAPI;
        private EndpointDescription mySelectedEndpoint;
        private object _objLock = new object();
        private bool _Logbo = false;
        private Dictionary<string, IPAddress> _DeviceIP = new Dictionary<string, IPAddress>();

        /// <summary>
        /// 黑匣子缓存
        /// </summary>
        public StringBuilder LogTxt;
        public OPCClient()
        {
            myClientHelperAPI = new UAClientHelperAPI();
        }
        /// <summary>
        /// 初始化opc类
        /// </summary>
        /// <param name="ip">opc ip地址</param>
        /// <param name="port">opc 端口号</param>
        /// <param name="logbo">是否启动log，若为ture则记录读写黑匣子到LogTxt缓存</param>
        public void InIt(string ip,int port,bool logbo=false)
        {
            Connect(ip, port);
            if (logbo)
            {
                _Logbo = true;
                LogTxt = new StringBuilder();
            }

            try
            {
                List<string> PingDer = new List<string>();
                List<string> paths = new List<string>();
                //便利设备若有掉线设备则禁用禁用此设备的数据收集
                ReferenceDescriptionCollection referenceDescriptionCollection = myClientHelperAPI.BrowseRoot();
                referenceDescriptionCollection.RemoveRange(1, referenceDescriptionCollection.Count - 1);
                foreach (ReferenceDescription referenceDescription in referenceDescriptionCollection)
                {
                    ReferenceDescriptionCollection reference = myClientHelperAPI.BrowseNode(referenceDescription);
                    if (reference.Count < 15)
                        return;
                    reference.RemoveRange(0, 14);
                    foreach (var referen in reference)
                    {
                        var re = myClientHelperAPI.BrowseNode(referen);
                        re.RemoveRange(0, 2);
                        foreach (var r in re)
                        {
                            PingDer.Add(r.NodeId.ToString());
                            paths.Add(r.NodeId.ToString() + "._System._DeviceId");
                        }
                    }
                }

                List<string> values = myClientHelperAPI.ReadValues(paths);
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i] == "1")
                    {
                        values[i] = "127.0.0.1";
                    }
                    if (IPAddress.TryParse(values[i], out IPAddress iP))
                    {
                        _DeviceIP.Add(PingDer[i], iP);
                    }
                }
                //key设备设能地址，value=值
                Dictionary<string, string> dic = new Dictionary<string, string>();
                foreach (var path in PingDer)
                {
                    dic.Add(path + "._System._Enabled", "false");
                }
                Task.Run(() =>
                {
                    do
                    {
                        Ping ping = new Ping();
                        foreach (string device in _DeviceIP.Keys)
                        {
                            string key = device + "._System._Enabled";
                            var results = ping.Send(_DeviceIP[device]);
                            Debug.WriteLine(string.Format("{3} PING Path={0},Ip={1},results={2}", device, _DeviceIP[device], results.Status, DateTime.Now.ToString("MM-dd-HH mm:ss:fff")));
                            if (results.Status != IPStatus.Success)
                            {
                                dic[key] = "false";
                            }
                            else
                            {
                                dic[key] = "true";
                            }
                        }
                        Write(dic);

                        Thread.Sleep(2000);
                    } while (true);
                });
            }
            catch(Exception ex)
            {
                Debug.WriteLine("InIt error" + ex.Message);
                Log("InIt error" + ex.Message);
            }
         
        }
        /// <summary>
        /// 将字符串转换为IP
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string ConvertIPAddress(string str)
        {
            IPAddress tmpIp;
            if (IPAddress.TryParse(str, out tmpIp))
            {
                return IPAddress.Parse(str).ToString();
            }
            else
            {
                return "";
            }
        }
        private void Log(string mes)
        {
            lock (_objLock)
            {
                if (LogTxt.Length >= 200000)
                {
                    //LogTxt.ToString().Substring(LogTxt.ToString().IndexOf(Environment.NewLine) + 1);
                    LogTxt.Remove(0, LogTxt.ToString().IndexOf(Environment.NewLine) + 1);
                }
                LogTxt.Append(DateTime.Now.ToString("MM-dd-HH mm:ss:fff "));
                LogTxt.AppendLine(mes);
            }
        }
        private void Log(string mes,Dictionary<string,string> dic)
        {
            lock (_objLock)
            {
                foreach(var path in dic.Keys)
                {
                    if (LogTxt.Length >= 200000)
                    {
                        LogTxt.Remove(0, LogTxt.ToString().IndexOf(Environment.NewLine) + 1);
                    }
                    LogTxt.Append(DateTime.Now.ToString("MM-dd-HH mm:ss:fff "));//16
                    LogTxt.AppendLine(string.Format("{0} path={1},value={2}", mes, path, dic[path]));
                }
            }
        }
        /// <summary>
        /// 连接opc
        /// </summary>
        /// <param name="ip">ip</param>
        /// <param name="port">UA端口</param>
        private void Connect(string ip, int port)
        {
            string opcURL = string.Format("opc.tcp://{0}:{1}", ip, port);
            try
            {
                ApplicationDescriptionCollection servers = myClientHelperAPI.FindServers(opcURL);
                foreach (ApplicationDescription ad in servers)
                {
                    foreach (string url in ad.DiscoveryUrls)
                    {
                        EndpointDescriptionCollection endpoints = myClientHelperAPI.GetEndpoints(url);
                        foreach (EndpointDescription ep in endpoints)
                        {
                            //"http://opcfoundation.org/UA/SecurityPolicy#None"//应该是无密码
                            //http://opcfoundation.org/UA/SecurityPolicy#Basic128Rsa15
                            if (ep.SecurityPolicyUri == "http://opcfoundation.org/UA/SecurityPolicy#Basic128Rsa15")
                            {
                                mySelectedEndpoint = ep;
                                break;
                            }
                        }
                    }
                }
                myClientHelperAPI.Connect(mySelectedEndpoint, false, "UserName", "PassWord");
            }
            catch(Exception ex)
            {
                Log("Connect error" + ex.Message);
                throw ex;
            }

        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="dic">地址+值</param>
        public void Write(Dictionary<string, string> dic)
        {
            List<string> values = new List<string>();
            List<string> nodeIdStrings = new List<string>();
            foreach (var path in dic.Keys)
            {
                nodeIdStrings.Add(path);
                values.Add(dic[path]);
            }
            myClientHelperAPI.WriteValues(values, nodeIdStrings);
            if (_Logbo)
            {
                Log("OPCWrite", dic);
            }
        }
        /// <summary>
        /// 读取
        /// </summary>
        /// <param name="paths">地址</param>
        /// <returns>地址+值</returns>
        public Dictionary<string, string> Read(List<string> paths)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            List<string> values = myClientHelperAPI.ReadValues(paths);
            for (int i = 0; i < values.Count; i++)
            {
                if (_Logbo)
                {
                    Log(string.Format("ReadOPC path={0},value={1}", paths[i],values[i]));
                }
                dic.Add(paths[i], values[i]);
            }
            return dic;
        }
        /// <summary>
        /// 异步读取
        /// </summary>
        /// <param name="paths">PLC地址</param>
        /// <returns>地址跟value对应</returns>
        public async Task<Dictionary<string, string>> ReadAsync(List<string> paths)
        {
            Dictionary<string, string> res = new Dictionary<string, string>();
            try
            {
                Task<Dictionary<string, string>> T_dic = Task.Run(() =>
                {
                    var values = myClientHelperAPI.ReadValues(paths);
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (_Logbo)
                        {
                            Log(string.Format("OPCReadAsync path={0},value={1}", paths[i], values[i]));
                        }
                        res.Add(paths[i], values[i]);
                    }
                    return res;
                });
                res = await T_dic;
            }
            catch (Exception ex)
            {
                Log("ReadAsync " + ex.Message);
                throw ex;
            }
            return res;
        }
        /// <summary>
        /// 异步写入
        /// </summary>
        /// <param name="dic"></param>
        public async void WriteAsync(Dictionary<string, string> dic)
        {
            try
            {
                await Task.Run(() =>
                {
                    List<string> values = new List<string>();
                    List<string> path = new List<string>();
                    foreach (var a in dic.Keys)
                    {
                        values.Add(dic[a]);
                        path.Add(a);
                    }
                    myClientHelperAPI.WriteValues(values, path);

                    if (_Logbo)
                    {
                        Log("OPCWriteAsync", dic);
                    }
                });
            }
            catch (Exception ex)
            {
                Log("WriteAsync " + ex.Message);
                throw ex;
            }
        }
    }
}
