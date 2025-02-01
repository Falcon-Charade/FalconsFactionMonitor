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
            Console.WriteLine("Welcome to Falcon's Faction Monitor.");
            Console.WriteLine("The following services are available:");
            Console.WriteLine("\t1 - Journal Monitor Service.");
            Console.WriteLine("\t2 - Web Retrieval Service.");
            Console.WriteLine("Please select which service you would like to launch.");
            serviceCheck = Console.ReadLine();
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

        Console.Clear();
        Console.WriteLine("Application Exiting.");
        Thread.Sleep(1000);
        Console.Clear();
        Console.WriteLine("Application Exiting..");
        Thread.Sleep(1000);
        Console.Clear();
        Console.WriteLine("Application Exiting...");
        Thread.Sleep(1000);
    }
}
