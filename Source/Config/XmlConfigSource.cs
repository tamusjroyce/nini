#region Copyright
//
// Nini Configuration Project.
// Copyright (C) 2004 Brent R. Matzelle.  All rights reserved.
//
// This software is published under the terms of the MIT X11 license, a copy of 
// which has been included with this distribution in the LICENSE.txt file.
// 
#endregion

using System;
using System.IO;
using System.Xml;
using System.Collections;

namespace Nini.Config
{
	/// <include file='XmlConfigSource.xml' path='//Class[@name="XmlConfigSource"]/docs/*' />
	public class XmlConfigSource : ConfigSourceBase
	{
		#region Private variables
		XmlDocument configDoc = null;
		string savePath = null;
		#endregion

		#region Constructors
		/// <include file='XmlConfigSource.xml' path='//Constructor[@name="Constructor"]/docs/*' />
		public XmlConfigSource ()
		{
			configDoc = new XmlDocument ();
			configDoc.LoadXml ("<Nini/>");
			PerformLoad (configDoc);
		}

		/// <include file='XmlConfigSource.xml' path='//Constructor[@name="ConstructorPath"]/docs/*' />
		public XmlConfigSource (string path)
		{
			savePath = path;
			configDoc = new XmlDocument ();
			configDoc.Load (path);
			PerformLoad (configDoc);
		}

		/// <include file='XmlConfigSource.xml' path='//Constructor[@name="ConstructorXmlReader"]/docs/*' />
		public XmlConfigSource (XmlReader reader)
		{
			configDoc = new XmlDocument ();
			configDoc.Load (reader);
			PerformLoad (configDoc);
		}
		#endregion
		
		#region Public properties
		/// <include file='XmlConfigSource.xml' path='//Property[@name="SavePath"]/docs/*' />
		public string SavePath
		{
			get { return savePath; }
		}
		#endregion
		
		#region Public methods
		/// <include file='XmlConfigSource.xml' path='//Method[@name="Save"]/docs/*' />
		public override void Save ()
		{
			if (!IsSavable ()) {
				throw new ArgumentException ("Source cannot be saved in this state");
			}

			MergeConfigsIntoDocument ();
			configDoc.Save (savePath);
			base.Save ();
		}
		
		/// <include file='XmlConfigSource.xml' path='//Method[@name="SavePath"]/docs/*' />
		public void Save (string path)
		{
			this.savePath = path;
			this.Save ();
		}
		
		/// <include file='XmlConfigSource.xml' path='//Method[@name="SaveTextWriter"]/docs/*' />
		public void Save (TextWriter writer)
		{
			MergeConfigsIntoDocument ();
			configDoc.Save (writer);
			savePath = null;
		}

		/// <include file='IConfigSource.xml' path='//Method[@name="Reload"]/docs/*' />
		public override void Reload ()
		{
			if (savePath == null) {
				throw new ArgumentException ("Error reloading: You must have "
							+ "the loaded the source from a file");
			}

			this.Configs.Clear ();
			configDoc = new XmlDocument ();
			configDoc.Load (savePath);
			PerformLoad (configDoc);
			base.Reload ();
		}

		/// <include file='XmlConfigSource.xml' path='//Method[@name="ToString"]/docs/*' />
		public override string ToString ()
		{
			MergeConfigsIntoDocument ();
			StringWriter writer = new StringWriter ();
			configDoc.Save (writer);

			return writer.ToString ();
		}
		#endregion

		#region Private methods
		/// <summary>
		/// Merges all of the configs from the config collection into the 
		/// XmlDocument.
		/// </summary>
		private void MergeConfigsIntoDocument ()
		{
			RemoveConfigs ();
			foreach (IConfig config in this.Configs)
			{
				string[] keys = config.GetKeys ();

				XmlNode node = GetSectionByName (config.Name);
				if (node == null) {
					node = SectionNode (config.Name);
					configDoc.DocumentElement.AppendChild (node);
				}
				RemoveKeys (config.Name);
				
				for (int i = 0; i < keys.Length; i++)
				{
					SetKey (node, keys[i], config.Get (keys[i]));
				}
			}
		}
		
		/// <summary>
		/// Removes all XML sections that were removed as configs.
		/// </summary>
		private void RemoveConfigs ()
		{
			XmlAttribute attr = null;

			foreach (XmlNode node in configDoc.DocumentElement.ChildNodes)
			{
				if (node.NodeType == XmlNodeType.Element
					&& node.Name == "Section") {

					attr = node.Attributes["Name"];
					if (attr != null) {
						if (this.Configs[attr.Value] == null) {
							configDoc.DocumentElement.RemoveChild (node);
						}
					} else {
						throw new ArgumentException ("Section name attribute not found");
					}
				}
			}
		}
		
		/// <summary>
		/// Removes all XML keys that were removed as config keys.
		/// </summary>
		private void RemoveKeys (string sectionName)
		{
			XmlNode sectionNode = GetSectionByName (sectionName);
			XmlAttribute keyName = null;
			
			if (sectionNode != null) {
				foreach (XmlNode node in sectionNode.ChildNodes)
				{
					if (node.NodeType == XmlNodeType.Element
						&& node.Name == "Key") {

						keyName = node.Attributes["Name"];
						if (keyName != null) {
							if (this.Configs[sectionName].Get (keyName.Value) == null) {
								sectionNode.RemoveChild (node);
							}
						} else {
							throw new ArgumentException ("Name attribute not found in key");
						}
					}
				}
			}
		}

		/// <summary>
		/// Loads all sections and keys.
		/// </summary>
		private void PerformLoad (XmlDocument document)
		{
			this.Merge (this); // required for SaveAll
			
			if (document.DocumentElement.Name != "Nini") {
				throw new ArgumentException ("Did not find Nini XML root node");
			}
			
			LoadSections (document.DocumentElement);
		}
		
		/// <summary>
		/// Loads all configuration sections.
		/// </summary>
		private void LoadSections (XmlNode rootNode)
		{
			ConfigBase config = null;

			foreach (XmlNode child in rootNode.ChildNodes)
			{
				if (child.NodeType == XmlNodeType.Element
					&& child.Name == "Section") {
					config = new ConfigBase (child.Attributes["Name"].Value, this);
					this.Configs.Add (config);
					LoadKeys (child, config);
				}
			}
		}
		
		/// <summary>
		/// Loads all keys for a config.
		/// </summary>
		private void LoadKeys (XmlNode node, ConfigBase config)
		{
			foreach (XmlNode child in node.ChildNodes)
			{
				if (child.NodeType == XmlNodeType.Element
					&& child.Name == "Key") {
					config.Add (child.Attributes["Name"].Value,
								child.Attributes["Value"].Value);
				}
			}
		}
		
		/// <summary>
		/// Sets an XML key.  If it does not exist then it is created.
		/// </summary>
		private void SetKey (XmlNode sectionNode, string key, string value)
		{
			XmlNode node = GetKeyByName (sectionNode, key);
			
			if (node == null) {
				CreateKey (sectionNode, key, value);
			} else {
				node.Attributes["Value"].Value = value;
			}
		}
		
		/// <summary>
		/// Creates a key node and adds it to the collection at the end.
		/// </summary>
		private void CreateKey (XmlNode sectionNode, string key, string value)
		{
			XmlNode node = configDoc.CreateElement ("Key");
			XmlAttribute keyAttr = configDoc.CreateAttribute ("Name");
			XmlAttribute valueAttr = configDoc.CreateAttribute ("Value");
			keyAttr.Value = key;
			valueAttr.Value = value;

			node.Attributes.Append (keyAttr);
			node.Attributes.Append (valueAttr);

			sectionNode.AppendChild (node);
		}
		
		/// <summary>
		/// Returns a new section node.
		/// </summary>
		private XmlNode SectionNode (string name)
		{
			XmlNode result = configDoc.CreateElement ("Section");
			XmlAttribute nameAttr = configDoc.CreateAttribute ("Name");
			nameAttr.Value = name;
			result.Attributes.Append (nameAttr);
			
			return result;
		}

		/// <summary>
		/// Returns a section node by name.
		/// </summary>
		private XmlNode GetSectionByName (string name)
		{
			XmlNode result = null;

			foreach (XmlNode node in configDoc.DocumentElement.ChildNodes)
			{
				if (node.NodeType == XmlNodeType.Element
					&& node.Name == "Section"
					&& node.Attributes["Name"].Value == name) {
					result = node;
					break;
				}
			}

			return result;
		}

		/// <summary>
		/// Returns a key node by name.
		/// </summary>
		private XmlNode GetKeyByName (XmlNode sectionNode, string name)
		{
			XmlNode result = null;

			foreach (XmlNode node in sectionNode.ChildNodes)
			{
				if (node.NodeType == XmlNodeType.Element
					&& node.Name == "Key"
					&& node.Attributes["Name"].Value == name) {
					result = node;
					break;
				}
			}

			return result;
		}
		
		/// <summary>
		/// Returns true if this instance is savable.
		/// </summary>
		private bool IsSavable ()
		{
			return (this.savePath != null
					&& configDoc != null);
		}
		#endregion
	}
}