using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace AsaborAutoPatcher
{
    class Program
    {
        const string c_Path = "autopatch_config.txt";
        const string v_Path = "autopatch_versions.txt";

        static void Main(string[] args)
        {
            IPEndPoint endpoint = GetIPEndPoint();
            Dictionary<string, Version> versions = GetVersions();

            TcpClient client = new TcpClient();
            client.Connect(endpoint.Address, endpoint.Port);

            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream);
            StreamWriter writer = new StreamWriter(stream);

            Console.WriteLine("Connected to the endpoint.");

            versions = GetPatchData(reader, versions);

            foreach (KeyValuePair<string, Version> entry in versions)
                Patch(entry.Key, entry.Value, client.ReceiveBufferSize, stream, reader, writer);

            Console.WriteLine("Patching complete! Enjoy!");
            stream.Close();
            client.Close();
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

        public static Dictionary<string, Version> GetVersions()
        {
            Dictionary<string, Version> versions = new Dictionary<string, Version>();

            if (File.Exists(v_Path))
            {
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
                            throw new FormatException("Invalid version '" + entry[1] +"' in " + v_Path);

                        versions.Add(entry[0], version);
                    }
                }
            }
            
            return versions;
        }

        static Dictionary<string, Version> GetPatchData(StreamReader reader, Dictionary<string, Version> versions)
        {
            Dictionary<string, Version> patchable = new Dictionary<string, Version>();

            string line;
            while ((line = reader.ReadLine()) != "end of patch data")
            {
                if (line != null)
                {
                    Console.Write(line);

                    string[] data = line.Split(':');

                    if (data.Length != 2)
                        throw new FormatException();

                    string file = data[0];
                    Version version;

                    if (!Version.TryParse(data[1], out version))
                        throw new FormatException();

                    if (versions.ContainsKey(file))
                    {
                        if (versions[file].CompareTo(version) == 0)
                        {
                            Console.WriteLine(" (up-to-date)");
                            continue;
                        }
                    }

                    Console.WriteLine(" (pending for a patch)");
                    patchable.Add(file, version);
                }
            }

            Console.WriteLine(patchable.Count + " file(s) to be patched...");
            Console.WriteLine();
            return patchable;
        }

        static void Patch(string file, Version version, int receiveBufferSize, NetworkStream stream, StreamReader reader, StreamWriter writer)
        {
            Console.Write("Requesting " + file + "...");
            writer.WriteLine(file);
            writer.Flush();
            Console.WriteLine("Requested!");

            int size = GetFileSize(reader);

            Console.WriteLine("Downloading " + file + "...");

            if (File.Exists(file))
                File.Delete(file);

            DateTime last = DateTime.MinValue;
            byte[] buffer = new byte[receiveBufferSize];
            using (MemoryStream memory = new MemoryStream())
            {
                while (memory.Length < size)
                {
                    int numberOfBytesRead = stream.Read(buffer, 0, buffer.Length);

                    memory.Write(buffer, 0, numberOfBytesRead);

                    if (DateTime.Now > last + TimeSpan.FromSeconds(1))
                    {
                        Console.WriteLine(memory.Length + "b/" + size + "b");
                        last = DateTime.Now;
                    }
                }

                Console.WriteLine(memory.Length + "b/" + size + "b" + " Download Complete!");
                Console.Write("Writing " + file + "...");

                using (FileStream fs = File.Create(file))
                {
                    memory.WriteTo(fs);
                    Console.WriteLine("Done!");
                }

                UpdateVersion(file, version);
                Console.WriteLine();
            }
        }

        static int GetFileSize(StreamReader reader)
        {
            Console.Write("Acquiring the file size...");

            string line;
            while (true)
            {
                if ((line = reader.ReadLine()) != null)
                {
                    int size;

                    if (!int.TryParse(line, NumberStyles.None, NumberFormatInfo.CurrentInfo, out size))
                        throw new FormatException();

                    Console.WriteLine("Done!");
                    return size;
                }
            }
        }

        static void UpdateVersion(string name, Version version)
        {
            Console.Write("Updating version...");
            string entries = name + ":" + version + Environment.NewLine;

            if (File.Exists(v_Path))
            {
                using (StreamReader reader = new StreamReader(v_Path))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (!line.StartsWith(name))
                            entries = entries + line + Environment.NewLine;
                    }
                }
            }

            File.WriteAllText(v_Path, entries.Trim());
            Console.WriteLine("Done!");
        }
    }
}
