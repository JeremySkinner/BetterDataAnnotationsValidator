using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BetterDataAnnotationsValidator
{
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