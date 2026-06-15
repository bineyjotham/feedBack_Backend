using FluentValidation;
using FeedBack.API.Dtos;

namespace FeedBack.API.Validators;

public class FeedbackRequestValidator : AbstractValidator<FeedbackRequestDto>
{
    public FeedbackRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(0, 10)
            .WithMessage("Rating must be between 0 and 10");
            
        RuleFor(x => x.Comment)
            .MaximumLength(200)
            .WithMessage("Comment cannot exceed 2000 characters");
            
        RuleFor(x => x.CustomerName)
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 200 characters");
            
        RuleFor(x => x.CustomerEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.CustomerEmail))
            .WithMessage("Invalid email address")
            .MaximumLength(100);
            
        RuleFor(x => x.Institution)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Institution));
            
        RuleFor(x => x.Category)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.Category));
            
        RuleFor(x => x.PersonnelName)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.PersonnelName));
            
        RuleFor(x => x.Source)
            .Must(x => x == null || new[] { "premises", "product_service", "website" }.Contains(x.ToLower()))
            .WithMessage("Source must be 'premises', 'product_service', or 'website'");
    }
}