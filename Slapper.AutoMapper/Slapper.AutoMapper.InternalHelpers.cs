﻿/*  Slapper.AutoMapper v1.0.0.6 ( https://github.com/SlapperAutoMapper/Slapper.AutoMapper )

    MIT License:
   
    Copyright (c) 2016, Randy Burden ( http://randyburden.com ) and contributors. All rights reserved.
    All rights reserved.

    Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
    associated documentation files (the "Software"), to deal in the Software without restriction, including 
    without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
    copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the 
    following conditions:

    The above copyright notice and this permission notice shall be included in all copies or substantial 
    portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
    LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN 
    NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
    WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
    SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 

    Description:
    
    Slapper.AutoMapper maps dynamic data to static types. Slap your data into submission!
    
    Slapper.AutoMapper ( Pronounced Slapper-Dot-Automapper ) is a single file mapping library that can convert 
    dynamic data into static types and populate complex nested child objects.
    It primarily converts C# dynamics and IDictionary<string, object> to strongly typed objects and supports
    populating an entire object graph by using underscore notation to underscore into nested objects.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;

namespace Slapper
{
    public static partial class AutoMapper
    {
        #region Internal Helpers

        /// <summary>
        /// Contains the methods and members responsible for this libraries internal concerns.
        /// </summary>
        public static class InternalHelpers
        {
            /// <summary>
            /// Gets the identifiers for the given type. Returns NULL if not found.
            /// Results are cached for subsequent use and performance.
            /// </summary>
            /// <remarks>
            /// If no identifiers have been manually added, this method will attempt
            /// to first find an <see cref="Slapper.AutoMapper.Id"/> attribute on the <paramref name="type"/>
            /// and if not found will then try to match based upon any specified identifier conventions.
            /// </remarks>
            /// <param name="type">Type</param>
            /// <returns>Identifier</returns>
            public static IEnumerable<string> GetIdentifiers(Type type)
            {
                var typeMap = Cache.TypeMapCache.GetOrAdd(type, CreateTypeMap(type));

                return typeMap.Identifiers.Any() ? typeMap.Identifiers : null;
            }

            /// <summary>
            /// Get a Dictionary of a type's property names and field names and their corresponding PropertyInfo or FieldInfo.
            /// Results are cached for subsequent use and performance.
            /// </summary>
            /// <param name="type">Type</param>
            /// <returns>Dictionary of a type's property names and their corresponding PropertyInfo</returns>
            public static Dictionary<string, object> GetFieldsAndProperties(Type type)
            {
                var typeMap = Cache.TypeMapCache.GetOrAdd(type, CreateTypeMap(type));

                return typeMap.PropertiesAndFieldsInfo;
            }

            /// <summary>
            /// Creates an instance of the specified type using that type's default constructor.
            /// </summary>
            /// <param name="type">The type of object to create.</param>
            /// <returns>
            /// A reference to the newly created object.
            /// </returns>
            public static object CreateInstance(Type type)
            {
                return Activator.CreateInstance(type);
            }

            /// <summary>
            /// Creates a TypeMap for a given Type.
            /// </summary>
            /// <param name="type">Type</param>
            /// <returns>TypeMap</returns>
            public static Cache.TypeMap CreateTypeMap(Type type)
            {
                var conventionIdentifiers = Configuration.IdentifierConventions.Select(applyIdentifierConvention => applyIdentifierConvention(type)).ToList();

                var fieldsAndProperties = CreateFieldAndPropertyInfoDictionary(type);

                var identifiers = new List<string>();

                foreach (var fieldOrProperty in fieldsAndProperties)
                {
                    var memberName = fieldOrProperty.Key;

                    var member = fieldOrProperty.Value;

                    var fieldInfo = member as FieldInfo;

                    if (fieldInfo != null)
                    {
                        if (fieldInfo.GetCustomAttributes(Configuration.IdentifierAttributeType, false).Length > 0)
                        {
                            identifiers.Add(memberName);
                        }
                        else if (conventionIdentifiers.Exists(x => x.ToLower() == memberName.ToLower()))
                        {
                            identifiers.Add(memberName);
                        }
                    }
                    else
                    {
                        var propertyInfo = member as PropertyInfo;

                        if (propertyInfo != null)
                        {
                            if (propertyInfo.GetCustomAttributes(Configuration.IdentifierAttributeType, false).Length > 0)
                            {
                                identifiers.Add(memberName);
                            }
                            else if (conventionIdentifiers.Exists(x => x.ToLower() == memberName.ToLower()))
                            {
                                identifiers.Add(memberName);
                            }
                        }
                    }
                }

                var typeMap = new Cache.TypeMap(type, identifiers, fieldsAndProperties);

                return typeMap;
            }

            /// <summary>
            /// Creates a Dictionary of field or property names and their corresponding FieldInfo or PropertyInfo objects
            /// </summary>
            /// <param name="type">Type</param>
            /// <returns>Dictionary of member names and member info objects</returns>
            public static Dictionary<string, object> CreateFieldAndPropertyInfoDictionary(Type type)
            {
                var dictionary = new Dictionary<string, object>();

                var properties = type.GetProperties();

                foreach (var propertyInfo in properties)
                {
                    dictionary.Add(propertyInfo.Name, propertyInfo);
                }

                var fields = type.GetFields();

                foreach (var fieldInfo in fields)
                {
                    dictionary.Add(fieldInfo.Name, fieldInfo);
                }

                return dictionary;
            }

            /// <summary>
            /// Gets the Type of the Field or Property
            /// </summary>
            /// <param name="member">FieldInfo or PropertyInfo object</param>
            /// <returns>Type</returns>
            public static Type GetMemberType(object member)
            {
                Type type = null;

                var fieldInfo = member as FieldInfo;

                if (fieldInfo != null)
                {
                    type = fieldInfo.FieldType;
                }
                else
                {
                    var propertyInfo = member as PropertyInfo;

                    if (propertyInfo != null)
                    {
                        type = propertyInfo.PropertyType;
                    }
                }

                return type;
            }

            /// <summary>
            /// Sets the value on a Field or Property
            /// </summary>
            /// <param name="member">FieldInfo or PropertyInfo object</param>
            /// <param name="obj">Object to set the value on</param>
            /// <param name="value">Value</param>
            public static void SetMemberValue(object member, object obj, object value)
            {
                var fieldInfo = member as FieldInfo;

                if (fieldInfo != null)
                {
                    value = ConvertValuesTypeToMembersType(value, fieldInfo.Name, fieldInfo.FieldType, fieldInfo.DeclaringType);

                    try
                    {
                        fieldInfo.SetValue(obj, value);
                    }
                    catch (Exception e)
                    {
                        string errorMessage =
                            string.Format("{0}: An error occurred while mapping the value '{1}' of type {2} to the member name '{3}' of type {4} on the {5} class.",
                                           e.Message, value, value.GetType(), fieldInfo.Name, fieldInfo.FieldType, fieldInfo.DeclaringType);

                        throw new Exception(errorMessage, e);
                    }
                }
                else
                {
                    var propertyInfo = member as PropertyInfo;

                    if (propertyInfo != null)
                    {
                        value = ConvertValuesTypeToMembersType(value, propertyInfo.Name, propertyInfo.PropertyType, propertyInfo.DeclaringType);

                        try
                        {
                            propertyInfo.SetValue(obj, value, null);
                        }
                        catch (Exception e)
                        {
                            string errorMessage =
                                string.Format("{0}: An error occurred while mapping the value '{1}' of type {2} to the member name '{3}' of type {4} on the {5} class.",
                                               e.Message, value, value.GetType(), propertyInfo.Name, propertyInfo.PropertyType, propertyInfo.DeclaringType);

                            throw new Exception(errorMessage, e);
                        }
                    }
                }
            }

            /// <summary>
            /// Converts the values type to the members type if needed.
            /// </summary>
            /// <param name="value">Object value.</param>
            /// <param name="memberName">Member name.</param>
            /// <param name="memberType">Member type.</param>
            /// <param name="classType">Declarying class type.</param>
            /// <returns>Value converted to the same type as the member type.</returns>
            private static object ConvertValuesTypeToMembersType(object value, string memberName, Type memberType, Type classType)
            {
                if (value == null || value == DBNull.Value)
                    return null;

                var valueType = value.GetType();

                try
                {
                    if (valueType != memberType)
                    {
                        foreach (var typeConverter in Configuration.TypeConverters.OrderBy(x => x.Order))
                        {
                            if (typeConverter.CanConvert(value, memberType))
                            {
                                var convertedValue = typeConverter.Convert(value, memberType);

                                return convertedValue;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    string errorMessage = string.Format("{0}: An error occurred while mapping the value '{1}' of type {2} to the member name '{3}' of type {4} on the {5} class.",
                                                         e.Message, value, valueType, memberName, memberType, classType);

                    throw new Exception(errorMessage, e);
                }

                return value;
            }

            /// <summary>
            /// Gets the value of the member
            /// </summary>
            /// <param name="member">FieldInfo or PropertyInfo object</param>
            /// <param name="obj">Object to get the value from</param>
            /// <returns>Value of the member</returns>
            public static object GetMemberValue(object member, object obj)
            {
                object value = null;

                var fieldInfo = member as FieldInfo;

                if (fieldInfo != null)
                {
                    value = fieldInfo.GetValue(obj);
                }
                else
                {
                    var propertyInfo = member as PropertyInfo;

                    if (propertyInfo != null)
                    {
                        value = propertyInfo.GetValue(obj, null);
                    }
                }

                return value;
            }

            /// <summary>
            /// Gets a new or existing instance depending on whether an instance with the same identifiers already existing
            /// in the instance cache.
            /// </summary>
            /// <param name="type">Type of instance to get</param>
            /// <param name="properties">List of properties and values</param>
            /// <param name="parentHash">Hash from parent object</param>
            /// <returns>
            /// Tuple of bool, object, int where bool represents whether this is a newly created instance,
            /// object being an instance of the requested type and int being the instance's identifier hash.
            /// </returns>
            public static Tuple<bool, object, int> GetInstance(Type type, IDictionary<string, object> properties, int parentHash)
            {
                var instanceCache = Cache.GetInstanceCache();

                var identifiers = GetIdentifiers(type);

                object instance = null;

                bool isNewlyCreatedInstance = false;

                int identifierHash = 0;

                if (identifiers != null)
                {
                    foreach (var identifier in identifiers)
                    {
                        if (properties.ContainsKey(identifier))
                        {
                            var identifierValue = properties[identifier];
                            if (identifierValue != null)
                                identifierHash += identifierValue.GetHashCode() + type.GetHashCode() + parentHash;
                        }
                    }

                    if (identifierHash != 0)
                    {
                        if (instanceCache.ContainsKey(identifierHash))
                        {
                            instance = instanceCache[identifierHash];
                        }
                        else
                        {
                            instance = CreateInstance(type);

                            instanceCache.Add(identifierHash, instance);

                            isNewlyCreatedInstance = true;
                        }
                    }
                }

                // An identifier hash with a value of zero means the type does not have any identifiers.
                // To make this instance unique generate a unique hash for it.
                if (identifierHash == 0 && identifiers != null) identifierHash = type.GetHashCode() + parentHash;

                if (instance == null)
                {
                    instance = CreateInstance(type);
                    identifierHash = Guid.NewGuid().GetHashCode();

                    isNewlyCreatedInstance = true;
                }

                return new Tuple<bool, object, int>(isNewlyCreatedInstance, instance, identifierHash);
            }

            /// <summary>
            /// Populates the given instance's properties where the IDictionary key property names
            /// match the type's property names case insensitively.
            /// 
            /// Population of complex nested child properties is supported by underscoring "_" into the
            /// nested child properties in the property name.
            /// </summary>
            /// <param name="dictionary">Dictionary of property names and values</param>
            /// <param name="instance">Instance to populate</param>
            /// <param name="parentInstance">Optional parent instance of the instance being populated</param>
            /// <returns>Populated instance</returns>
            public static object Map(IDictionary<string, object> dictionary, object instance, object parentInstance = null)
            {
                var fieldsAndProperties = GetFieldsAndProperties(instance.GetType());

                foreach (var fieldOrProperty in fieldsAndProperties)
                {
                    var memberNameSingle = fieldOrProperty.Key.ToLower();
                    var instanceName = instance.GetType().Name.ToLower();

                    var memberNames = new List<string>() {
                        memberNameSingle,
                        String.Format("{0}_{1}", instanceName, memberNameSingle) };

                    var member = fieldOrProperty.Value;

                    object value;

                    // Handle populating simple members on the current type
                    foreach (var memberName in memberNames)
                    {
                        if (dictionary.TryGetValue(memberName, out value))
                        {
                            SetMemberValue(member, instance, value);
                        }
                        else
                        {
                            Type memberType = GetMemberType(member);

                            // Handle populating complex members on the current type
                            if (memberType.IsClass || memberType.IsInterface)
                            {
                                // Try to find any keys that start with the current member name
                                var nestedDictionary = dictionary.Where(x => x.Key.ToLower().StartsWith(memberName + "_")).ToList();

                                // If there weren't any keys
                                if (!nestedDictionary.Any())
                                {
                                    // And the parent instance was not null
                                    if (parentInstance != null)
                                    {
                                        // And the parent instance is of the same type as the current member
                                        if (parentInstance.GetType() == memberType)
                                        {
                                            // Then this must be a 'parent' to the current type
                                            SetMemberValue(member, instance, parentInstance);
                                        }
                                    }

                                    continue;
                                }

                                var newDictionary = nestedDictionary.ToDictionary(pair => pair.Key.ToLower()
                                                                                              .Replace(memberName + "_", string.Empty), pair => pair.Value,
                                                                                   StringComparer.OrdinalIgnoreCase);

                                // Try to get the value of the complex member. If the member
                                // hasn't been initialized, then this will return null.
                                object nestedInstance = GetMemberValue(member, instance);

                                // If the member is null and is a class, try to create an instance of the type
                                if (nestedInstance == null && memberType.IsClass)
                                {
                                    if (memberType.IsArray)
                                    {
                                        nestedInstance = new ArrayList().ToArray(memberType.GetElementType());
                                    }
                                    else
                                    {
                                        nestedInstance = CreateInstance(memberType);
                                    }
                                }

                                Type genericCollectionType = typeof(IEnumerable<>);

                                if (memberType.IsGenericType && genericCollectionType.IsAssignableFrom(memberType.GetGenericTypeDefinition())
                                     || memberType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == genericCollectionType))
                                {
                                    var innerType = memberType.GetGenericArguments().FirstOrDefault();

                                    if (innerType == null)
                                    {
                                        innerType = memberType.GetElementType();
                                    }

                                    nestedInstance = MapCollection(innerType, newDictionary, nestedInstance, instance);
                                }
                                else
                                {
                                    nestedInstance = Map(newDictionary, nestedInstance, instance);
                                }

                                SetMemberValue(member, instance, nestedInstance);
                            }
                        }
                    }
                }

                return instance;
            }

            /// <summary>
            /// Populates the given instance's properties where the IDictionary key property names
            /// match the type's property names case insensitively.
            /// 
            /// Population of complex nested child properties is supported by underscoring "_" into the
            /// nested child properties in the property name.
            /// </summary>
            /// <param name="type">Underlying instance type</param>
            /// <param name="dictionary">Dictionary of property names and values</param>
            /// <param name="instance">Instance to populate</param>
            /// <param name="parentInstance">Optional parent instance of the instance being populated</param>
            /// <returns>Populated instance</returns>
            public static object MapCollection(Type type, IDictionary<string, object> dictionary, object instance, object parentInstance = null)
            {
                Type baseListType = typeof(List<>);

                Type listType = baseListType.MakeGenericType(type);

                if (instance == null)
                {
                    instance = CreateInstance(listType);
                }

                // If the dictionnary only contains null values, we return an empty instance
                if (dictionary.Values.FirstOrDefault(v => v != null) == null)
                {
                    return instance;
                }

                var getInstanceResult = GetInstance(type, dictionary, parentInstance == null ? 0 : parentInstance.GetHashCode());

                // Is this a newly created instance? If false, then this item was retrieved from the instance cache.
                bool isNewlyCreatedInstance = getInstanceResult.Item1;

                bool isArray = instance.GetType().IsArray;

                object instanceToAddToCollectionInstance = getInstanceResult.Item2;

                instanceToAddToCollectionInstance = Map(dictionary, instanceToAddToCollectionInstance, parentInstance);

                if (isNewlyCreatedInstance)
                {
                    if (isArray)
                    {
                        var arrayList = new ArrayList { instanceToAddToCollectionInstance };

                        instance = arrayList.ToArray(type);
                    }
                    else
                    {
                        MethodInfo addMethod = listType.GetMethod("Add");

                        addMethod.Invoke(instance, new[] { instanceToAddToCollectionInstance });
                    }
                }
                else
                {
                    MethodInfo containsMethod = listType.GetMethod("Contains");

                    var alreadyContainsInstance = (bool)containsMethod.Invoke(instance, new[] { instanceToAddToCollectionInstance });

                    if (alreadyContainsInstance == false)
                    {
                        if (isArray)
                        {
                            var arrayList = new ArrayList((ICollection)instance);

                            instance = arrayList.ToArray(type);
                        }
                        else
                        {
                            MethodInfo addMethod = listType.GetMethod("Add");

                            addMethod.Invoke(instance, new[] { instanceToAddToCollectionInstance });
                        }
                    }
                }

                return instance;
            }

            /// <summary>
            /// Provides a means of getting/storing data in the host application's
            /// appropriate context.
            /// </summary>
            public interface IContextStorage
            {
                /// <summary>
                /// Get a stored item.
                /// </summary>
                /// <typeparam name="T">Object type</typeparam>
                /// <param name="key">Item key</param>
                /// <returns>Reference to the requested object</returns>
                T Get<T>(string key);

                /// <summary>
                /// Stores an item.
                /// </summary>
                /// <param name="key">Item key</param>
                /// <param name="obj">Object to store</param>
                void Store(string key, object obj);

                /// <summary>
                /// Removes an item.
                /// </summary>
                /// <param name="key">Item key</param>
                void Remove(string key);
            }

            /// <summary>
            /// Provides a means of getting/storing data in the host application's
            /// appropriate context.
            /// </summary>
            /// <remarks>
            /// For ASP.NET applications, it will store in the data in the current HTTPContext.
            /// For all other applications, it will store the data in the logical call context.
            /// </remarks>
            public class InternalContextStorage : IContextStorage
            {
                /// <summary>
                /// Get a stored item.
                /// </summary>
                /// <typeparam name="T">Object type</typeparam>
                /// <param name="key">Item key</param>
                /// <returns>Reference to the requested object</returns>
                public T Get<T>(string key)
                {
                    try
                    {
                        if (ReflectionHelper.HttpContext.GetCurrentHttpContext() == null)
                        {
                            return (T)CallContext.LogicalGetData(key);
                        }

                        return ReflectionHelper.HttpContext.GetItemFromHttpContext<T>(key);
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Log(Logging.LogLevel.Error, ex, "An error occurred in ContextStorage.Get() retrieving key: {0} for type: {1}.", key, typeof(T));
                    }

                    return default(T);
                }

                /// <summary>
                /// Stores an item.
                /// </summary>
                /// <param name="key">Item key</param>
                /// <param name="obj">Object to store</param>
                public void Store(string key, object obj)
                {
                    if (ReflectionHelper.HttpContext.GetCurrentHttpContext() == null)
                    {
                        CallContext.LogicalSetData(key, obj);
                    }
                    else
                    {
                        ReflectionHelper.HttpContext.StoreItemInHttpContext(key, obj);
                    }
                }

                /// <summary>
                /// Removes an item.
                /// </summary>
                /// <param name="key">Item key</param>
                public void Remove(string key)
                {
                    if (ReflectionHelper.HttpContext.GetCurrentHttpContext() == null)
                    {
                        CallContext.FreeNamedDataSlot(key);
                    }
                    else
                    {
                        ReflectionHelper.HttpContext.RemoveItemFromHttpContext(key);
                    }
                }
            }

            /// <summary>
            /// Provides a means of getting/storing data in the host application's
            /// appropriate context.
            /// </summary>
            /// <remarks>
            /// For ASP.NET applications, it will store in the data in the current HTTPContext.
            /// For all other applications, it will store the data in the logical call context.
            /// </remarks>
            public static class ContextStorage
            {
                /// <summary>
                /// Provides a means of getting/storing data in the host application's
                /// appropriate context.
                /// </summary>
                public static IContextStorage ContextStorageImplementation { get; set; }

                static ContextStorage()
                {
                    ContextStorageImplementation = new InternalContextStorage();
                }

                /// <summary>
                /// Get a stored item.
                /// </summary>
                /// <typeparam name="T">Object type</typeparam>
                /// <param name="key">Item key</param>
                /// <returns>Reference to the requested object</returns>
                public static T Get<T>(string key)
                {
                    return ContextStorageImplementation.Get<T>(key);
                }

                /// <summary>
                /// Stores an item.
                /// </summary>
                /// <param name="key">Item key</param>
                /// <param name="obj">Object to store</param>
                public static void Store(string key, object obj)
                {
                    ContextStorageImplementation.Store(key, obj);
                }

                /// <summary>
                /// Removes an item.
                /// </summary>
                /// <param name="key">Item key</param>
                public static void Remove(string key)
                {
                    ContextStorageImplementation.Remove(key);
                }
            }

            /// <summary>
            /// Contains the methods and members responsible for this libraries reflection concerns.
            /// </summary>
            public static class ReflectionHelper
            {
                /// <summary>
                /// Provides access to System.Web.HttpContext.Current.Items via reflection.
                /// </summary>
                public static class HttpContext
                {
                    /// <summary>
                    /// Attempts to load and cache System.Web.HttpContext.Current.Items.
                    /// </summary>
                    static HttpContext()
                    {
                        try
                        {
                            SystemDotWeb = Assembly
                                .Load("System.Web, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

                            if (SystemDotWeb == null) return;

                            SystemDotWebDotHttpContext = SystemDotWeb.GetType("System.Web.HttpContext", false, true);

                            if (SystemDotWebDotHttpContext == null) return;

                            // Get the current HTTP context property info
                            CurrentHttpContextPropertyInfo = SystemDotWebDotHttpContext
                                .GetProperty("Current", (BindingFlags.Public | BindingFlags.Static));

                            // Get the property info for the requested property
                            ItemsPropertyInfo = SystemDotWebDotHttpContext
                                .GetProperty("Items", (BindingFlags.Public | BindingFlags.Instance));
                        }
                        catch (Exception ex)
                        {
                            Logging.Logger.Log(Logging.LogLevel.Error, ex, "An error occurred attempting to get the current HttpContext.");
                        }
                    }

                    /// <summary>
                    /// System.Web assembly reference.
                    /// </summary>
                    public static readonly Assembly SystemDotWeb;

                    /// <summary>
                    /// System.Web.HttpContext type reference.
                    /// </summary>
                    public static readonly Type SystemDotWebDotHttpContext;

                    /// <summary>
                    /// System.Web.HttpContext.Current PropertyInfo reference.
                    /// </summary>
                    public static readonly PropertyInfo CurrentHttpContextPropertyInfo;

                    /// <summary>
                    /// System.Web.HttpContext.Current.Items PropertyInfo reference.
                    /// </summary>
                    public static readonly PropertyInfo ItemsPropertyInfo;

                    /// <summary>
                    /// Retrieves an item of type <typeparamref name="T"/> from the current HttpContext.
                    /// </summary>
                    /// <remarks>
                    /// This is functionally equivalent to:
                    /// T obj = ( T ) System.Web.HttpContext.Current.Items[ "SomeKeyName" ];
                    /// </remarks>
                    /// <typeparam name="T">Type requested</typeparam>
                    /// <param name="key">Key name</param>
                    /// <returns>Requested item</returns>
                    public static T GetItemFromHttpContext<T>(string key)
                    {
                        if (SystemDotWeb != null && SystemDotWebDotHttpContext != null
                             && CurrentHttpContextPropertyInfo != null && ItemsPropertyInfo != null)
                        {
                            // Get a reference to the current HTTP context
                            object currentHttpContext = CurrentHttpContextPropertyInfo.GetValue(null, null);

                            if (currentHttpContext != null)
                            {
                                var items = ItemsPropertyInfo.GetValue(currentHttpContext, null) as IDictionary;

                                if (items != null)
                                {
                                    object value = items[key];

                                    if (value != null)
                                    {
                                        return (T)value;
                                    }
                                }
                            }
                        }

                        return default(T);
                    }

                    /// <summary>
                    /// Stores an item in the current HttpContext.
                    /// </summary>
                    /// <param name="key">Item key</param>
                    /// <param name="value">Item value</param>
                    public static void StoreItemInHttpContext(object key, object value)
                    {
                        object currentHttpContext = GetCurrentHttpContext();

                        if (currentHttpContext != null)
                        {
                            var items = ItemsPropertyInfo.GetValue(currentHttpContext, null) as IDictionary;

                            if (items != null)
                            {
                                items.Add(key, value);
                            }
                        }
                    }

                    /// <summary>
                    /// Removes an item from the current HttpContext.
                    /// </summary>
                    /// <param name="key">Item key</param>
                    public static void RemoveItemFromHttpContext(object key)
                    {
                        object currentHttpContext = GetCurrentHttpContext();

                        if (currentHttpContext != null)
                        {
                            var items = ItemsPropertyInfo.GetValue(currentHttpContext, null) as IDictionary;

                            if (items != null)
                            {
                                items.Remove(key);
                            }
                        }
                    }

                    /// <summary>
                    /// Gets the current HttpContext.
                    /// </summary>
                    /// <returns>Reference to the current HttpContext.</returns>
                    public static object GetCurrentHttpContext()
                    {
                        if (SystemDotWeb != null && SystemDotWebDotHttpContext != null
                             && CurrentHttpContextPropertyInfo != null && ItemsPropertyInfo != null)
                        {
                            // Get a reference to the current HTTP context
                            object currentHttpContext = CurrentHttpContextPropertyInfo.GetValue(null, null);

                            return currentHttpContext;
                        }

                        return null;
                    }
                }
            }
        }

        #endregion Internal Helpers
    }
}
