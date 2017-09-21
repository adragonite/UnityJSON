using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityJSON
{
	/// <summary>
	/// An exception during the deserialization process.
	/// </summary>
	public class DeserializationException : Exception
	{
		public DeserializationException () : base ()
		{
		}

		public DeserializationException (string message) : base (message)
		{
		}
	}

	/// <summary>
	/// Deserializes JSON string either into newly instantiated or
	/// previously existing objects.
	/// </summary>
	public class Deserializer
	{
		private static Deserializer _default = new Deserializer ();
		private static Deserializer _simple = _default;

		/// <summary>
		/// The default deserializer to be used when no deserializer is given.
		/// You can set this to your own default deserializer. Uses the
		/// #Simple deserializer by default.
		/// </summary>
		public static Deserializer Default {
			get { return _default; }
			set {
				if (value == null) {
					throw new ArgumentNullException ("default deserializer");
				}
				_simple = value;
			}
		}

		/// <summary>
		/// The initial deserializer that is provided by the framework.
		/// </summary>
		public static Deserializer Simple {
			get { return _simple; }
		}

		/// <summary>
		/// Tries to instantiate an object of a given type. This will be called
		/// for all custom objects before trying to instantiate the object with a
		/// default constructor.
		/// 
		/// Subclasses should override this method to provide instantiated versions
		/// for classes with constructors with arguments or interfaces or abstract
		/// classes. The JSON node can be used to decide which class or struct to
		/// instantiate.
		/// </summary>
		protected virtual bool TryInstantiate (
			JSONNode node, 
			Type type,
			NodeOptions options,
			out object instantiatedObject)
		{
			instantiatedObject = null;
			return false;
		}

		/// <summary>
		/// Tries to deserialize the JSON node onto the given object. It is guaranteed
		/// that the object is not null. This will be called before trying any other
		/// deserialization method. Subclasses should override this method to perform
		/// their own deserialization logic.
		/// </summary>
		protected virtual bool TryDeserializeOn (
			object obj,
			JSONNode node,
			NodeOptions options)
		{
			return false;
		}

		/// <summary>
		/// Deserializes the JSON string directly on the object. Throws an
		/// ArgumentNullException of the object is <c>null</c>.
		/// </summary>
		public void DeserializeOn (
			object obj, 
			JSONNode node, 
			NodeOptions options = NodeOptions.Default)
		{
			if (obj == null) {
				throw new ArgumentNullException ("obj");
			}

			if (TryDeserializeOn (obj, node, options)) {
				return;
			}

			Type type = obj.GetType ();
			if (type.IsEnum) {
				throw new ArgumentException ("Cannot deserialize on enums.");
			} else if (type.IsPrimitive) {
				throw new ArgumentException ("Cannot deserialize on primitive types.");
			} else if (!node.IsObject) {
				throw new ArgumentException ("Expected a JSON object, found " + node.Tag);
			} 

			_FeedCustom (obj, node, options);
		}

		/// <summary>
		/// Deserializes the JSON node to a new object of the requested type. This
		/// will first call #TryInstantiate to create an object for the type, then
		/// try the default constructor without arguments. If an object can be
		/// instantiated, then first the IDeserializable.Deserialize method will
		/// be used if the object implements the interface. If not, the framework
		/// deserialization will be performed.
		/// </summary>
		/// <param name="node">JSON node to deserialize.</param>
		/// <param name="type">Requested type of the deserialized object.</param>
		/// <param name="options">Deserialization options for the node (optional).</param>
		public object Deserialize (
			JSONNode node, 
			Type type, 
			NodeOptions options = NodeOptions.Default)
		{
			return _Deserialize (node, type, options, ObjectTypes.JSON, null);
		}

		/// <summary>
		/// Deserializes a JSON node into a C# System.Object type. If no restrictions
		/// are given, the deserialized types can be doubles, booleans, strings, and arrays
		/// and dictionaries thereof. Restricted types can allow custom types to create
		/// classes or structs instead of dictionaries.
		/// </summary>
		/// <param name="node">JSON node to deserialize.</param>
		/// <param name="restrictedTypes">Restricted types for the object.</param>
		/// <param name="customTypes">Allowed custom types for the object. Restrictions
		/// must allow custom types if not <c>null</c>.</param>
		/// <param name="options">Deserialization options.</param>
		public object DeserializeToObject (
			JSONNode node,
			ObjectTypes restrictedTypes = ObjectTypes.JSON,
			Type[] customTypes = null,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null || node.IsNull) {
				return null;
			}
			if (customTypes != null) {
				if (!restrictedTypes.SupportsCustom ()) {
					throw new ArgumentException ("Restrictions do not allow custom types.");
				}
				foreach (Type type in customTypes) {
					if (!Util.IsCustomType (type)) {
						throw new ArgumentException ("Unsupported custom type: " + type);
					}
				}
			}
			return _DeserializeToObject (node, options, restrictedTypes, customTypes);
		}

		/// <summary>
		/// Deserializes a JSON node into a System.Nullable object.
		/// </summary>
		public Nullable<T> DeserializeToNullable<T> (
			JSONNode node,
			NodeOptions options = NodeOptions.Default) where T : struct
		{
			if (node == null || node.IsNull) {
				return null;
			}
			return (Nullable<T>)Deserialize (node, typeof(T), options);
		}

		/// <summary>
		/// Deserializes a JSON node into an integer. Throws an ArgumentNullException
		/// if the node is <c>null</c>. Throws a CastException if the node does
		/// not contain an integer.
		/// </summary>
		public int DeserializeToInt (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null) {
				throw new ArgumentNullException ("node");
			}
			return (int)_DeserializeToInt (node, options);
		}

		/// <summary>
		/// Deserializes a JSON node into an unsigned integer. Throws an ArgumentNullException
		/// if the node is <c>null</c>. Throws a CastException if the node does
		/// not contain an unsigned integer.
		/// </summary>
		public uint DeserializeToUInt (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null) {
				throw new ArgumentNullException ("node");
			}
			return (uint)_DeserializeToUInt (node, options);
		}

		/// <summary>
		/// Deserializes a JSON node into a byte. Throws an ArgumentNullException
		/// if the node is <c>null</c>. Throws a CastException if the node does
		/// not contain a byte.
		/// </summary>
		public byte DeserializeToByte (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null) {
				throw new ArgumentNullException ("node");
			}
			return (byte)_DeserializeToByte (node, options);
		}

		/// <summary>
		/// Deserializes a JSON node into a boolean. Throws an ArgumentNullException
		/// if the node is <c>null</c>. Throws a CastException if the node does
		/// not contain a boolean.
		/// </summary>
		public bool DeserializeToBool (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null) {
				throw new ArgumentNullException ("node");
			}
			return (bool)_DeserializeToBool (node, options);
		}

		/// <summary>
		/// Deserializes a JSON node into a float. Throws an ArgumentNullException
		/// if the node is <c>null</c>. Throws a CastException if the node does
		/// not contain a float.
		/// </summary>
		public float DeserializeToFloat (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null) {
				throw new ArgumentNullException ("node");
			}
			return (float)_DeserializeToFloat (node, options);
		}

		/// <summary>
		/// Deserializes a JSON node into a double. Throws an ArgumentNullException
		/// if the node is <c>null</c>. Throws a CastException if the node does
		/// not contain a double.
		/// </summary>
		public double DeserializeToDouble (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null) {
				throw new ArgumentNullException ("node");
			}
			return (double)_DeserializeToDouble (node, options);
		}

		/// <summary>
		/// Deserializes a JSON node into a long. Throws an ArgumentNullException
		/// if the node is <c>null</c>. Throws a CastException if the node does
		/// not contain a long.
		/// </summary>
		public long DeserializeToLong (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null) {
				throw new ArgumentNullException ("node");
			}
			return (long)_DeserializeToLong (node, options);
		}

		/// <summary>
		/// Deserializes a JSON node into a string.
		/// </summary>
		public string DeserializeToString (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null || node.IsNull) {
				return null;
			}
			return _DeserializeToString (node, options);
		}

		/// <summary>
		/// Deserializes a JSON node into an enum. Throws an ArgumentNullException
		/// if the node is <c>null</c>. Throws an ArgumentException if the generic
		/// type T is not an enum.
		/// </summary>
		public T DeserializeToEnum<T> (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null || node.IsNull) {
				throw new ArgumentNullException ("node");
			}
			if (!typeof(T).IsEnum) {
				throw new ArgumentException ("Generic type is not an enum.");
			}
			return (T)_DeserializeToEnum (node, typeof(T), options);
		}

		/// <summary>
		/// Deserializes a JSON node into a generic list.
		/// </summary>
		public List<T> DeserializeToList<T> (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null || node.IsNull) {
				return null;
			}
			var list = new List<T> ();
			_FeedList (list, node, typeof(T), options);
			return list;
		}

		/// <summary>
		/// Deserializes a JSON node into a System.Object list. If no restrictions
		/// are given, the deserialized types can be doubles, booleans, strings, and arrays
		/// and dictionaries thereof. Restricted types can allow custom types to create
		/// classes or structs instead of dictionaries.
		/// </summary>
		/// <param name="node">JSON node to deserialize.</param>
		/// <param name="restrictedTypes">Restricted types for the object.</param>
		/// <param name="customTypes">Allowed custom types for the object. Restrictions
		/// must allow custom types if not <c>null</c>.</param>
		/// <param name="options">Deserialization options.</param>
		public List<object> DeserializeToObjectList (
			JSONNode node,
			ObjectTypes restrictedTypes = ObjectTypes.JSON,
			Type[] customTypes = null,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null || node.IsNull) {
				return null;
			}
			var list = new List<object> ();
			_FeedList (list, node, typeof(object), options, restrictedTypes, customTypes);
			return list;
		}

		/// <summary>
		/// Deserializes a JSON node into a generic dictionary.
		/// </summary>
		public Dictionary<K, V> DeserializeToDictionary<K, V> (
			JSONNode node,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null || node.IsNull) {
				return null;
			}
			var dictionary = new Dictionary<K, V> ();
			_FeedDictionary (dictionary, node, typeof(K), typeof(V), options);
			return dictionary;
		}

		/// <summary>
		/// Deserializes a JSON node into a dictionary with value type System.Object. If 
		/// no restrictions are given, the deserialized value types can be doubles, booleans, 
		/// strings, and arrays and dictionaries thereof. Restricted types can allow custom 
		/// types to create classes or structs instead of dictionaries.
		/// </summary>
		/// <param name="node">JSON node to deserialize.</param>
		/// <param name="restrictedTypes">Restricted types for the values.</param>
		/// <param name="customTypes">Allowed custom types for the values. Restrictions
		/// must allow custom types if not <c>null</c>.</param>
		/// <param name="options">Deserialization options.</param>
		public Dictionary<K, object> DeserializeToObjectDictionary<K> (
			JSONNode node,
			ObjectTypes restrictedTypes = ObjectTypes.JSON,
			Type[] customTypes = null,
			NodeOptions options = NodeOptions.Default)
		{
			if (node == null || node.IsNull) {
				return null;
			}
			var dictionary = new Dictionary<K, object> ();
			_FeedDictionary (
				dictionary, 
				node, 
				typeof(K), 
				typeof(object), 
				options, 
				restrictedTypes, 
				customTypes);
			return dictionary;
		}

		private object _Deserialize (
			JSONNode node, 
			Type type, 
			NodeOptions options, 
			ObjectTypes types,
			Type[] customTypes)
		{
			if (node == null || node.IsNull) {
				return null;
			}

			object obj;
			if (TryInstantiate (node, type, options, out obj)) {
				DeserializeOn (obj, node, options);
				return obj;
			}

			if (type == typeof(object)) {
				return _DeserializeToObject (node, options, types, customTypes);
			}

			if (type.IsValueType) {
				if (type.IsEnum) {
					return _DeserializeToEnum (node, type, options);
				} else if (type.IsPrimitive) {
					return _DeserializeToPrimitive (node, type, options);
				}
			} else {
				if (type == typeof(string)) {
					return _DeserializeToString (node, options);
				} else if (Nullable.GetUnderlyingType (type) != null) {
					return _DeserializeToNullable (node, type, options);
				} else if (typeof(IList).IsAssignableFrom (type)) {
					return _DeserializeToIList (node, type, options, types, customTypes);
				} else if (Util.IsDictionary (type)) {
					return _DeserializeToIDictionary (node, type, options, types, customTypes);
				}
			}
			return _DeserializeCustom (node, type, options);
		}

		private object _Deserialize (
			JSONNode node,
			Type type,
			NodeOptions options, 
			MemberInfo memberInfo)
		{
			var typeAttribute = memberInfo == null 
				? null : Util.GetAttribute<RestrictTypeAttribute> (memberInfo);
			ObjectTypes types = typeAttribute == null ? ObjectTypes.JSON : typeAttribute.types;
			Type[] customTypes = typeAttribute == null ? null : typeAttribute.customTypes;
			return _Deserialize (node, type, options, types, customTypes);
		}

		private object _DeserializeToObject (
			JSONNode node, 
			NodeOptions options, 
			ObjectTypes restrictedTypes,
			Type[] customTypes)
		{
			if (node.IsArray) {
				if (!restrictedTypes.SupportsArray ()) {
					return _HandleMismatch (options, "Arrays are not supported for object.");
				}
				return _DeserializeToArray (
					node, 
					typeof(object), 
					options,
					restrictedTypes,
					customTypes);
			} else if (node.IsBoolean) {
				if (!restrictedTypes.SupportsBool ()) {
					return _HandleMismatch (options, "Bools are not supported for object.");
				}
				return node.AsBool;
			} else if (node.IsNumber) {
				if (!restrictedTypes.SupportsNumber ()) {
					return _HandleMismatch (options, "Numbers are not supported for object.");
				}
				return node.AsDouble;
			} else if (node.IsObject) {
				if (restrictedTypes.SupportsCustom () && customTypes != null) {
					foreach (Type customType in customTypes) {
						try {
							var obj = Deserialize (node, customType, NodeOptions.Default);
							if (obj != null) {
								return obj;
							}
						} catch (Exception) {
						}
					}
				}

				if (!restrictedTypes.SupportsDictionary ()) {
					return _HandleMismatch (options, "Dictionaries are not supported for object.");
				}
				return _DeserializeToGenericDictionary (
					node, 
					typeof(string), 
					typeof(object), 
					options,
					restrictedTypes,
					customTypes);
			} else if (node.IsString) {
				if (!restrictedTypes.SupportsString ()) {
					return _HandleMismatch (options, "Strings are not supported for object.");
				}
				return _DeserializeToString (node, options);
			} else {
				return _HandleUnknown (options, "Unknown JSON node type " + node);
			}
		}

		private object _DeserializeToNullable (JSONNode node, Type nullableType, NodeOptions options)
		{
			Type underlyingType = Nullable.GetUnderlyingType (nullableType);
			return Deserialize (node, underlyingType, options);
		}

		private object _DeserializeToPrimitive (JSONNode node, Type type, NodeOptions options)
		{
			if (type == typeof(int)) {
				return _DeserializeToInt (node, options);
			} else if (type == typeof(byte)) {
				return _DeserializeToByte (node, options);
			} else if (type == typeof(long)) {
				return _DeserializeToByte (node, options);
			} else if (type == typeof(uint)) {
				return _DeserializeToUInt (node, options);
			} else if (type == typeof(bool)) {
				return _DeserializeToBool (node, options);
			} else if (type == typeof(float)) {
				return _DeserializeToFloat (node, options);
			} else if (type == typeof(double)) {
				return _DeserializeToDouble (node, options);
			} else {
				return _HandleUnknown (options, "Unknown primitive type " + type);
			}
		}

		private string _DeserializeToString (JSONNode node, NodeOptions options)
		{
			if (!node.IsString) {
				return _HandleMismatch (options, "Expected string, found: " + node) as string;
			} else {
				return node.Value;
			}
		}

		private object _DeserializeToInt (JSONNode node, NodeOptions options)
		{
			if (node.IsNumber) {
				int value;
				if (int.TryParse (node.Value, out value)) {
					return value;
				}
			}
			return _HandleMismatch (options, "Expected integer, found " + node);
		}

		private object _DeserializeToUInt (JSONNode node, NodeOptions options)
		{
			if (node.IsNumber) {
				uint value;
				if (uint.TryParse (node.Value, out value)) {
					return value;
				}
			}
			return _HandleMismatch (options, "Expected unsigned integer, found " + node);
		}

		private object _DeserializeToByte (JSONNode node, NodeOptions options)
		{
			if (node.IsNumber) {
				byte value;
				if (byte.TryParse (node.Value, out value)) {
					return value;
				}
			}
			return _HandleMismatch (options, "Expected byte, found " + node);
		}

		private object _DeserializeToLong (JSONNode node, NodeOptions options)
		{
			if (node.IsNumber) {
				long value;
				if (long.TryParse (node.Value, out value)) {
					return value;
				}
			}
			return _HandleMismatch (options, "Expected long, found " + node);
		}

		private object _DeserializeToFloat (JSONNode node, NodeOptions options)
		{
			if (node.IsNumber) {
				return node.AsFloat;
			}
			return _HandleMismatch (options, "Expected float, found " + node);
		}

		private object _DeserializeToDouble (JSONNode node, NodeOptions options)
		{
			if (node.IsNumber) {
				return node.AsDouble;
			}
			return _HandleMismatch (options, "Expected double, found " + node);
		}

		private object _DeserializeToBool (JSONNode node, NodeOptions options)
		{
			if (node.IsBoolean) {
				return node.AsBool;
			}
			return _HandleMismatch (options, "Expected integer, found " + node);
		}

		private object _DeserializeToEnum (JSONNode node, Type enumType, NodeOptions options)
		{
			Func<object> handleError = () => _HandleMismatch (
				                           options, "Expected enum of type " + enumType + ", found: " + node);

			var enumAttribute = Util.GetAttribute<JSONEnumAttribute> (enumType);
			if (enumAttribute != null && enumAttribute.useIntegers && node.IsNumber) {
				try {
					return Enum.ToObject (enumType, _DeserializeToInt (node, options));
				} catch (Exception) {
				}
			} else if (node.IsString) {
				string value = node.Value;
				if (enumAttribute != null) {
					if (enumAttribute.prefix != null) {
						if (!value.StartsWith (enumAttribute.prefix)) {
							return handleError ();
						} else {
							value = value.Substring (enumAttribute.prefix.Length);
						}
					}
					if (enumAttribute.suffix != null) {
						if (!value.EndsWith (enumAttribute.suffix)) {
							return handleError ();
						} else {
							value = value.Substring (0, value.Length - enumAttribute.suffix.Length);
						}
					}
				}
				try {
					return Enum.Parse (enumType, value, true);
				} catch (Exception) {
				}
			}
			return handleError ();
		}

		private IDictionary _DeserializeToIDictionary (
			JSONNode node, 
			Type dictionaryType, 
			NodeOptions options,
			ObjectTypes types,
			Type[] customTypes)
		{
			Type genericType = dictionaryType.IsGenericType ? (dictionaryType.IsGenericTypeDefinition 
				? dictionaryType : dictionaryType.GetGenericTypeDefinition ()) : null;
			if (dictionaryType == typeof(IDictionary)) {
				return _DeserializeToGenericDictionary (
					node, 
					typeof(string), 
					typeof(object), 
					options,
					types,
					customTypes);
			} else if (genericType == typeof(IDictionary<,>) || genericType == typeof(Dictionary<,>)) {
				var args = dictionaryType.GetGenericArguments ();
				return _DeserializeToGenericDictionary (
					node, 
					args [0], 
					args [1], 
					options,
					types,
					customTypes);
			} else {
				return _HandleUnknown (options, "Unknown dictionary type " + dictionaryType) as IDictionary;
			}
		}

		private IList _DeserializeToIList (
			JSONNode node, 
			Type listType, 
			NodeOptions options,
			ObjectTypes types,
			Type[] customTypes)
		{
			Type genericType = listType.IsGenericType ? (listType.IsGenericTypeDefinition 
				? listType : listType.GetGenericTypeDefinition ()) : null;
			if (listType == typeof(Array)) {
				return _DeserializeToArray (
					node, 
					typeof(object), 
					options,
					types,
					customTypes);
			} else if (listType.IsArray) {
				return _DeserializeToArray (
					node, 
					listType.GetElementType (), 
					options,
					types,
					customTypes);
			} else if (listType == typeof(IList)) {
				return _DeserializeToGenericList (
					node, 
					typeof(object), 
					options,
					types,
					customTypes);
			} else if (genericType == typeof(IList<>) || genericType == typeof(List<>)) {
				return _DeserializeToGenericList (
					node, 
					listType.GetGenericArguments () [0], 
					options,
					types,
					customTypes);
			} else {
				return _HandleUnknown (options, "Unknown list type " + listType) as IList;
			}
		}

		private Array _DeserializeToArray (
			JSONNode node, 
			Type elementType, 
			NodeOptions options,
			ObjectTypes types,
			Type[] customTypes)
		{
			IList list = _DeserializeToGenericList (
				             node, 
				             elementType, 
				             options,
				             types,
				             customTypes);
			Array array = Array.CreateInstance (elementType, list.Count);
			list.CopyTo (array, 0);
			return array;
		}

		private IList _DeserializeToGenericList (
			JSONNode node, 
			Type genericArgument, 
			NodeOptions options,
			ObjectTypes types = ObjectTypes.JSON,
			Type[] customTypes = null)
		{
			IList list = (IList)Activator.CreateInstance (typeof(List<>).MakeGenericType (genericArgument));
			_FeedList (list, node, genericArgument, options, types, customTypes);
			return list;
		}

		private void _FeedList (
			IList list,
			JSONNode node, 
			Type genericArgument, 
			NodeOptions options,
			ObjectTypes types = ObjectTypes.JSON,
			Type[] customTypes = null)
		{
			if (node.IsArray) {
				JSONArray array = node as JSONArray;
				IEnumerator enumerator = array.GetEnumerator ();
				while (enumerator.MoveNext ()) {
					JSONNode child = (JSONNode)enumerator.Current;
					// Throws an error if needed.
					list.Add (_Deserialize (
						child, 
						genericArgument, 
						options & ~NodeOptions.ReplaceDeserialized,
						types,
						customTypes));
				}
			} else {
				_HandleMismatch (options, "Expected an array, found " + node);
			}
		}

		private IDictionary _DeserializeToGenericDictionary (
			JSONNode node,
			Type keyType,
			Type valueType,
			NodeOptions options,
			ObjectTypes types = ObjectTypes.JSON,
			Type[] customTypes = null)
		{
			IDictionary dictionary = (IDictionary)Activator
				.CreateInstance (typeof(Dictionary<,>)
					.MakeGenericType (keyType, valueType));
			_FeedDictionary (dictionary, node, keyType, valueType, options, types, customTypes);
			return dictionary;
		}

		private void _FeedDictionary (
			IDictionary dictionary,
			JSONNode node,
			Type keyType,
			Type valueType,
			NodeOptions options,
			ObjectTypes types = ObjectTypes.JSON,
			Type[] customTypes = null)
		{
			if (node.IsObject) {
				JSONObject obj = node as JSONObject;
				IEnumerator enumerator = obj.GetEnumerator ();
				while (enumerator.MoveNext ()) {
					var pair = (KeyValuePair<string, JSONNode>)enumerator.Current;
					// Use default field options to throw at any error.
					object key = _Deserialize (
						             new JSONString (pair.Key), 
						             keyType, 
						             NodeOptions.Default,
						             ObjectTypes.JSON,
						             null /* customTypes */);

					// Throws an error if needed.
					object value = _Deserialize (
						               pair.Value, 
						               valueType, 
						               options & ~NodeOptions.ReplaceDeserialized,
						               types,
						               customTypes);
					dictionary.Add (key, value);
				}
			} else {
				_HandleMismatch (options, "Expected a dictionary, found " + node);
			}
		}

		private object _DeserializeCustom (JSONNode node, Type type, NodeOptions options)
		{
			var conditionalAttributes = type.GetCustomAttributes (typeof(ConditionalInstantiationAttribute), false);
			foreach (object attribute in conditionalAttributes) {
				var condition = attribute as ConditionalInstantiationAttribute;
				if (Equals (node [condition.key].Value, condition.value.ToString ())) {
					JSONNode value = node [condition.key];
					if (condition.removeKey) {
						node.Remove (condition.key);
					}
					object result = Deserialize (node, condition.referenceType, options);
					if (condition.removeKey) {
						node.Add (condition.key, value);
					}
					return result;
				}
			}

			var defaultAttribute = Util.GetAttribute<DefaultInstantiationAttribute> (type);
			if (defaultAttribute != null) {
				return Deserialize (node, defaultAttribute.referenceType, options);
			}

			object obj = null;
			KeyValuePair<string, JSONNode>[] removedKeys = null;
			ConstructorInfo[] constructors = type.GetConstructors (
				                                 BindingFlags.Instance |
				                                 BindingFlags.Public |
				                                 BindingFlags.NonPublic);
			foreach (ConstructorInfo constructor in constructors) {
				var constructorAttribute = Util.GetAttribute<JSONConstructorAttribute> (constructor);
				if (constructorAttribute != null) {
					obj = _CreateObjectWithConstructor (type, constructor, node, out removedKeys);
					break;
				}
			}

			if (obj == null) {
				try {
					obj = Activator.CreateInstance (type);
				} catch (Exception) {
					return _HandleUnknown (options, "Unknown type " + type + " cannot be instantiated.");
				}
			}
				
			DeserializeOn (obj, node, options);
			if (removedKeys != null) {
				foreach (var pair in removedKeys) {
					node.Add (pair.Key, pair.Value);
				}
			}
			return obj;
		}

		private object _CreateObjectWithConstructor (
			Type type,
			ConstructorInfo constructor, 
			JSONNode node,
			out KeyValuePair<string, JSONNode>[] removedKeys)
		{
			ParameterInfo[] parameters = constructor.GetParameters ();
			object[] parameterValues = new object[parameters.Length];
			removedKeys = new KeyValuePair<string, JSONNode>[parameterValues.Length];

			for (int i = 0; i < parameterValues.Length; i++) {
				var parameterAttribute = Util.GetAttribute<JSONNodeAttribute> (parameters [i]);
				string key = parameterAttribute != null && parameterAttribute.key != null
					? parameterAttribute.key : parameters [i].Name;
				parameterValues [i] = Deserialize (
					node [key], 
					parameters [i].ParameterType, 
					parameterAttribute == null ? NodeOptions.Default : parameterAttribute.options);

				removedKeys [i] = new KeyValuePair<string, JSONNode> (key, node [key]);
				node.Remove (key);
			}
			return Activator.CreateInstance (type, parameterValues);
		}

		private void _FeedCustom (object filledObject, JSONNode node, NodeOptions options)
		{
			if (filledObject is IDeserializable) {
				(filledObject as IDeserializable).Deserialize (node, this);
				return;
			}

			var listener = filledObject as IDeserializationListener;
			if (listener != null) {
				listener.OnDeserializationWillBegin (this);
			}

			if (node.IsObject) {
				try {
					Type type = filledObject.GetType ();

					MemberInfo extrasMember = null;
					JSONExtrasAttribute extrasAttribute = null;
					Dictionary<string, object> extras = new Dictionary<string, object> ();

					var members = _GetDeserializedClassMembers (type, out extrasMember, out extrasAttribute);
					JSONObject obj = node as JSONObject;
					IEnumerator enumerator = obj.GetEnumerator ();

					var extrasTypeAttribute = extrasMember == null 
						? null : Util.GetAttribute<RestrictTypeAttribute> (extrasMember);
					ObjectTypes extrasTypes = extrasTypeAttribute == null 
						? ObjectTypes.JSON : extrasTypeAttribute.types;
					Type[] extrasCustomTypes = extrasTypeAttribute == null 
						? null : extrasTypeAttribute.customTypes;

					while (enumerator.MoveNext ()) {
						var pair = (KeyValuePair<string, JSONNode>)enumerator.Current;
						if (members.ContainsKey (pair.Key)) {
							_DeserializeClassMember (filledObject, members [pair.Key], pair.Value);
						} else {
							if (extrasMember != null) {
								extras.Add (pair.Key, _DeserializeToObject (
									pair.Value, 
									extrasAttribute.options,
									extrasTypes,
									extrasCustomTypes));
								continue;
							}

							var objectAttribute = Util.GetAttribute<JSONObjectAttribute> (type);
							if (objectAttribute == null || objectAttribute.options.ShouldThrowAtUnknownKey ()) {
								throw new DeserializationException ("The key " + pair.Key + " does not exist "
								+ "in class " + type);
							}
						}
					}

					if (extrasMember != null) {
						if (extras.Count != 0 || extrasAttribute.options.ShouldAssignNull ()) {
							Util.SetMemberValue (extrasMember, filledObject, extras);
						}
					}

					if (listener != null) {
						listener.OnDeserializationSucceeded (this);
					}
				} catch (Exception exception) {
					if (listener != null) {
						listener.OnDeserializationFailed (this);
					}
					throw exception;
				}
			} else {
				if (listener != null) {
					listener.OnDeserializationFailed (this);
				}
				_HandleMismatch (options, "Expected a JSON object, found " + node);
			}
		}

		private void _DeserializeClassMember (
			object filledObject, 
			List<MemberInfo> memberInfos, 
			JSONNode node)
		{
			for (int i = 0; i < memberInfos.Count; i++) {
				var memberInfo = memberInfos [i];
				var fieldAttribute = Util.GetAttribute<JSONNodeAttribute> (memberInfo);
				var options = fieldAttribute != null ? fieldAttribute.options : NodeOptions.Default;

				try {
					Type type = Util.GetMemberType (memberInfo);
					if (node.IsObject
					    && !type.IsValueType
					    && !typeof(IDictionary).IsAssignableFrom (type)
					    && !options.ShouldReplaceWithDeserialized ()) {
						var value = Util.GetMemberValue (memberInfo, filledObject);
						if (value != null) {
							DeserializeOn (value, node, options);
							return;
						}
					}

					object deserialized = _Deserialize (node, type, options, memberInfo);
					if (deserialized != null || options.ShouldAssignNull ()) {
						Util.SetMemberValue (memberInfo, filledObject, deserialized);
						return;
					}
				} catch (Exception ex) {
					if (i == memberInfos.Count - 1) {
						throw ex;
					}
				}
			}
		}

		private Dictionary<string, List<MemberInfo>> _GetDeserializedClassMembers (
			Type classType,
			out MemberInfo extrasMember,
			out JSONExtrasAttribute extrasAttribute)
		{
			JSONObjectAttribute classAttribute = Util.GetAttribute<JSONObjectAttribute> (classType);
			Dictionary<string, List<MemberInfo>> members = new Dictionary<string, List<MemberInfo>> ();

			var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			if (classAttribute != null && !classAttribute.options.ShouldIgnoreStatic ()) {
				flags |= BindingFlags.Static;
			}

			extrasMember = null;
			extrasAttribute = null;

			foreach (var fieldInfo in classType.GetFields(flags)) {
				if (extrasMember == null) {
					if (Util.IsJSONExtrasMember (fieldInfo, out extrasAttribute)) {
						extrasMember = fieldInfo;
						continue;
					}
				}

				var fieldAttribute = Util.GetAttribute<JSONNodeAttribute> (fieldInfo);
				if (fieldAttribute != null && !fieldAttribute.options.IsDeserialized ()) {
					continue;
				} else if (!fieldInfo.IsLiteral && (fieldInfo.IsPublic || fieldAttribute != null)) {
					string key = (fieldAttribute != null && fieldAttribute.key != null) 
						? fieldAttribute.key : fieldInfo.Name;
					if (!members.ContainsKey (key)) {
						members [key] = new List<MemberInfo> ();
					}
					members [key].Add (fieldInfo);
				}
			}

			if (classAttribute == null || !classAttribute.options.ShouldIgnoreProperties ()) {
				foreach (var propertyInfo in classType.GetProperties(flags)) {
					if (extrasMember == null) {
						if (Util.IsJSONExtrasMember (propertyInfo, out extrasAttribute)) {
							extrasMember = propertyInfo;
							continue;
						}
					}

					var fieldAttribute = Util.GetAttribute<JSONNodeAttribute> (propertyInfo);
					if (fieldAttribute != null && !fieldAttribute.options.IsDeserialized ()) {
						continue;
					} else if (propertyInfo.GetIndexParameters ().Length == 0 && propertyInfo.CanWrite &&
					           (fieldAttribute != null || propertyInfo.GetSetMethod (false) != null)) {
						string key = (fieldAttribute != null && fieldAttribute.key != null) 
							? fieldAttribute.key : propertyInfo.Name;
						if (!members.ContainsKey (key)) {
							members [key] = new List<MemberInfo> ();
						}
						members [key].Add (propertyInfo);
					}
				}
			}

			return members;
		}

		private object _HandleMismatch (NodeOptions options, string message)
		{
			if (!options.ShouldIgnoreTypeMismatch ()) {
				throw new DeserializationException (message);
			} else {
				return null;
			}
		}

		private object _HandleUnknown (NodeOptions options, string message)
		{
			if (!options.ShouldIgnoreUnknownType ()) {
				throw new DeserializationException (message);
			} else {
				return null;
			}
		}
	}
}
