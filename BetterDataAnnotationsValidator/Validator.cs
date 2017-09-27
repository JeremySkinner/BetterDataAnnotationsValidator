using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BetterDataAnnotationsValidator
{
	public class Validator<T> : IValidator
	{
		private static Lazy<ObjectMetadata> _metadataCache = new Lazy<ObjectMetadata>(() => new ObjectMetadata(typeof(T)));

		/// <summary>
		/// Set to true for compatibility with regular DataAnnotations Validator
		/// </summary>
		public bool StopIfRequiredValidatorFails { get; set; }

		/// <summary>
		/// Set to false for compatibility with regular DataAnnotations validator
		/// </summary>
		public bool RunModelLevelValidatorsIfPropertyValidatorsFail { get; set; } = true;

		public bool BreakOnFirstError { get; set; }

		public ValidationSummary Validate(T instance, ValidationContext context = null)
		{
			context = context ?? new ValidationContext(instance);

			var errors = new List<ValidationResult>();
			errors.AddRange(GetPropertyValidationErrors(instance, context));

			if (errors.Any() && (RunModelLevelValidatorsIfPropertyValidatorsFail == false || BreakOnFirstError))
			{
				return new ValidationSummary(errors);
			}

			errors.AddRange(GetModelLevelValidationErrors(instance, context));

			if (errors.Any() && (RunModelLevelValidatorsIfPropertyValidatorsFail == false || BreakOnFirstError)) 
			{
				return new ValidationSummary(errors);
			}

			if (instance is IValidatableObject validatable)
			{
				errors.AddRange(GetIValidatableObjectValidationErrors(validatable, context));
			}

			var summary = new ValidationSummary(errors);
			return summary;
		}

		protected virtual IEnumerable<ValidationResult> GetIValidatableObjectValidationErrors(IValidatableObject instance,
			ValidationContext context)
		{
			return instance.Validate(context);

		}

		protected virtual IEnumerable<ValidationResult> GetModelLevelValidationErrors(object instance, ValidationContext context)
		{
			return GetValidationErrors(instance, context, GetTypeValidationAttributes(context));
		}

		protected virtual IEnumerable<ValidationResult> GetPropertyValidationErrors(object instance, ValidationContext context)
		{
			var properties = GetProperties(instance, context);
			var errors = new List<ValidationResult>();

			foreach (KeyValuePair<ValidationContext, object> property in properties)
			{
				errors.AddRange(GetValidationErrors(property.Value, property.Key, GetPropertyValidationAttributes(property.Key)));
			}

			return errors;
		}

		protected virtual IEnumerable<ValidationResult> GetValidationErrors(object value, ValidationContext validationContext,
			IEnumerable<ValidationAttribute> attributes)
		{
			var errors = new List<ValidationResult>();

			RequiredAttribute required = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute;

			if (required != null)
			{
				var valResult = required.GetValidationResult(value, validationContext);

				if (valResult != ValidationResult.Success && StopIfRequiredValidatorFails)
				{
					errors.Add(valResult);
					return errors;
				}
			}

			if (errors.Any() && BreakOnFirstError) return errors;

			// Iterate through the rest of the validators, skipping the required validator
			foreach (var attr in attributes)
			{
				if (attr != required)
				{
					var valResult = attr.GetValidationResult(value, validationContext);

					if (valResult != ValidationResult.Success)
					{
						errors.Add(valResult);

						if (BreakOnFirstError) break;
					}
				}
			}

			return errors;
		}


		protected virtual ICollection<KeyValuePair<ValidationContext, object>> GetProperties(object instance,
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


		protected virtual IEnumerable<ValidationAttribute> GetPropertyValidationAttributes(ValidationContext validationContext)
		{
			var typeItem = GetMetadata();

			if (typeItem.Properties.TryGetValue(validationContext.MemberName, out var attributes))
			{
				return attributes;
			}

			return Enumerable.Empty<ValidationAttribute>();
		}

		protected virtual IEnumerable<ValidationAttribute> GetTypeValidationAttributes(ValidationContext validationContext)
		{
			var item = GetMetadata();
			return item.Attributes;
		}

		protected virtual ObjectMetadata GetMetadata()
		{
			return _metadataCache.Value;
		}

		ValidationSummary IValidator.Validate(ValidationContext context)
		{
			if(!(context.ObjectInstance is T))
				throw new InvalidOperationException("Instance is not of type " + typeof(T).FullName);

			return Validate((T)context.ObjectInstance, context);
		}
	}
}