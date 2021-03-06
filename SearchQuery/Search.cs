﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Dynamic;
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
    ///     
    /// Note: Credit to http://stackoverflow.com/a/35382430/2524708 for contributions to dynamic
    ///       predicate development
    /// </summary>
    [Serializable]
    public abstract class SearchQuery<T>
    {
        public SearchQuery()
        {
            //defaults
            string invalidFieldName = GetInvalidLinkedField();
            if (!string.IsNullOrEmpty(invalidFieldName))
                throw (ErrorInfo = new Exception("Found invalid linked field or property: " + invalidFieldName));
        }

        public abstract IQueryable<T> GetQuery();
        public IQueryable<T> GetQuery(IQueryable<T> data) => ApplyFilters(data);
        public List<T> GetResults(IQueryable<T> data = null)
        {
            var query = data == null ? GetQuery() : GetQuery(data);
            Results = query.Skip(Skip).Take(Take).ToList();
            TotalCount = query.Skip(MAX_ROWS).Any() ? MAX_ROWS
                                                    : query.Count();
            return Results;
        }

        #region Search Properties

        public List<T> Results { get; set; }
        public int TotalCount { get; set; }        
        public int PageCount { get; set; }
        public int PageSize { get; set; } = 10;
        public int CurrentPage { get; set; } = 1;
        public string SortField { get; set; } = string.Empty;

        protected string _sortDir = string.Empty;
        public string SortDir
        {
            get { return _sortDir; }
            set
            {
                //only allow valid sort values
                var acceptedValues = new[] { "asc", "desc" };
                if (acceptedValues.Contains(value.ToLower()))
                    _sortDir = value;
            }
        }

        public string SortString
        {
            get
            {
                return string.IsNullOrWhiteSpace(SortField)
                    || string.IsNullOrWhiteSpace(SortDir) ? string.Empty
                                                          : $"{SortField} {SortDir}";
            }
        }

        protected int MAX_ROWS { get { return PageCount * PageSize; } }
        public int Skip { get { return Math.Max(0, (CurrentPage - 1) * PageSize); } }
        public int Take { get { return PageSize; } }

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
                                  .Where(s => !string.IsNullOrEmpty(s))
                                  .ToList();

            //Also check the Sort field
            if (!string.IsNullOrWhiteSpace(SortField)) fields.Add(SortField);

            //loop through linked field names and validate against the result type
            foreach (string fieldName in fields)
                if (!ValidateLinkedField(fieldName)) return fieldName;

            //if all the links were good, return an empty string
            return string.Empty;
        }

        private bool ValidateLinkedField(string fieldName) => FieldExists(typeof(T), fieldName);

        private bool FieldExists(Type type, string fieldName)
        {
            //loop through the "levels" (e.g. Order / Customer / Name) validating that the fields/properties all exist
            Type currentType = type;
            foreach (string currentLevel in DeQualifyFieldName(fieldName, typeof(T)))
            {
                MemberInfo match = (MemberInfo)currentType.GetField(currentLevel) ?? currentType.GetProperty(currentLevel);
                if (match == null) return false;
                currentType = GetMemberType(match, true);
            }
            return true; //if we checked all levels and found matches, exit
        }

        /// <summary>
        /// Linked fields default to the member name if not specified; this method handles that logic
        /// </summary>
        /// <param name="member"></param>
        /// <returns>Returns the target field, is string.Empty if none</returns>
        private string GetTargetField(MemberInfo member)
        {
            if (member == null) return string.Empty;
            var attrib = GetAttribute<LinkedField>(member);
            if (attrib == null) return string.Empty;
           return string.IsNullOrEmpty(attrib.TargetField) ? member.Name
                                                           : attrib.TargetField;
        }

        #endregion

        /// <summary>
        /// Apply all search filters to a dataset
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="data"></param>
        protected IQueryable<T> ApplyFilters(IQueryable<T> data)
        {
            if (data == null) return null;
            IQueryable<T> retVal = data.AsQueryable();

            //get all the fields and properties that have search attributes specified
            var fields = GetFieldsAndProperties(GetType()).Where(f => f.GetCustomAttribute(typeof(LinkedField)) != null);

            //loop through them and generate expressions for validation and searching
            try
            {
                foreach (var f in fields)
                {
                    var value = GetMemberValue(f, this);
                    if (value == null) continue;
                    var link = GetAttribute<LinkedField>(f);
                    Type t = GetMemberType(f);
                    retVal = new SearchFilter {
                        SearchValue = value,
                        ApplySearchCondition = GetValidationExpression(t),
                        SearchExpression = GetSearchExpression(GetTargetField(f), link.Type, value, link.EnumMethod)
                    }.Apply(retVal); //once the expressions are generated, go ahead and (try to) apply it
                }
            }
            catch (Exception ex) { throw (ErrorInfo = ex); }

            //Add the OrderBy if applicable
            return string.IsNullOrWhiteSpace(SortString) ? retVal
                                                         : retVal.OrderBy(SortString);
        }

        public static IEnumerable<MemberInfo> GetFieldsAndProperties(Type type)
            => type.GetFields().Cast<MemberInfo>()
                               .Concat(type.GetProperties());


        private static IEnumerable<TAttrib> GetAttributes<TAttrib>(MemberInfo member)
            => member.GetCustomAttributes(typeof(TAttrib))?.Cast<TAttrib>();

        private static TAttrib GetAttribute<TAttrib>(MemberInfo member)
            => GetAttributes<TAttrib>(member).FirstOrDefault();

        public static object GetMemberValue(MemberInfo field, object instance)
            => field.MemberType == MemberTypes.Property ? ((PropertyInfo)field).GetValue(instance) 
                                                        : ((FieldInfo)field).GetValue(instance);

        /// <summary>
        /// Represents a condition to be applied AND the logic to determine whether or not the condition
        /// will be applied (for example, by checking for null values).
        /// 
        /// One of these will be created for every search field defined in the inherited class
        /// </summary>
        private class SearchFilter
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
        private Expression<Func<T, bool>> GetSearchExpression(string targetField, ExpressionType comparison, object value, string enumMethod)
        {
            return (Expression<Func<T, bool>>)MakePredicate(DeQualifyFieldName(targetField, typeof(T)), comparison, value, enumMethod);
        }

        /// <summary>
        /// TODO: enumMethod string validation
        /// </summary>
        /// <param name="targetType"></param>
        /// <param name="memberNames"></param>
        /// <param name="index"></param>
        /// <param name="comparison"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private LambdaExpression MakePredicate(string[] memberNames, ExpressionType comparison, object value, string enumMethod = "Any")
        {
            //create parameter for inner lambda expression
            var parameter = Expression.Parameter(typeof(T), "t");
            Expression left = parameter;

            //Get the value against which the property/field will be compared
            var right = Expression.Constant(value);

            var currentType = typeof(T);
            for (int x = 0; x < memberNames.Count(); x++)
            {
                string memberName = memberNames[x];
                if (FieldExists(currentType, memberName))
                {
                    //assign the current type member type 
                    currentType = SingleLevelFieldType(currentType, memberName);
                    left = Expression.PropertyOrField(left ?? parameter, memberName);

                    //mini-loop for non collection objects
                    if (!currentType.IsGenericType || (!(currentType.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                                                         currentType.GetGenericTypeDefinition() == typeof(ICollection<>))))
                        continue;

                    ///Begin loop for collection objects -- this section can only run once

                    //get enum method
                    if (enumMethod.Length < 2) throw new Exception("Invalid enum method target.");
                    bool negateEnumMethod = enumMethod[0] == '!';
                    string methodName = negateEnumMethod ? enumMethod.Substring(1) : enumMethod;

                    //get the interface sub-type
                    var itemType = currentType.GetInterfaces()
                                              .Single(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                                              .GetGenericArguments()[0];

                    //generate lambda for single item
                    var itemPredicate = MakeSimplePredicate(itemType, memberNames[++x], comparison, value);

                    //get method call
                    var staticMethod = typeof(Enumerable).GetMember(methodName).OfType<MethodInfo>()
                                                         .Where(m => m.GetParameters().Length == 2)
                                                         .First()
                                                         .MakeGenericMethod(itemType);

                    //generate method call, then break loop for return
                    left = Expression.Call(null, staticMethod, left, itemPredicate);
                    right = Expression.Constant(!negateEnumMethod);
                    comparison = ExpressionType.Equal;
                    break;
                }
            }

            //build the final expression
            var binaryExpression = Expression.MakeBinary(comparison, left, right);
            return Expression.Lambda<Func<T, bool>>(binaryExpression, parameter);
        }

        static LambdaExpression MakeSimplePredicate(Type inputType, string memberName, ExpressionType comparison, object value)
        {
            var parameter = Expression.Parameter(inputType, "t");
            Expression left = Expression.PropertyOrField(parameter, memberName);
            return Expression.Lambda(Expression.MakeBinary(comparison, left, Expression.Constant(value)), parameter);
        }

        private static Type SingleLevelFieldType(Type baseType, string fieldName)
        {
            Type currentType = baseType;
            MemberInfo match = (MemberInfo)currentType.GetField(fieldName) ?? currentType.GetProperty(fieldName);
            return match == null ? null
                                 : GetMemberType(match);
        }

        /// <summary>
        /// Gets the type of a field or property.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="selectSingle">When true, if the item is a collection, returns the type of the underlying type.</param>
        /// <returns></returns>
        public static Type GetMemberType(MemberInfo field, bool selectSingle = false)
        {
            Type type = field.MemberType == MemberTypes.Property ? ((PropertyInfo)field).PropertyType
                                                                 : ((FieldInfo)field).FieldType;
            if (!selectSingle) return type;

            //look for collection and if it is, get the interface sub-type
            return type?.IsGenericType == true ? type.GetInterfaces().FirstOrDefault(t => t.IsGenericType)
                                                                     .GetGenericArguments()[0]
                                               : type;
        }

        /// <summary>
        /// Remove qualifying names from a target field.  For example, if targetField is "Order.Customer.Name" and
        /// targetType is Order, the de-qualified expression will be "Customer.Name" split into constituent parts
        /// </summary>
        /// <param name="targetField"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        public static string[] DeQualifyFieldName(string targetField, Type targetType) => DeQualifyFieldName(targetField.Split('.'), targetType);
        public static string[] DeQualifyFieldName(string[] targetFields, Type targetType)
        {
            var r = targetFields.ToList();
            foreach (var p in targetType.Name.Split('.'))
                if (r.First() == p) r.RemoveAt(0);
            return r.ToArray();
        }

        /// <summary>
        /// Based on the object type, provides the logic to determine whether the field should be searched. For example,
        /// if the item is of type int?, this returns that the item should not be used as a search parameter unless it
        /// is not null and has a value >= 0.
        /// 
        /// //TODO: allow validation regex to be specified for a field via attribute and figure out how to do this more
        ///         elegantly
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static Expression<Func<object, bool>> GetValidationExpression(Type type)
        {
            //throw exception for non-nullable types (strings are nullable, but is a reference type and thus have to be called out separately)
            if (type != typeof(string) && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                throw new Exception("Non-nullable types not supported.");

            //strings can't be blank, numbers can't be 0, and dates can't be minvalue
            if (type == typeof(string)) return t => !string.IsNullOrWhiteSpace((string)t);
            if (type == typeof(int?)) return t => t != null && (int)t >= 0;
            if (type == typeof(decimal?)) return t => t != null && (decimal)t >= decimal.Zero;
            if (type == typeof(DateTime?)) return t => t != null && (DateTime?)t != DateTime.MinValue;

            //everything else just can't be null
            return t => t != null;
        }

        #endregion

        #endregion
    }

    #region Attributes to define inherited class properties & fields

    /// <summary>
    /// Attribute that contains the target field/property to be compared against
    /// </summary>
    public class LinkedField : Attribute
    {
        const int EQUALS = 13;
        public string TargetField;
        public ExpressionType Type;
        public string EnumMethod;
        public LinkedField(string target, ExpressionType type = (ExpressionType)EQUALS, EnumerableMethod enumMethod = 0)
        {
            TargetField = target;
            Type = type;
            switch (enumMethod)
            {
                case EnumerableMethod.FirstOrDefault: EnumMethod = "FirstOrDefault"; break;
                case EnumerableMethod.None: EnumMethod = "!Any"; break;
                default:
                case EnumerableMethod.Any: EnumMethod = "Any"; break;
            }
        }
    }

    public enum EnumerableMethod
    {
        Any = 0,
        None,
        FirstOrDefault
    }

    public enum QualifierType
    {
        //OrderBy, 
        Last,
        First
    }

    #endregion

    #endregion
}