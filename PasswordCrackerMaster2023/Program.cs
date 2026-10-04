using System;
using System.Net;

namespace PasswordCrackerMaster2023
{
    class Program
    {
        static void Main(string[] args)
        {

            CrackerMaster master = new CrackerMaster();

            master.Listen(IPAddress.Loopback, 6789);
        }
    }
}

//Program.cs er startpunktet for Master-programmet.
//Først oprettes et CrackerMaster-objekt, hvilket får Masteren til at læse passwordfilen og opdele dictionary'en i chunks.
//Derefter kaldes Listen() med IPAddress.Loopback og port 6789.
//Loopback betyder, at kommunikationen foregår lokalt på samme computer, mens 6789 er den port, som Masteren lytter på.
//Masteren venter derefter på, at Slave-programmet opretter en TCP-forbindelse til denne port.