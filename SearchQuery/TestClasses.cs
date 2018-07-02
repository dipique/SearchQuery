using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Search
{
    public class Order
    {
        public int TxNumber;
        public Customer OrderCustomer;
        public DateTime TxDate;
        public ICollection<Item> Items;
    }

    public class Customer
    {
        public string Name;
        public Address CustomerAddress;
    }

    public class Item
    {
        public string Description;
        public decimal Price;
    }

    public class Address
    {
        public int StreetNumber;
        public string StreetName;
        public int ZipCode;
    }

    public class OrderSearchFilter<T> : SearchQuery<T>
    {
        public OrderSearchFilter(IQueryable<T> data) { Data = data; }
        public IQueryable<T> Data;

        public override IQueryable<T> GetQuery()
        {
            return ApplyFilters(Data);
        }

        [LinkedField("TxDate", ExpressionType.GreaterThanOrEqual)]
        public DateTime? TransactionDateFrom { get; set; }

        [LinkedField("TxDate", ExpressionType.LessThanOrEqual)]
        public DateTime? TransactionDateTo { get; set; }

        [LinkedField("")]
        public int? TxNumber { get; set; }

        [LinkedField("Order.OrderCustomer.Name")]
        public string CustomerName { get; set; }

        [LinkedField("Order.OrderCustomer.CustomerAddress.ZipCode")]
        public int? CustomerZip { get; set; }

        [LinkedField("Order.Items.Price", ExpressionType.GreaterThanOrEqual)]
        public decimal? MinBigTicketItemPrice { get; set; }

        [LinkedField("Order.Items.Price", ExpressionType.GreaterThanOrEqual, EnumerableMethod.None)]
        public decimal? MaxBigTicketItemPrice { get; set; }
    }
}
