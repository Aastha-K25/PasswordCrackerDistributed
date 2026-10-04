using PasswordCrackerMaster2023.util;
using PasswordCrackerSlave.util;
using PasswordCrackerSlave2023.model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace PasswordCrackerSlave2023
{
    internal class Slave
    {
        private string _passwords;
        private string _chunk;

        private List<UserInfo> _userInfos;
        private List<UserInfoClearText> _result;

        // Bruges til at lave SHA-1 hash af password-kandidaterne
        private readonly HashAlgorithm _messageDigest;

        public Slave()
        {
            _messageDigest = new SHA1CryptoServiceProvider();

            // Resultatlisten oprettes kun én gang.
            // På den måde gemmes resultater fra ALLE chunks.
            _result = new List<UserInfoClearText>();
        }


        internal void connect(string host, int port)
        {
            // Starter tidsmålingen for denne Slave
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Slave fungerer som TCP-client og forbinder til Master
            TcpClient client = new TcpClient(host, port);

            Stream networkStream = client.GetStream();

            StreamReader reader = new StreamReader(networkStream);
            StreamWriter writer = new StreamWriter(networkStream);

            writer.AutoFlush = true;
            
            // HENT PASSWORD-HASHES FRA MASTER
            writer.WriteLine("password");

            _passwords = reader.ReadLine();

            if (string.IsNullOrEmpty(_passwords))
            {
                Console.WriteLine("Could not get passwords");

                client.Close();
                return;
            }

            // JSON fra Master omdannes tilbage til C# objekter
            _userInfos =
                JsonSerializer.Deserialize<List<UserInfo>>(_passwords);



            // BED MASTER OM FØRSTE CHUNK
            writer.WriteLine("chunk");

            _chunk = reader.ReadLine();
            
            // 3. BEHANDL CHUNKS
            while (!string.IsNullOrEmpty(_chunk))
            {
                // JSON omdannes til en liste med dictionary-ord
                List<string> chunk =
                    JsonSerializer.Deserialize<List<string>>(_chunk);


                // Master sender ["no more"], når der
                // ikke findes flere chunks.
                if (chunk.Count == 1 && chunk[0] == "no more")
                {
                    // Fortæl Master at denne Slave er færdig
                    writer.WriteLine("by");

                    break;
                }


                // Crack alle ordene i det modtagne chunk
                RunCracker(chunk);


                // Når chunken er færdig, beder Slaven
                // Master om næste chunk.
                writer.WriteLine("chunk");

                _chunk = reader.ReadLine();
            }
            
            // 4. STOP TIDSMÅLING
            stopwatch.Stop();

            
            // 5. VIS RESULTATER
            Console.WriteLine();
            Console.WriteLine("-----------------------------");
            Console.WriteLine("SLAVE FINISHED");
            Console.WriteLine("-----------------------------");

            Console.WriteLine(
                "Passwords found: " + _result.Count);

            foreach (UserInfoClearText result in _result)
            {
                Console.WriteLine(result);
            }

            Console.WriteLine();
            Console.WriteLine(
                "Time elapsed: " + stopwatch.Elapsed);


            // Luk TCP-forbindelsen
            client.Close();
        }
        
        // CRACK ET CHUNK
        private void RunCracker(List<string> chunk)
        {
            foreach (string dictionaryWord in chunk)
            {
                IEnumerable<UserInfoClearText> partialResult =
                    CheckWordWithVariations(
                        dictionaryWord,
                        _userInfos);

                // Resultaterne lægges til den eksisterende liste.
                // Listen bliver IKKE nulstillet mellem chunks.
                _result.AddRange(partialResult);
            }
        }


        // --------------------------------------------------
        // GENERER PASSWORD-VARIATIONER
        // --------------------------------------------------

        private IEnumerable<UserInfoClearText> CheckWordWithVariations(
            string dictionaryEntry,
            List<UserInfo> userInfos)
        {
            List<UserInfoClearText> result =
                new List<UserInfoClearText>();


            // 1. Det normale dictionary-ord
            string possiblePassword = dictionaryEntry;

            result.AddRange(
                CheckSingleWord(
                    userInfos,
                    possiblePassword));


            // 2. Hele ordet med STORE bogstaver
            string possiblePasswordUpperCase =
                dictionaryEntry.ToUpper();

            result.AddRange(
                CheckSingleWord(
                    userInfos,
                    possiblePasswordUpperCase));


            // 3. Første bogstav stort
            string possiblePasswordCapitalized =
                StringUtilities.Capitalize(dictionaryEntry);

            result.AddRange(
                CheckSingleWord(
                    userInfos,
                    possiblePasswordCapitalized));


            // 4. Ordet skrevet baglæns
            string possiblePasswordReverse =
                StringUtilities.Reverse(dictionaryEntry);

            result.AddRange(
                CheckSingleWord(
                    userInfos,
                    possiblePasswordReverse));


            // 5. Tal efter ordet
            // Fx secret0, secret1 ... secret99
            for (int i = 0; i < 100; i++)
            {
                string possiblePasswordEndDigit =
                    dictionaryEntry + i;

                result.AddRange(
                    CheckSingleWord(
                        userInfos,
                        possiblePasswordEndDigit));
            }


            // 6. Tal foran ordet
            // Fx 0secret, 1secret ... 99secret
            for (int i = 0; i < 100; i++)
            {
                string possiblePasswordStartDigit =
                    i + dictionaryEntry;

                result.AddRange(
                    CheckSingleWord(
                        userInfos,
                        possiblePasswordStartDigit));
            }


            // 7. Tal både foran og efter ordet
            // Fx 1secret2
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    string possiblePasswordStartEndDigit =
                        i + dictionaryEntry + j;

                    result.AddRange(
                        CheckSingleWord(
                            userInfos,
                            possiblePasswordStartEndDigit));
                }
            }

            return result;
        }


        // --------------------------------------------------
        // HASH OG SAMMENLIGN ÉT PASSWORD
        // --------------------------------------------------

        private IEnumerable<UserInfoClearText> CheckSingleWord(
            IEnumerable<UserInfo> userInfos,
            string possiblePassword)
        {
            // Password-kandidaten konverteres til bytes
            char[] charArray =
                possiblePassword.ToCharArray();

            byte[] passwordAsBytes =
                Array.ConvertAll(
                    charArray,
                    PasswordFileHandler.GetConverter());


            // Kandidaten hashes med SHA-1
            byte[] encryptedPassword =
                _messageDigest.ComputeHash(passwordAsBytes);


            List<UserInfoClearText> results =
                new List<UserInfoClearText>();


            // Sammenlign kandidatens hash med
            // alle de gemte password-hashes.
            foreach (UserInfo userInfo in userInfos)
            {
                if (CompareBytes(
                        userInfo.EntryptedPassword,
                        encryptedPassword))
                {
                    // Hashene er ens.
                    // Derfor har vi fundet passwordet.
                    UserInfoClearText foundPassword =
                        new UserInfoClearText(
                            userInfo.Username,
                            possiblePassword);

                    results.Add(foundPassword);

                    Console.WriteLine(
                        userInfo.Username +
                        " " +
                        possiblePassword);
                }
            }

            return results;
        }


        // --------------------------------------------------
        // SAMMENLIGN TO HASHES
        // --------------------------------------------------

        private static bool CompareBytes(
            IList<byte> firstArray,
            IList<byte> secondArray)
        {
            // Hashene kan ikke være ens,
            // hvis de ikke har samme længde.
            if (firstArray.Count != secondArray.Count)
            {
                return false;
            }


            // Sammenlign byte for byte
            for (int i = 0; i < firstArray.Count; i++)
            {
                if (firstArray[i] != secondArray[i])
                {
                    return false;
                }
            }


            // Alle bytes var ens
            return true;
        }
    }
}