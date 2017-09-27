using System.ComponentModel.DataAnnotations;

namespace BetterDataAnnotationsValidator
{
	public interface IValidator
	{
		ValidationSummary Validate(ValidationContext context);
	}
}