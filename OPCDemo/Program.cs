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
        static void Main(string[] args)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>() {
                { "ns=2;s=Channel9.Device1._System._Enabled","false" }
            };
            string str = "MM-dd-HH ss:fff ";
            Console.WriteLine(str.Length);

            OPCClient.Instance.InIt("127.0.0.1", 49320, true);

            Thread.Sleep(100000);
            Console.WriteLine(OPCClient.Instance.LogTxt.ToString());

            Console.ReadKey();
        }
    }
}
