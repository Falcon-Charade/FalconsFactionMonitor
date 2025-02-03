using System;
using System.Threading;
using FalconsFactionMonitor.Services;

class Program
{
    static void Main()
    {
        string serviceCheck = "";
        while (serviceCheck != "1" && serviceCheck != "2")
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Welcome to Falcon's Faction Monitor.");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("The following services are available:");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\t1 - ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Journal Monitor Service.");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\t2 - ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Web Retrieval Service.");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("\nPlease select which service you would like to launch.");
            Console.ForegroundColor = ConsoleColor.White;
            serviceCheck = Console.ReadKey().KeyChar.ToString();
        };

        Console.Clear();


        if (serviceCheck == "1")
        {
            JournalRetrievalService service = new JournalRetrievalService();
            service.JournalRetrieval().Wait();
        }
        else
        {
            WebRetrievalService service = new WebRetrievalService();
            service.WebRetrieval().Wait();
        }

        // Final goodbye animations, etc., if you want
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Application Exiting.");
        Thread.Sleep(1000);
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("Application Exiting..");
        Thread.Sleep(1000);
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Application Exiting...");
        Thread.Sleep(1000);
    }
}
