using OPCManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OPCDemo
{
    class Program
    {
        static async void Main(string[] args)
        {
            OPCClient.Instance.InIt("127.0.0.1", 49320, true);
            #region write
            Dictionary<string, string> dic = new Dictionary<string, string>() {
                { "ns=2;s=Channel9.Device1._System._Enabled","false" }
            };
            OPCClient.Instance.WriteAsync(dic);
            #endregion
            #region Read
            List<string> path = new List<string>() { "ns=2;s=Channel9.Device1._System._Enabled" };
            var values=await OPCClient.Instance.ReadAsync(path);
            foreach(var key in values.Keys)
            {
                Console.WriteLine(string.Format("path={0},value={1}", key, values[key]));
            }
            #endregion
            Thread.Sleep(100000);
            Console.WriteLine(OPCClient.Instance.LogTxt.ToString());

            Console.ReadKey();
        }
    }
}
