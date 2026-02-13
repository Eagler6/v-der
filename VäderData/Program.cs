using System;
using System.Collections.Generic;
using System.Linq;
using VäderData.Infrastructure;
using VäderData.Models;

namespace VäderData
{
    internal class Program
    {
        static void Main(string[] args)
        {
            bool running = true;
            while (running)
            {
                Console.Clear();
                var choice = Menu.MenuStartPage();
                switch (choice)
                {
                    case "1":
                        Menu.MenuSearchDate();
                        break;
                    case "2":
                        Menu.MenuStatisticsMenu();
                        break;
                    case "3":
                        running = false;
                        break;
                    default:
                        break;
                }
            }
        }

    }
}
