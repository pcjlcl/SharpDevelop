﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;

namespace ICSharpCode.Reports.Core
{
	public interface IMycroXaml
	{
		void Initialize(object parent);
		object ReturnedObject
		{
			get;
		}
	}

	/// <summary>
	/// See http://www.codeproject.com/dotnet/MycroXaml.asp
	/// </summary>
	public abstract class MycroParser
	{
		public object Load(XmlElement element)
		{
			return ProcessNode(element, null);
		}

		protected abstract Type GetTypeByName(string ns, string name);
		
		protected object ProcessNode(XmlNode node, object parent)
		{
			object ret=null;
			if (node is XmlElement)
			{
				// instantiate the class
				string ns=node.Prefix;
				string cname=node.LocalName;

				Type t=GetTypeByName(ns, cname);
				if (t == null) {
					t = GetTypeByName (ns,"ErrorItem");
				}
				
//				Trace.Assert(t != null, "Type "+cname+" could not be determined.");
//				Debug.WriteLine("Looking for " + cname + " and got " + t.FullName);
				try
				{
					ret=Activator.CreateInstance(t);
				}
				catch(Exception e)
				{
					Trace.Fail("Type "+cname+" could not be instantiated:\r\n"+e.Message);
				}

				// support the ISupportInitialize interface
				if (ret is ISupportInitialize) {
					((ISupportInitialize)ret).BeginInit();
				}

				// If the instance implements the IMicroXaml interface, then it may need
				// access to the parser.
				if (ret is IMycroXaml) {
					((IMycroXaml)ret).Initialize(parent);
				}

				// implements the class-property-class model
				ProcessAttributes(node, ret, t);
				ProcessChildProperties(node, ret);

				// support the ISupportInitialize interface
				if (ret is ISupportInitialize) {
					((ISupportInitialize)ret).EndInit();
				}

				// If the instance implements the IMicroXaml interface, then it has the option
				// to return an object that replaces the instance created by the parser.
				if (ret is IMycroXaml) {
					ret=((IMycroXaml)ret).ReturnedObject;
				}
				
			}
			return ret;
		}

		protected void ProcessChildProperties(XmlNode node, object parent)
		{
			Type t=parent.GetType();

			// children of a class must always be properties
			foreach(XmlNode child in node.ChildNodes)
			{
				if (child is XmlElement)
				{
					string pname=child.LocalName;
					PropertyInfo pi=t.GetProperty(pname);

					if (pi==null)
					{
						// Special case--we're going to assume that the child is a class instance
						// not associated with the parent object
//						Trace.Fail("Unsupported property: "+pname);
						System.Console.WriteLine("Unsupported property: "+pname);
						continue;
					}

					// a property can only have one child node unless it's a collection
					foreach(XmlNode grandChild in child.ChildNodes)
					{
						if (grandChild is XmlText) {
							SetPropertyToString(parent, pi, child.InnerText);
							break;
						}
						else if (grandChild is XmlElement)
						{
							object propObject=pi.GetValue(parent, null);
							object obj=ProcessNode(grandChild, propObject);

							// A null return is valid in cases where a class implementing the IMicroXaml interface
							// might want to take care of managing the instance it creates itself.  See DataBinding
							if (obj != null)
							{

								// support for ICollection objects
								if (propObject is ICollection)
								{
									MethodInfo mi=t.GetMethod("Add", new Type[] {obj.GetType()});
									if (mi != null)
									{
										try
										{
											mi.Invoke(obj, new object[] {obj});
										}
										catch(Exception e)
										{
											Trace.Fail("Adding to collection failed:\r\n"+e.Message);
										}
									}
									else if (propObject is IList)
									{
										try
										{
											((IList)propObject).Add(obj);
										}
										catch(Exception e)
										{
											Trace.Fail("List/Collection add failed:\r\n"+e.Message);
										}
									}
								}
								else if (!pi.CanWrite) {
									Trace.Fail("Unsupported read-only property: "+pname);
								}
								else
								{
									// direct assignment if not a collection
									try
									{
										pi.SetValue(parent, obj, null);
									}
									catch(Exception e)
									{
										Trace.Fail("Property setter for "+pname+" failed:\r\n"+e.Message);
									}
								}
							}
						}
					}
				}
			}
		}

		protected void ProcessAttributes(XmlNode node, object ret, Type type)
		{
			// process attributes
			foreach(XmlAttribute attr in node.Attributes)
			{
				string pname=attr.Name;
				string pvalue=attr.Value;

				// it's either a property or an event
				PropertyInfo pi=type.GetProperty(pname);

				if (pi != null)
				{
					// it's a property!
					SetPropertyToString(ret, pi, pvalue);
				}
				else
				{
					// who knows what it is???
					Trace.Fail("Failed acquiring property information for "+pname);
				}
			}
		}
		
		void SetPropertyToString(object obj, PropertyInfo pi, string value)
		{
			// it's string, so use a type converter.
			TypeConverter tc=TypeDescriptor.GetConverter(pi.PropertyType);
			try
			{
				if (tc.CanConvertFrom(typeof(string)))
				{
					object val=tc.ConvertFromInvariantString(value);
					pi.SetValue(obj, val, null);
				} else if (pi.PropertyType == typeof(Type)) {
					pi.SetValue(obj, Type.GetType(value), null);
				}
			}
			catch(Exception e)
			{
				String s = String.Format("Property setter for {0} failed {1}\r\n",pi.Name,
				                         e.Message);
				System.Console.WriteLine("MycroParser : {0}",s);
			}
		}
		
	}
}
