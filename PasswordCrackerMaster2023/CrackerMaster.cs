using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using PasswordCrackerMaster2023.model;
using PasswordCrackerMaster2023.util;

namespace PasswordCrackerMaster2023
{
    public class CrackerMaster
    {
        // Brugerne og deres password-hashes.
        private readonly List<UserInfo> _userInfos;

        // Dictionary'en opdeles i chunks.
        // BlockingCollection er thread-safe,
        // så flere Slaves kan hente arbejde samtidig.
        private readonly BlockingCollection<List<string>> _chunks =
            new BlockingCollection<List<string>>();


        public CrackerMaster()
        {
            // Viser den præcise passwords.txt-fil,
            // som Master faktisk bruger.
            Console.WriteLine("MASTER password file:");
            Console.WriteLine(
                Path.GetFullPath("passwords.txt"));
            Console.WriteLine();


            // Læs brugerne og deres password-hashes.
            _userInfos =
                PasswordFileHandler.ReadPasswordFile(
                    "passwords.txt");


            // Læs dictionary'en og del den op i chunks.
            CreateChunks(
                "webster-dictionary.txt");
        }


        // Starter Masterens TCP-server.
        internal void Listen(
            IPAddress loopback,
            int port)
        {
            TcpListener server =
                new TcpListener(
                    loopback,
                    port);

            server.Start();

            Console.WriteLine("Master started");
            Console.WriteLine("Waiting for slaves...");
            Console.WriteLine();


            // Master bliver ved med at acceptere nye Slaves.
            while (true)
            {
                TcpClient client =
                    server.AcceptTcpClient();

                Console.WriteLine(
                    "Slave connected");


                // Hver Slave håndteres i sin egen Task.
                // Derfor kan flere Slaves arbejde samtidig.
                Task.Run(
                    () => HandleSlave(client));
            }
        }


        // Håndterer kommunikationen med én Slave.
        private void HandleSlave(
            TcpClient client)
        {
            try
            {
                Stream networkStream =
                    client.GetStream();

                StreamReader reader =
                    new StreamReader(
                        networkStream);

                StreamWriter writer =
                    new StreamWriter(
                        networkStream);

                writer.AutoFlush = true;


                string request =
                    reader.ReadLine();


                while (request != null &&
                       request != "by")
                {
                    // Slave beder om password-hashes.
                    if (request == "password")
                    {
                        string usersAsJson =
                            JsonSerializer.Serialize(
                                _userInfos);

                        writer.WriteLine(
                            usersAsJson);
                    }


                    // Slave beder om næste chunk.
                    else if (request == "chunk")
                    {
                        // Forsøg at tage næste chunk.
                        if (_chunks.TryTake(
                                out List<string> chunk))
                        {
                            string chunkAsJson =
                                JsonSerializer.Serialize(
                                    chunk);

                            writer.WriteLine(
                                chunkAsJson);

                            Console.WriteLine(
                                "Chunk sent to Slave - " +
                                chunk.Count +
                                " words");
                        }
                        else
                        {
                            // Der findes ikke flere chunks.
                            List<string> noMore =
                                new List<string>
                                {
                                    "no more"
                                };

                            string noMoreAsJson =
                                JsonSerializer.Serialize(
                                    noMore);

                            writer.WriteLine(
                                noMoreAsJson);

                            Console.WriteLine(
                                "No more chunks - Slave finished");
                        }
                    }


                    // Vent på næste request.
                    request =
                        reader.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Slave connection closed: " +
                    ex.Message);
            }
            finally
            {
                client.Close();
            }
        }


        // Læser dictionary'en og deler den
        // op i chunks på maksimalt 10.000 ord.
        private void CreateChunks(
            string filename)
        {
            using FileStream fileStream =
                new FileStream(
                    filename,
                    FileMode.Open,
                    FileAccess.Read);

            using StreamReader dictionary =
                new StreamReader(
                    fileStream);


            List<string> words =
                new List<string>();


            while (!dictionary.EndOfStream)
            {
                string dictionaryEntry =
                    dictionary.ReadLine();


                // Tilføj altid dictionary-ordet først.
                if (dictionaryEntry != null)
                {
                    words.Add(
                        dictionaryEntry);
                }


                // Når vi har 10.000 ord,
                // er chunken færdig.
                if (words.Count == 10000)
                {
                    _chunks.Add(
                        words);

                    // Start næste chunk.
                    words =
                        new List<string>();
                }
            }


            // Tilføj de sidste ord,
            // hvis sidste chunk er mindre end 10.000.
            if (words.Count > 0)
            {
                _chunks.Add(
                    words);
            }


            // Der kommer ikke flere chunks.
            _chunks.CompleteAdding();


            Console.WriteLine(
                "Dictionary divided into chunks");
        }
    }
}