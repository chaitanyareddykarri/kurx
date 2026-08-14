using FluentValidation;
using Kurx.Api.Endpoints;
using Kurx.Domain.Enums;

namespace Kurx.Api.Validation;

public class CreateCategoryBodyValidator : AbstractValidator<CreateCategoryBody>
{
    public CreateCategoryBodyValidator()
    {
        RuleFor(x => x.Level).NotEmpty().IsEnumName(typeof(CategoryLevel), caseSensitive: false);
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
    }
}
