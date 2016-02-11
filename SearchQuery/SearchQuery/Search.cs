using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Search
{
    #region Search parameter assembly
    /// <summary>
    /// The inheritable class that contains the logic to apply a set of filters to a dataset based
    /// on a set of provided conditions.  The values in the derived class are mapped to the object
    /// fields using reflection and attributes on the derived class fields.
    /// 
    /// To use this object:
    /// (1) Create a derived class that contains all the fields that will be entered as search
    ///     criteria.
    /// (2) Use the attributes defined below to link those fields to the target object fields/
    ///     properties. 
    /// (3) Create a constructor that specifies the return type
    /// (4) Create an instance of the inherited class, populate the search fields, and call the
    ///     ApplyFilters method on the input data.
    /// </summary>
    [Serializable]
    public abstract class SearchQuery
    {
        #region Return type property -- must be specified in inherited classes

        //Type of the IQueryable return object
        public Type ResultType { get; set; }

        #endregion

        public SearchQuery(Type searchResultType)
        {
            ResultType = searchResultType;
            string invalidFieldName = GetInvalidLinkedField();
            if (!string.IsNullOrEmpty(invalidFieldName))
                throw (ErrorInfo = new Exception("Found invalid linked field or property: " + invalidFieldName));
        }

        public IQueryable<T> GetQuery<T>(IQueryable<T> data)
        {
            return ApplyFilters<T>(data);
        }

        public List<T> GetResults<T>(IQueryable<T> data)
        {
            Results = GetQuery(data).ToList();
            return Results;
        }

        public dynamic Results { get; set; }

        #region Search Properties

        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public string SortField { get; set; }
        public string SortDir { get; set; }
        public string SortString { get { return SortField + " " + SortDir; } }

        #endregion

        #region Attributes to define inherited class properties & fields

        /// <summary>
        /// Attribute that contains what comparison type should be used for that field.
        /// </summary>
        protected class Comparison : Attribute
        {
            public ExpressionType Type;
            public Comparison(ExpressionType type)
            {
                Type = type;
            }
        }

        /// <summary>
        /// Attribute that contains the target field/property to be compared against
        /// </summary>
        protected class LinkedField : Attribute
        {
            public string TargetField;
            public LinkedField(string target)
            {
                TargetField = target;
            }
        }

        /// <summary>
        /// Linked fields default to the member name if not specified; this method handles that logic
        /// </summary>
        /// <param name="member"></param>
        /// <returns>Returns the target field, is string.Empty if none</returns>
        private string GetTargetField(MemberInfo member)
        {
            if (member == null) return string.Empty;
            var attrib = (LinkedField)member.GetCustomAttribute(typeof(LinkedField));
            if (attrib == null) return string.Empty;
            if (string.IsNullOrEmpty(attrib.TargetField)) return member.Name;
            return attrib.TargetField;
        }

        #endregion

        #region Error Info

        public bool SuccessfullyCompletedSearch { get { return ErrorInfo == null; } }
        public Exception ErrorInfo { get; protected set; }

        #endregion

        #region Reflection-based Search logic

        #region Attribute Validation

        /// <summary>
        /// Validates all the linked field names in the inherited class and returns the first invalid name if one is found
        /// </summary>
        /// <returns></returns>
        private string GetInvalidLinkedField()
        {
            //get a list of linked fields (defaults to member name if no linked field specified)
            var fields = GetType().GetFields().Cast<MemberInfo>()
                                  .Concat(GetType().GetProperties())
                                  .Select(m => GetTargetField(m))
                                  .Where(s => !string.IsNullOrEmpty(s));

            //loop through linked field names and validate against the result type
            foreach (string fieldName in fields)
                if (!ValidateLinkedField(fieldName)) return fieldName;

            //if all the links were good, return an empty string
            return string.Empty;
        }

        private bool ValidateLinkedField(string fieldName)
        {
            //loop through the "levels" (e.g. Order / Customer / Name) validating that the fields/properties all exist
            Type currentType = ResultType;
            foreach (string currentLevel in DeQualifyExpression(fieldName, ResultType))
            {
                MemberInfo match = (MemberInfo)currentType.GetField(currentLevel) ?? currentType.GetProperty(currentLevel);
                if (match == null) return false;
                currentType = match.MemberType == MemberTypes.Property ? ((PropertyInfo)match).PropertyType
                                                                       : ((FieldInfo)match).FieldType;
            }
            return true; //if we checked all levels and found matches, exit
        }

        #endregion

        /// <summary>
        /// Apply all search filters to a dataset
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="data"></param>
        private IQueryable<T> ApplyFilters<T>(IQueryable<T> data)
        {
            if (data == null) return null;
            IQueryable<T> retVal = data.AsQueryable();

            //get all the fields and properties that have search attributes specified
            var fields = GetType().GetFields().Cast<MemberInfo>()
                                  .Concat(GetType().GetProperties())
                                  .Where(f => f.GetCustomAttribute(typeof(LinkedField)) != null)
                                  .Where(f => f.GetCustomAttribute(typeof(Comparison)) != null);

            //loop through them and generate expressions for validation and searching
            try
            {
                foreach (var f in fields)
                {
                    var value = f.MemberType == MemberTypes.Property ? ((PropertyInfo)f).GetValue(this) : ((FieldInfo)f).GetValue(this);
                    if (value == null) continue;
                    Type t = f.MemberType == MemberTypes.Property ? ((PropertyInfo)f).PropertyType : ((FieldInfo)f).FieldType;
                    retVal = new SearchFilter<T>
                    {
                        SearchValue = value,
                        ApplySearchCondition = GetValidationExpression(t),
                        SearchExpression = GetSearchExpression<T>(GetTargetField(f), ((Comparison)f.GetCustomAttribute(typeof(Comparison))).Type, value)
                    }.Apply(retVal); //once the expressions are generated, go ahead and (try to) apply it
                }
            }
            catch (Exception ex) { throw (ErrorInfo = ex); }
            return retVal;
        }

        /// <summary>
        /// Represents a condition to be applied AND the logic to determine whether or not the condition
        /// will be applied (for example, by checking for null values).
        /// 
        /// One of these will be created for every search field defined in the inherited class
        /// </summary>
        private class SearchFilter<T>
        {
            public Expression<Func<object, bool>> ApplySearchCondition { get; set; }
            public Expression<Func<T, bool>> SearchExpression { get; set; }
            public object SearchValue { get; set; }

            public IQueryable<T> Apply(IQueryable<T> query)
            {
                //if the search value meets the criteria (e.g. is not null), apply it; otherwise, just return the original query.
                bool valid = ApplySearchCondition.Compile().Invoke(SearchValue);
                return valid ? query.Where(SearchExpression) : query;
            }
        }

        #region Validation & Search lambda expression construction

        /// <summary>
        /// Get an expression where the input (object) will be compared
        /// to a value, then return true if the object matches or false if not
        /// </summary>
        /// <param name="targetField">Property/field of the target object</param>
        /// <param name="comparison">Type of binary comparison</param>
        /// <param name="value">Value entered by user</param>
        /// <returns>Expression for search mutator</returns>
        private Expression<Func<T, bool>> GetSearchExpression<T>(
            string targetField, ExpressionType comparison, object value)
        {
            //get the property or field of the target object (ResultType)
            //which will contain the value to be checked
            var param = Expression.Parameter(ResultType, "t");
            Expression left = null;
            foreach (var part in DeQualifyExpression(targetField, ResultType))
                left = Expression.PropertyOrField(left == null ? param : left, part);

            //Get the value against which the property/field will be compared
            var right = Expression.Constant(value);

            //join the expressions with the specified operator
            var binaryExpression = Expression.MakeBinary(comparison, left, new SwapVisitor(left, param).Visit(right));
            return Expression.Lambda<Func<T, bool>>(binaryExpression, param);

        }

        /// <summary>
        /// Remove qualifying names from a target field.  For example, if targetField is "Order.Customer.Name" and
        /// targetType is Order, the de-qualified expression will be "Customer.Name" split into constituent parts
        /// </summary>
        /// <param name="targetField"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        public static List<string> DeQualifyExpression(string targetField, Type targetType)
        {
            var r = targetField.Split('.').ToList();
            foreach (var p in targetType.Name.Split('.'))
                if (r.First() == p) r.RemoveAt(0);
            return r;
        }

        public class SwapVisitor : ExpressionVisitor
        {
            private readonly Expression from, to;
            public SwapVisitor(Expression from, Expression to)
            {
                this.from = from;
                this.to = to;
            }
            public override Expression Visit(Expression node)
            {
                return node == from ? to : base.Visit(node);
            }
        }

        /// <summary>
        /// Based on the object type, provides the logic to determine whether the field should be searched. For example,
        /// if the item is of type int?, this returns that the item should not be used as a search parameter unless it
        /// is not null and has a value >= 0.
        /// 
        /// //TODO: allow validation regex to be specified for a field via attribute
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static Expression<Func<object, bool>> GetValidationExpression(Type type)
        {
            //throw exception for non-nullable types (strings are nullable, but is a reference type and thus has to be called out separately)
            if (type != typeof(string) && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                throw new Exception("Non-nullable types not supported.");

            //strings can't be blank, numbers can't be 0, and dates can't be minvalue
            if (type == typeof(string   )) return t => !string.IsNullOrWhiteSpace((string)t);
            if (type == typeof(int?     )) return t => t != null && (int)t >= 0;
            if (type == typeof(decimal? )) return t => t != null && (decimal)t >= decimal.Zero;
            if (type == typeof(DateTime?)) return t => t != null && (DateTime?)t != DateTime.MinValue;

            //everything else just can't be null
            return t => t != null;
        }

        #endregion

        #endregion
    }

    #region Test Code

    public class OrderSearchFilter : SearchQuery
    {
        public OrderSearchFilter() : base(typeof(Order)) { }

        public void RunSearch(IQueryable<Order> data)
        {
            Console.WriteLine("Running search...");
            Results = GetResults(data);
            Console.WriteLine("Result transaction numbers:");
            foreach (var o in Results)
                Console.WriteLine(o.TxNumber);
            Console.ReadKey();
        }

        [LinkedField("TxDate")]
        [Comparison(ExpressionType.GreaterThanOrEqual)]
        public DateTime? TransactionDateFrom { get; set; }

        [LinkedField("TxDate")]
        [Comparison(ExpressionType.LessThanOrEqual)]
        public DateTime? TransactionDateTo { get; set; }

        [LinkedField("")]
        [Comparison(ExpressionType.Equal)]
        public int? TxNumber { get; set; }

        [LinkedField("Order.OrderCustomer.Name")]
        [Comparison(ExpressionType.Equal)]
        public string CustomerName { get; set; }

        [LinkedField("Order.OrderCustomer.CustomerAddress.ZipCode")]
        [Comparison(ExpressionType.Equal)]
        public int? CustomerZip { get; set; }
    }

    #region Test Dataset Classes

    public class Order
    {
        public int TxNumber;
        public Customer OrderCustomer;
        public DateTime TxDate;
    }

    public class Customer
    {
        public string Name;
        public Address CustomerAddress;
    }

    public class Address
    {
        public int StreetNumber;
        public string StreetName;
        public int ZipCode;
    }

    #endregion

    #endregion

    #endregion
}