using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search
{
    class Program
    {
        public static List<Order> Orders = new List<Order>();
        public static OrderSearchFilter Search = new OrderSearchFilter();
        static void Main(string[] args)
        {
            CreateSampleData();

            //create search
            DateTime from = new DateTime(2016, 1, 1);
            Search.TxNumber = 2;
            Search.TransactionDateFrom = from;

            //start search
            Search.RunSearch(Orders.AsQueryable());
        }

        static void CreateSampleData()
        {

            Orders.Add(new Order
            {
                TxNumber = 1,
                TxDate = new DateTime(2016, 2, 9),
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
            });
            Orders.Add(new Order
            {
                TxNumber = 2,
                TxDate = new DateTime(2016, 2, 2),
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
            });
            Orders.Add(new Order
            {
                TxNumber = 3,
                TxDate = new DateTime(2016, 1, 10),
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
            });
            Orders.Add(new Order
            {
                TxNumber = 4,
                TxDate = new DateTime(2015, 10, 9),
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
            });
            Orders.Add(new Order
            {
                TxNumber = 5,
                TxDate = new DateTime(2015, 11, 4),
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
            });
        }
    }
}
