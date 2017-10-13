using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeployServer
{
    static class Program
    {
        public delegate void DelegateMessage(string msg);
        public static string PipeName;
        static event DelegateMessage OnNewMessage;

        static string log_version = "version.log";
        static string version_filename = "version.txt";
        static string patch_version_keyword = "(patch)";

        static StreamWriter log_writer;
        static string app_path;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            app_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            log_writer = new StreamWriter(app_path + "\\log.txt", true);

            NotifyIcon tray = new NotifyIcon();
            tray.Visible = true;
            tray.Icon = Properties.Resources.main;
            tray.Click += Tray_Click;

            OnNewMessage += Program_OnNewMessage;
            Listen("ExpDeployServer");
            Application.Run();
        }

        private static void Tray_Click(object sender, EventArgs e)
        {
            FrmDeployPackage frm = new FrmDeployPackage();
            frm.Show();
        }

        private static void Program_OnNewMessage(string msg)
        {
            string[] st_arr = msg.Split(new char[] { '|' });
            string src_path = st_arr[0];
            string note_filename = st_arr[1];

            DirectoryInfo src_dir_info = new DirectoryInfo(src_path);
            string project_name = src_dir_info.Name;
            string backup_project_path = Properties.Settings.Default.backup_path + "\\" + project_name;
            string version_filepath = backup_project_path + "\\" + version_filename;
            int version = 0;
            if ((!Directory.Exists(backup_project_path) || (!File.Exists(version_filepath))))
            {
                Directory.CreateDirectory(backup_project_path);
            }
            else
            {
                StreamReader version_reader = new StreamReader(version_filepath);
                version = Int32.Parse(version_reader.ReadLine());
                version_reader.Close();
            }

            string dest_path = backup_project_path + "\\v" + version.ToString();

            string note_filepath = src_path + "\\" + note_filename;
            StreamReader note_reader = new StreamReader(note_filepath);
            string note = note_reader.ReadLine();
            note_reader.Close();
            if (!note.Contains(patch_version_keyword))
            {
                version++;
                dest_path = backup_project_path + "\\v" + version.ToString();
            }
            else
            {
                if(Directory.Exists(dest_path))
                    Directory.Delete(dest_path, true);
            }
                

            Utils.DirectoryCopy(src_path, dest_path, true, true);
            File.Copy(note_filepath, dest_path + "\\" + log_version);

            StreamWriter version_writer = new StreamWriter(version_filepath);
            version_writer.Write(version);
            version_writer.Close();
        }

        static void Listen(string pipe_name)
        {
            try
            {
                PipeName = pipe_name;

                // Create the new async pipe 
                NamedPipeServerStream pipe_server = new NamedPipeServerStream(pipe_name,
                   PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                // Wait for a connection
                pipe_server.BeginWaitForConnection
                    (new AsyncCallback(connection_callback), pipe_server);
            }
            catch (Exception oEx)
            {
                log_writer.WriteLine("Error: " + oEx.Message);
                log_writer.Flush();
            }
        }
        static void connection_callback(IAsyncResult iar)
        {
            try
            {
                // Get the pipe
                NamedPipeServerStream pipe_server = (NamedPipeServerStream)iar.AsyncState;
                // End waiting for the connection
                pipe_server.EndWaitForConnection(iar);

                byte[] buffer = new byte[255];

                // Read the incoming message
                pipe_server.Read(buffer, 0, 255);

                // Convert byte buffer to string
                string msg = Encoding.UTF8.GetString(buffer, 0, buffer.Length).TrimEnd('\0');

                // Pass message back to calling form
                OnNewMessage.Invoke(msg);

                // Kill original sever and create new wait server
                pipe_server.Close();
                pipe_server = null;
                pipe_server = new NamedPipeServerStream(PipeName, PipeDirection.InOut,
                   1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                // Recursively wait for the connection again and again....
                pipe_server.BeginWaitForConnection(
                   new AsyncCallback(connection_callback), pipe_server);
            }
            catch(Exception oEx)
            {
                log_writer.WriteLine("Error: " + oEx.Message);
                log_writer.Flush();
            }
        }
    }
}
