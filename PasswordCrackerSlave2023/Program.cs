using System;

namespace PasswordCrackerSlave2023
{
    class Program
    {
        static void Main(string[] args)
        {

            Slave slave = new Slave();

            slave.connect("localhost", 6789);

            
        }
    }

}
