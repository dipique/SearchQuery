using System;
using System.Collections.Generic;
using System.Linq;

namespace Search
{
    class Program
    {
        public static OrderSearchFilter<Order> Search;
        static void Main(string[] args)
        {
            Search = new OrderSearchFilter<Order>(CreateSampleData());

            //create search
            DateTime from = new DateTime(2015, 1, 1);
            //Search.TxNumber = 2;
            Search.TransactionDateFrom = from;
            Search.MaxBigTicketItemPrice = 1000m;
            Search.SortDir = "Desc";
            Search.SortField = "TxNumber";
            //Search.CustomerName = "Jill";

            //Run search
            Console.WriteLine("Running search...");
            Search.GetResults();
            Console.WriteLine("Result transaction numbers:");
            foreach (var o in Search.Results)
                Console.WriteLine(o.TxNumber);
            Console.ReadKey();
        }

        static IQueryable<Order> CreateSampleData()
        {
            var orders = new List<Order>
            {
                new Order
                {
                    TxNumber = 1,
                    TxDate = new DateTime(2016, 2, 9),
                    Items = new List<Item>
                {
                    new Item
                    {
                        Description = "Light bulb",
                        Price = 100.00m
                    },
                    new Item
                    {
                        Description = "House",
                        Price = 200000m
                    }
                },
                    OrderCustomer = new Customer
                    {
                        Name = "Billy",
                        CustomerAddress = new Address
                        {
                            StreetNumber = 11,
                            StreetName = "Maple",
                            ZipCode = 75432
                        }
                    }
                },
                new Order
                {
                    TxNumber = 2,
                    TxDate = new DateTime(2016, 2, 2),
                    Items = new List<Item>
                {
                    new Item
                    {
                        Description = "Monitor",
                        Price = 50.00m
                    }
                },
                    OrderCustomer = new Customer
                    {
                        Name = "John",
                        CustomerAddress = new Address
                        {
                            StreetNumber = 22,
                            StreetName = "Ironwood",
                            ZipCode = 56545
                        }
                    }
                },
                new Order
                {
                    TxNumber = 3,
                    TxDate = new DateTime(2016, 1, 10),
                    Items = new List<Item>
                {
                    new Item
                    {
                        Description = "Staple",
                        Price = .01m
                    }
                },
                    OrderCustomer = new Customer
                    {
                        Name = "Jacob",
                        CustomerAddress = new Address
                        {
                            StreetNumber = 33,
                            StreetName = "Birch",
                            ZipCode = 90210
                        }
                    }
                },
                new Order
                {
                    TxNumber = 4,
                    TxDate = new DateTime(2015, 10, 9),
                    Items = new List<Item>
                {
                    new Item
                    {
                        Description = "Chapstick",
                        Price = 9500.00m
                    },
                    new Item
                    {
                        Description = "Headphones",
                        Price = 2000.57m
                    }
                },
                    OrderCustomer = new Customer
                    {
                        Name = "Jill",
                        CustomerAddress = new Address
                        {
                            StreetNumber = 44,
                            StreetName = "Sycamore",
                            ZipCode = 85753
                        }
                    }
                },
                new Order
                {
                    TxNumber = 5,
                    TxDate = new DateTime(2015, 11, 4),
                    Items = new List<Item>
                {
                    new Item
                    {
                        Description = "Phone",
                        Price = 57.99m
                    }
                },
                    OrderCustomer = new Customer
                    {
                        Name = "Joan",
                        CustomerAddress = new Address
                        {
                            StreetNumber = 55,
                            StreetName = "Oak",
                            ZipCode = 09771
                        }
                    }
                }
            };
            return orders.AsQueryable();
        }
    }
}
