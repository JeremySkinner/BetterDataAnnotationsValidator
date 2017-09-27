using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BetterDataAnnotationsValidator
{
	public class ObjectMetadata
	{
		public ObjectMetadata()
		{
		}

		public ObjectMetadata(Type type)
		{
			Attributes = TypeDescriptor.GetAttributes(type).Cast<Attribute>().OfType<ValidationAttribute>().ToList();

			var properties = TypeDescriptor.GetProperties(type);

			foreach (PropertyDescriptor property in properties)
			{
				var attributes = new List<Attribute>(property.Attributes.Cast<Attribute>());
				var typeAttributes = TypeDescriptor.GetAttributes(property.PropertyType).Cast<Attribute>();

				// This logic from MS Reference source ValidationAttributeStore.cs 

				bool removedAttribute = false;
				foreach (Attribute attr in typeAttributes)
				{
					for (int i = attributes.Count - 1; i >= 0; --i)
					{
						// We must use ReferenceEquals since attributes could Match if they are the same.
						// Only ReferenceEquals will catch actual duplications.
						if (ReferenceEquals(attr, attributes[i]))
						{
							attributes.RemoveAt(i);
							removedAttribute = true;
						}
					}
				}
				var attributesToUse = removedAttribute ? new AttributeCollection(attributes.ToArray()) : property.Attributes;

				Properties[property.Name] = attributesToUse.OfType<ValidationAttribute>().ToList();
			}
		}

		public IEnumerable<ValidationAttribute> Attributes { get; set; }

		public Dictionary<string, List<ValidationAttribute>> Properties { get; } =
			new Dictionary<string, List<ValidationAttribute>>();
	}
}