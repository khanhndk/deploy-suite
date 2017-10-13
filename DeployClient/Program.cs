using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeployClient
{
    class Program
    {
        static StreamWriter log_writer;
        static string app_path;
        static void Main(string[] args)
        {
            app_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            log_writer = new StreamWriter(app_path + "\\log.txt", true);

            NamedPipeClientStream pipe_client =
                new NamedPipeClientStream(".", "ExpDeployServer", PipeDirection.InOut, PipeOptions.None);

            if (pipe_client.IsConnected != true) { pipe_client.Connect(); }

            StreamReader sr = new StreamReader(pipe_client);
            StreamWriter sw = new StreamWriter(pipe_client);

            try
            {
                string msg = args[0] + "|" + args[1];
                log_writer.WriteLine("Deploy: " + msg);

                sw.Write(msg);
                sw.Flush();
                pipe_client.Close();
            }
            catch (Exception ex)
            {
                log_writer.WriteLine(ex.Message);
            }
            finally
            {
                log_writer.Close();
            }
        }
    }
}
