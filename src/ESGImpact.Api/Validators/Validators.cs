using ESGImpact.Api.ViewModels;
using FluentValidation;

namespace ESGImpact.Api.Validators;

public class SimulationRequestValidator : AbstractValidator<SimulationRequest>
{
    public SimulationRequestValidator()
    {
        RuleFor(x => x.Activity)
            .NotEmpty().WithMessage("A atividade é obrigatória.")
            .MaximumLength(80);

        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0).WithMessage("O valor não pode ser negativo.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Valor acima do limite suportado.");

        RuleFor(x => x.OwnerCode).MaximumLength(80);
    }
}

public class OffsetQuoteRequestValidator : AbstractValidator<OffsetQuoteRequest>
{
    public OffsetQuoteRequestValidator()
    {
        RuleFor(x => x.KgCo2)
            .GreaterThanOrEqualTo(0).When(x => x.KgCo2.HasValue)
            .WithMessage("KgCo2 não pode ser negativo.");

        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
    }
}
