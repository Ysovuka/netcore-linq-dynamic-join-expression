using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Linq.Example.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var itemList = new List<Item>()
            {
                new Item(){ Id = 0, Description = "Test" }
            };

            var itemEffectList = new List<ItemEffect>()
            {
                new ItemEffect() { Id = 0, ItemId = 0, Effect = "Doomsday" }
            };

            var q = itemList.Join(itemEffectList,
                            CreateSelector<Item>("Id").Compile(),
                            CreateSelector<ItemEffect>("ItemId").Compile(),
                            (entity, acl) => new { Entity = entity, ACL = acl });

            Assert.IsTrue(q.Count() == 1);
        }

        private Expression<Func<T, int>> CreateSelector<T>(string field)
        {
            var itemParam = Expression.Parameter(typeof(T));

            // obj.Field
            var entityAccess = Expression.MakeMemberAccess(itemParam, typeof(T).GetMember(field).AsEnumerable().First());
            return Expression.Lambda<Func<T, int>>(entityAccess, itemParam);
        }

        [TestMethod]
        public void TestMethod2()
        {
            var expression = Extensions.Create<ItemEffect>(ExpressionType.NotEqual, "Effect", "");

            Assert.IsTrue(expression.Compile()(new ItemEffect { Effect = "No Empty" }));
        }


        [TestMethod]
        public void TestMethod3()
        {
            var itemList = new List<Item>()
            {
                new Item(){ Id = 0, Description = "Test" }
            };


            var expandoList = itemList.ToList();

            Assert.IsFalse(expandoList.Count == 0);
        }

        [TestMethod]
        public void TestMethod4()
        {
            try
            {
                using (var context = new ItemDbContext((new DbContextOptionsBuilder<ItemDbContext>()
                    .UseInMemoryDatabase("ItemDbContext")).Options))
                {
                    context.Add(new Item() { Id = 0, Description = "Test" });
                    context.SaveChanges();

                    var expandoQuery = context.Items.Select(new string[] { "Description" });

                    var expandoList = expandoQuery.ToList();

                    Assert.IsFalse(expandoList.Count == 0);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }


    public static class Extensions
    {
        private static IEnumerable<KeyValuePair<ConstantExpression, Expression>> GetPropertyExpressionsWithAllProperties(this ParameterExpression parameterExpression)
        {
            IEnumerable<string> propertiesToProvide = parameterExpression.Type.GetProperties().Select(r => r.Name);
            
            return parameterExpression.GetPropertyExpressionsWithOnlyProvidedProperties(propertiesToProvide);
        }

        private static IEnumerable<KeyValuePair<ConstantExpression, Expression>> GetPropertyExpressionsWithOnlyProvidedProperties(this ParameterExpression parameterExpression, IEnumerable<string> providedProperties)
        {
            // Iterate through all properties for the source parameter expression type.
            var memberExpressions = parameterExpression.Type.GetProperties().AsEnumerable()
                .Where(property => providedProperties.Contains(property.Name))
                .Select(property =>
                {
                    Debug.WriteLine($"Property: {property.Name}");

                    // Create the constant expression used to denote the property name.
                    var constantExpression = Expression.Constant(property.Name);
                    Debug.WriteLine($"Constant Expression: {constantExpression.ToString()}");

                    // Create the property expression from the source paramter expression.
                    var propertyExpression = Expression.Property(parameterExpression, property);
                    Debug.WriteLine($"Property Expression: {propertyExpression.ToString()}");

                    // Convert the property type to object to prevent errors.
                    var convertExpression = Expression.Convert(propertyExpression, typeof(object));
                    Debug.WriteLine($"Convert Expression: {convertExpression.ToString()}");

                    // Append the expressions to the list.
                    return new KeyValuePair<ConstantExpression, Expression>(constantExpression, convertExpression);
                }).ToList();

            return memberExpressions;
        }

        public static Expression GetAssignedTypeExpression(this ParameterExpression parameterExpression)
        {
            // Create new result object for usage.
            NewExpression newResultExpression = Expression.New(parameterExpression.Type);
            Debug.WriteLine($"New Result Expression: {newResultExpression.ToString()}");

            // Create assignment to result object.
            var resultAssignmentExpression = Expression.Assign(parameterExpression, newResultExpression);
            Debug.WriteLine($"Result Assignment Expression: {resultAssignmentExpression.ToString()}");

            return resultAssignmentExpression;
        }

        public static IEnumerable<Expression> GetReturnExpressions(this ParameterExpression parameterExpression)
        {
            // Type used by the return expressions.
            var returnType = parameterExpression.Type;
            Debug.WriteLine($"Return Type: {returnType.ToString()}");

            // Create the return target from the return type.
            var returnTarget = Expression.Label(returnType, "returnTarget");
            Debug.WriteLine($"Return Target: {returnTarget.ToString()}");

            // Create the return expression based on the parameter expression and return type.
            var returnExpression = Expression.Return(returnTarget, parameterExpression, returnType);
            Debug.WriteLine($"Return Expression: {returnExpression.ToString()}");

            // Create the return label based on the return target, passing in default if null.
            var returnLabel = Expression.Label(returnTarget, Expression.Default(returnType));
            Debug.WriteLine($"Return Label: {returnLabel.ToString()}");

            return new Expression[] { returnExpression, returnLabel };
        }

        public static  IEnumerable<MethodCallExpression> GetMethodCallsFromPropertyExpressions(this ParameterExpression parameterExpression, IEnumerable<KeyValuePair<ConstantExpression, Expression>> propertyExpressions)
        {
            var methodInfo = typeof(IDictionary<string, object>).GetMethod("Add");

            // Create the conversion used by the method call expression.
            var conversionExpression = Expression.Convert(parameterExpression, typeof(IDictionary<string, object>));
            Debug.WriteLine($"Conversion Expression: {conversionExpression.ToString()}");

            var methodCallExpressions = propertyExpressions.Select(pair =>
                                        {
                                            // Create the method call from the method information.
                                            var methodCallExpression = Expression.Call(
                                                    conversionExpression,
                                                    methodInfo,
                                                    pair.Key,
                                                    pair.Value);
                                            Debug.WriteLine($"Method Call: {methodCallExpression.ToString()}");

                                            return methodCallExpression;
                                        }).ToList();

            return methodCallExpressions;
        }

        public static IQueryable<ExpandoObject> Select<TSource>(this IQueryable<TSource> source, IEnumerable<string> providedProperties)
        {
            // Create parameter expression for source type.
            ParameterExpression sourceParameterExpression = Expression.Parameter(typeof(TSource));
            Debug.WriteLine($"Source Parameter Expression: {sourceParameterExpression.ToString()}");

            // Create parameter expression for result type.
            ParameterExpression resultParameterExpression = Expression.Parameter(typeof(ExpandoObject));
            Debug.WriteLine($"Result Parameter Expression: {resultParameterExpression.ToString()}");

            // Create assigned variable for result type.
            var resultAssignmentExpression = resultParameterExpression.GetAssignedTypeExpression();

            // Create property expressions used in method calls.
            var propertyExpressions = sourceParameterExpression
                                            .GetPropertyExpressionsWithOnlyProvidedProperties(providedProperties);

            // Create method calls used in block expression.
            var methodCallExpressions = resultParameterExpression
                                            .GetMethodCallsFromPropertyExpressions(propertyExpressions);
                                            
            // Create return expressions used in block expression.
            var returnExpressions = resultParameterExpression.GetReturnExpressions();

            var beginningExpressions = new Expression[]
                {
                    resultAssignmentExpression,
                };

            var blockExpressions = beginningExpressions
                        .Concat(methodCallExpressions)
                        .Concat(returnExpressions);

            var blockExpression = Expression.Block(
                new[] { resultParameterExpression },
                blockExpressions);

            var lambdaExpression = Expression.Lambda<Func<TSource, ExpandoObject>>(
                blockExpression,
                sourceParameterExpression
                );

            return source.Select(r => lambdaExpression.Compile()(r));
        }

        public static IQueryable<TResult> Select<TSource, TResult>(this IQueryable<TSource> source, IEnumerable<string> providedProperties)
        {
            var results = source.Select(providedProperties)
                .Select(r => JsonConvert.DeserializeObject<TResult>(JsonConvert.SerializeObject(r)));

            return results;
        }

        /// <summary>
        /// Extension method that turns a dictionary of string and object to an ExpandoObject
        /// </summary>
        public static ExpandoObject ToExpando(this IDictionary<string, object> dictionary)
        {
            var expando = new ExpandoObject();
            var expandoDic = (IDictionary<string, object>)expando;

            // go through the items in the dictionary and copy over the key value pairs)
            foreach (var kvp in dictionary)
            {
                // if the value can also be turned into an ExpandoObject, then do it!
                if (kvp.Value is IDictionary<string, object>)
                {
                    var expandoValue = ((IDictionary<string, object>)kvp.Value).ToExpando();
                    expandoDic.Add(kvp.Key, expandoValue);
                }
                else if (kvp.Value is ICollection)
                {
                    // iterate through the collection and convert any strin-object dictionaries
                    // along the way into expando objects
                    var itemList = new List<object>();
                    foreach (var item in (ICollection)kvp.Value)
                    {
                        if (item is IDictionary<string, object>)
                        {
                            var expandoItem = ((IDictionary<string, object>)item).ToExpando();
                            itemList.Add(expandoItem);
                        }
                        else
                        {
                            itemList.Add(item);
                        }
                    }

                    expandoDic.Add(kvp.Key, itemList);
                }
                else
                {
                    expandoDic.Add(kvp);
                }
            }

            return expando;
        }

        public static IEnumerable<TResult> Cast<TSource, TResult>(this IEnumerable<TSource> query,
            IDictionary<string, string> mappedVariables = default(IDictionary<string, string>))
        {
            List<Expression> propertyExpressions = new List<Expression>();
            List<MemberAssignment> assignmentExpressions = new List<MemberAssignment>();

            // Get type of source.
            Type sourceType = typeof(TSource);

            // Get type of result.
            Type resultType = typeof(TResult);

            // Get ctor of result type.
            NewExpression ctorExpression = Expression.New(resultType);

            // Create parameter used in expression.
            ParameterExpression parameterExpression = Expression.Parameter(sourceType);

            if (mappedVariables != null)
            {
                foreach (var pair in mappedVariables)
                {
                    // Get source property.
                    PropertyInfo sourceProperty = sourceType.GetProperty(pair.Key);

                    // Get result property.
                    PropertyInfo resultProperty = resultType.GetProperty(pair.Value);

                    // Create source property used in the expression.
                    MemberExpression sourcePropertyExpression = Expression.Property(parameterExpression, sourceProperty);

                    // Create result assignment used in expression.
                    MemberAssignment resultAssignmentExpression = Expression.Bind(resultProperty, sourcePropertyExpression);

                    // Add source property to property expressions list.
                    propertyExpressions.Add(sourcePropertyExpression);

                    // Add result assignment to assignment expressions list.
                    assignmentExpressions.Add(resultAssignmentExpression);
                }
            }

            foreach (var sourceProperty in sourceType.GetProperties())
            {
                if (resultType.GetProperty(sourceProperty.Name) != null)
                {
                    // Get result property.
                    PropertyInfo resultProperty = resultType.GetProperty(sourceProperty.Name);

                    // Get source property used in the expression.
                    MemberExpression sourcePropertyExpression = Expression.Property(parameterExpression, sourceProperty);

                    // Create result assignment used in expression.
                    MemberAssignment resultAssignmentExpression = Expression.Bind(resultProperty, sourcePropertyExpression);

                    // Add source property to property expressions list.
                    propertyExpressions.Add(sourcePropertyExpression);

                    // Add result assignment to assignment expressions list.
                    assignmentExpressions.Add(resultAssignmentExpression);

                }
            }

            // Create member initialization used in the expression.
            MemberInitExpression memberInitializeExpression = Expression.MemberInit(ctorExpression, assignmentExpressions);

            // Create selector used by the function.
            Expression<Func<TSource, TResult>> selectorExpression = Expression.Lambda<Func<TSource, TResult>>(memberInitializeExpression, parameterExpression);

            return query.Select(selectorExpression.Compile());
        }

        public static object GetDefault(this Type targetType)
            => targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        public static MemberExpression GetPropertyExpression(this ParameterExpression parameterExpression, string propertyName)
        {
            // Get source type from parameter expression.
            Type sourceType = parameterExpression.Type;
            Debug.WriteLine($"Source Type: {sourceType.ToString()}");

            // Get property information from source type.
            PropertyInfo sourceProperty = sourceType.GetProperty(propertyName);

            // Create member used in expression;
            MemberExpression propertyExpression = Expression.Property(parameterExpression, sourceProperty);
            Debug.WriteLine($"Property Expression: {propertyExpression.ToString()}");

            return propertyExpression;
        }

        public static Expression<Func<TSource, bool>> Create<TSource>(ExpressionType expressionType, string propertyName, object value)
        {
            // Validate property name is not null or empty; cannot continue if null or empty.
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException("propertyName", "Unable to continue as the property name parameter is either null or empty.");

            // Get source type from generic type.
            Type sourceType = typeof(TSource);

            // Create parameter used in expression.
            ParameterExpression parameterExpression = Expression.Parameter(sourceType);

            // Create member used in expression.
            MemberExpression memberExpression = parameterExpression.GetPropertyExpression(propertyName);

            // Create constant used in expression.
            Expression valueExpression = Expression.Constant(value ?? sourceType.GetDefault());

            // Convert constant to property type, if not null.
            if (value != null && value.GetType() != memberExpression.Type)
                valueExpression = Expression.Convert(valueExpression, memberExpression.Type);

            Expression finalExpression = Expression.MakeBinary(expressionType, valueExpression, memberExpression);

            // Return final lambda for usage.
            return Expression.Lambda<Func<TSource, bool>>(finalExpression, parameterExpression);
        }

        public static Expression<Func<T, bool>> InvokeMethod<T>(string propertyName, string methodName, params object[] args)
        {
            List<ConstantExpression> argumentExpressions = new List<ConstantExpression>();
            foreach(var arg in args)
            {
                argumentExpressions.Add(Expression.Constant(arg));
            }
            
            ParameterExpression parameterExpression = Expression.Parameter(typeof(T));

            Expression memberExpression = Expression.Property(parameterExpression, propertyName);

            MethodInfo methodInfo = typeof(T).GetMethod(methodName);

            MethodCallExpression methodCallExpression = Expression.Call(memberExpression, methodInfo, argumentExpressions);

            return Expression.Lambda<Func<T, bool>>(methodCallExpression, parameterExpression);
        }  
    }

    public class ItemDbContext : DbContext
    {
        public ItemDbContext(DbContextOptions<ItemDbContext> options)
            : base(options)
        {

        }

        public DbSet<Item> Items { get; set; }
        public DbSet<ItemEffect> ItemEffects { get; set; }
    }

    public class Item
    {
        [Key]
        public int Id { get; set; }
        public string Description { get; set; }
    }

    public class ItemEffect
    {
        [Key]
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string Effect { get; set; }
    }
}
