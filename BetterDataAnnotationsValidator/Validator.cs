using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BetterDataAnnotationsValidator
{
	public class Validator
	{
		static ConcurrentDictionary<Type, ObjectMetadata> _cache = new ConcurrentDictionary<Type, ObjectMetadata>();

		public ValidationSummary Validate(object instance, ValidationContext context = null)
		{
			context = context ?? new ValidationContext(instance);

			var errors = new List<ValidationResult>();
			errors.AddRange(GetPropertyValidationErrors(instance, context));
			errors.AddRange(GetModelLevelValidationErrors(instance, context));

			if (instance is IValidatableObject validatable)
			{
				var validatableResults = validatable.Validate(context);
				errors.AddRange(validatableResults);
			}

			var summary = new ValidationSummary(errors);
			return summary;
		}

		private IEnumerable<ValidationResult> GetModelLevelValidationErrors(object instance, ValidationContext context)
		{
			return GetValidationErrors(instance, context, GetTypeValidationAttributes(context));
		}

		private IEnumerable<ValidationResult> GetPropertyValidationErrors(object instance, ValidationContext context)
		{
			var properties = GetProperties(instance, context);
			var errors = new List<ValidationResult>();

			foreach (KeyValuePair<ValidationContext, object> property in properties)
			{
				errors.AddRange(GetValidationErrors(property.Value, property.Key, GetPropertyValidationAttributes(property.Key)));
			}

			return errors;
		}

		private static IEnumerable<ValidationResult> GetValidationErrors(object value, ValidationContext validationContext,
			IEnumerable<ValidationAttribute> attributes)
		{
			var errors = new List<ValidationResult>();

			// Get the required validator if there is one and test it first, aborting on failure
			RequiredAttribute required = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute;

			if (required != null)
			{
				var valResult = required.GetValidationResult(value, validationContext);

				if (valResult != ValidationResult.Success)
				{
					errors.Add(valResult);
					return errors;
				}
			}

			// Iterate through the rest of the validators, skipping the required validator
			foreach (var attr in attributes)
			{
				if (attr != required)
				{
					var valResult = attr.GetValidationResult(value, validationContext);

					if (valResult != ValidationResult.Success)
					{
						errors.Add(valResult);
					}
				}
			}

			return errors;
		}


		private ICollection<KeyValuePair<ValidationContext, object>> GetProperties(object instance,
			ValidationContext validationContext)
		{
			var properties = TypeDescriptor.GetProperties(instance);
			var propertyValues = new List<KeyValuePair<ValidationContext, object>>(properties.Count);

			foreach (PropertyDescriptor property in properties)
			{
				var context =
					new ValidationContext(instance, validationContext, validationContext.Items) {MemberName = property.Name};

				if (GetPropertyValidationAttributes(context).Any())
				{
					propertyValues.Add(new KeyValuePair<ValidationContext, object>(context, property.GetValue(instance)));
				}
			}

			return propertyValues;
		}


		private IEnumerable<ValidationAttribute> GetPropertyValidationAttributes(ValidationContext validationContext)
		{
			var typeItem = GetMetadata(validationContext.ObjectType);

			if (typeItem.Properties.TryGetValue(validationContext.MemberName, out var attributes))
			{
				return attributes;
			}

			return Enumerable.Empty<ValidationAttribute>();
		}

		private IEnumerable<ValidationAttribute> GetTypeValidationAttributes(ValidationContext validationContext)
		{
			var item = GetMetadata(validationContext.ObjectType);
			return item.Attributes;
		}

		private ObjectMetadata GetMetadata(Type type)
		{
			return _cache.GetOrAdd(type, t => new ObjectMetadata(t));
		}

		public class ObjectMetadata
		{
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

	public class ValidationSummary
	{
		public ValidationSummary(List<ValidationResult> results)
		{
			Results = results;
			Success = Results.Count == 0;
		}

		public bool Success { get; }
		public List<ValidationResult> Results { get; private set; }
	}
}