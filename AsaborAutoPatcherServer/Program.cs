using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AsaborPatcherServer
{
    class Program
    {
        const string c_Path = "config.txt";
        const string v_Path = "versions.txt";

        static Dictionary<string, Version> versions;
        static Dictionary<string, byte[]> files;

        static void Main(string[] args)
        {
            IPEndPoint endpoint = GetIPEndPoint();
            versions = GetVersions();
            files = GetFiles(new List<string>(versions.Keys));

            TcpListener listener = new TcpListener(endpoint.Address, endpoint.Port);
            Console.WriteLine("Started Listening...");
            listener.Start();

            Slice(listener);
            Console.ReadLine();
        }

        static IPEndPoint GetIPEndPoint()
        {
            if (!File.Exists(c_Path))
                throw new FileNotFoundException("Could not find " + c_Path);

            string[] config = File.ReadAllText(c_Path).Split(':');

            if (config.Length != 2)
                throw new FormatException("Invalid endpoint format");

            IPAddress ip;
            int port;

            if (!IPAddress.TryParse(config[0], out ip))
                throw new FormatException("Invalid IP address");
            else if (!int.TryParse(config[1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
                throw new FormatException("Invalid port");

            return new IPEndPoint(ip, port);
        }

        static Dictionary<string, Version> GetVersions()
        {
            Dictionary<string, Version> versions = new Dictionary<string, Version>();

            if (!File.Exists(v_Path))
                throw new FileNotFoundException("Could not find " + v_Path);

            using (StreamReader reader = new StreamReader(v_Path))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    string[] entry = line.Split(':');

                    if (entry.Length != 2)
                        throw new FormatException("Invalid entry in " + v_Path);

                    Version version;
                    if (!File.Exists(entry[0]))
                        throw new FileNotFoundException("Could not find " + entry[0]);
                    else if (!Version.TryParse(entry[1], out version))
                        throw new FormatException("Invalid version '" + entry[1] + "' in " + v_Path);

                    versions.Add(entry[0], version);
                }
            }

            return versions;
        }

        static Dictionary<string, byte[]> GetFiles(List<string> versions)
        {
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

            Console.WriteLine("Loading Files...");

            foreach (string entry in versions)
            {
                if (!File.Exists(entry))
                    throw new FileNotFoundException("Could not find: " + entry);

                Console.Write(entry);
                byte[] data = File.ReadAllBytes(entry);
                Console.WriteLine(" (" + data.Length + "b)");
                files.Add(entry, data);
            }

            Console.WriteLine("Files loaded!");

            return files;
        }

        static void Slice(TcpListener listener)
        {
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();

                Thread t = new Thread(new ParameterizedThreadStart(HandleClient));
                t.Start(client);
            }
        }

        public static void HandleClient(Object obj)
        {
            try
            {
                TcpClient client = (TcpClient)obj;
                string address = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                Console.WriteLine("New connection from " + address);

                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream);
                StreamWriter writer = new StreamWriter(stream);

                Console.WriteLine("Sending patch data to " + address);

                foreach (KeyValuePair<string, Version> entry in versions)
                    writer.WriteLine(entry.Key + ":" + entry.Value);

                writer.WriteLine("end of patch data");
                writer.Flush();

                string request;
                while (true)
                {
                    if ((request = reader.ReadLine()) != null && versions.ContainsKey(request))
                    {
                        Console.WriteLine(request + " requested by " + address);

                        byte[] content = files[request];

                        writer.WriteLine(content.Length);
                        writer.Flush();

                        stream.Write(content, 0, content.Length);

                        Console.WriteLine("Request complete");
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}
